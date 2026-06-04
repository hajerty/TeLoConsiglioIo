using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace TeLoConsiglio.Infrastructure.Services;

public class DocumentTextExtractor : IDocumentTextExtractor
{
    public Task<string> ExtractTextAsync(string filePath, string originalFileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => Task.FromResult(ExtractPdf(filePath)),
            ".docx" => Task.FromResult(ExtractDocx(filePath)),
            ".txt" or ".md" => File.ReadAllTextAsync(filePath, ct),
            _ => Task.FromResult(string.Empty)
        };
    }

    private static string ExtractPdf(string path)
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(path);
        foreach (var page in doc.GetPages())
        {
            sb.AppendLine(page.Text);
        }
        return sb.ToString();
    }

    private static string ExtractDocx(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);
        var body = doc.MainDocumentPart?.Document.Body;
        if (body == null) return string.Empty;
        var sb = new StringBuilder();
        foreach (var p in body.Descendants<Paragraph>())
        {
            sb.AppendLine(p.InnerText);
        }
        return sb.ToString();
    }
}
