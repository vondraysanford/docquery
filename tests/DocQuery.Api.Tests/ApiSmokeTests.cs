using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DocQuery.Core.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DocQuery.Api.Tests;

/// <summary>
/// Boots the real API in-process with fake providers, then exercises the
/// ingestion and query paths over HTTP — no Ollama/ChromaDB required.
/// </summary>
public class ApiSmokeTests : IClassFixture<ApiSmokeTests.TestAppFactory>
{
    public class TestAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmbeddingProvider>();
                services.RemoveAll<ILlmProvider>();
                services.RemoveAll<IVectorStore>();

                services.AddSingleton<IEmbeddingProvider, FakeEmbeddingProvider>();
                services.AddSingleton<ILlmProvider, FakeLlmProvider>();
                services.AddSingleton<IVectorStore, FakeVectorStore>();
            });
        }
    }

    private readonly HttpClient _client;

    public ApiSmokeTests(TestAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static MultipartFormDataContent FileUpload(string fileName, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes), "file", fileName);
        return content;
    }

    [Fact]
    public async Task Health_Returns200()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Upload_TextFile_ReturnsChunkCount()
    {
        var body = FileUpload("smoke.txt", Encoding.UTF8.GetBytes("DocQuery ingestion smoke test content."));

        var response = await _client.PostAsync("/api/documents/upload", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("smoke.txt", result.GetProperty("fileName").GetString());
        Assert.True(result.GetProperty("chunksCreated").GetInt32() >= 1);
    }

    [Fact]
    public async Task Upload_MissingFile_Returns400()
    {
        var response = await _client.PostAsync("/api/documents/upload", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_UnsupportedExtension_Returns400()
    {
        var body = FileUpload("malware.exe", Encoding.UTF8.GetBytes("nope"));

        var response = await _client.PostAsync("/api/documents/upload", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_CorruptPdf_Returns400()
    {
        var body = FileUpload("broken.pdf", Encoding.UTF8.GetBytes("not a real pdf"));

        var response = await _client.PostAsync("/api/documents/upload", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Query_AfterUpload_ReturnsCannedAnswerWithSources()
    {
        var upload = FileUpload("query-fixture.txt", Encoding.UTF8.GetBytes("The retrieval smoke test fixture."));
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync("/api/documents/upload", upload)).StatusCode);

        var response = await _client.PostAsJsonAsync("/api/query", new { question = "What is this?" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(FakeLlmProvider.CannedAnswer, result.GetProperty("answer").GetString());
        Assert.True(result.GetProperty("sources").GetArrayLength() >= 1);
        Assert.False(string.IsNullOrEmpty(result.GetProperty("conversationId").GetString()));
    }

    [Fact]
    public async Task Query_EmptyQuestion_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/query", new { question = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_UploadedDocument_Returns204()
    {
        var upload = FileUpload("deletable.txt", Encoding.UTF8.GetBytes("Delete me."));
        var uploadResult = await (await _client.PostAsync("/api/documents/upload", upload)).Content.ReadFromJsonAsync<JsonElement>();
        var documentId = uploadResult.GetProperty("documentId").GetString();

        var response = await _client.DeleteAsync($"/api/documents/{documentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}