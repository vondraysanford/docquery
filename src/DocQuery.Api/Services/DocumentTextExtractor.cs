using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DocQuery.Api.Services;

/// <summary>
/// Extracts plain text from uploaded files: PDFs via PdfPig,
/// Markdown and plain text passed through as-is.
/// </summary>
public static class DocumentTextExtractor
{
    private static readonly HashSet<string> PlainTextExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".txt", ".md", ".markdown" };

    public static bool IsSupported(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || PlainTextExtensions.Contains(extension);
    }

    public static async Task<string> ExtractAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return await ExtractPdfAsync(file, cancellationToken);

        using var reader = new StreamReader(file.OpenReadStream());
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static async Task<string> ExtractPdfAsync(IFormFile file, CancellationToken cancellationToken)
    {
        // PdfPig requires a seekable stream; the multipart request stream is not.
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        using var pdf = PdfDocument.Open(buffer);
        var pages = pdf.GetPages().Select(page => ContentOrderTextExtractor.GetText(page));
        return string.Join("\n\n", pages);
    }
}
