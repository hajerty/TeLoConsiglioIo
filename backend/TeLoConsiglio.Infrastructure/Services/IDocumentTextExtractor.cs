namespace TeLoConsiglio.Infrastructure.Services;

public interface IDocumentTextExtractor
{
    /// <summary>Extracts plain text from a PDF/DOCX/TXT file.</summary>
    Task<string> ExtractTextAsync(string filePath, string originalFileName, CancellationToken ct = default);
}
