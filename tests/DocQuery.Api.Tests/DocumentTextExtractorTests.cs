using System.Text;
using DocQuery.Api.Services;
using Microsoft.AspNetCore.Http;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace DocQuery.Api.Tests;

public class DocumentTextExtractorTests
{
    [Theory]
    [InlineData("notes.pdf", true)]
    [InlineData("notes.md", true)]
    [InlineData("notes.markdown", true)]
    [InlineData("notes.txt", true)]
    [InlineData("NOTES.TXT", true)]
    [InlineData("notes.docx", false)]
    [InlineData("archive.zip", false)]
    [InlineData("no-extension", false)]
    public void IsSupported_MatchesExpectedExtensions(string fileName, bool expected)
    {
        Assert.Equal(expected, DocumentTextExtractor.IsSupported(fileName));
    }

    [Theory]
    [InlineData("sample.txt")]
    [InlineData("sample.md")]
    public async Task ExtractAsync_PlainText_PassesContentThrough(string fileName)
    {
        const string content = "# Heading\n\nSome plain text content.";
        var file = MakeFormFile(Encoding.UTF8.GetBytes(content), fileName);

        var extracted = await DocumentTextExtractor.ExtractAsync(file, CancellationToken.None);

        Assert.Equal(content, extracted);
    }

    [Fact]
    public async Task ExtractAsync_Pdf_ExtractsPageText()
    {
        // Build a tiny valid PDF in memory — no binary fixture in the repo.
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText("DocQuery smoke test sentence", 12, new PdfPoint(25, 700), font);
        var file = MakeFormFile(builder.Build(), "generated.pdf");

        var extracted = await DocumentTextExtractor.ExtractAsync(file, CancellationToken.None);

        Assert.Contains("DocQuery smoke test sentence", extracted);
    }

    private static IFormFile MakeFormFile(byte[] bytes, string fileName)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, stream.Length, "file", fileName);
    }
}