namespace TeLoConsiglio.Infrastructure.Services;

public interface IDocumentTextExtractor
{
    /// <summary>Extracts plain text from a PDF/DOCX/TXT file.</summary>
    Task<string> ExtractTextAsync(string filePath, string originalFileName, CancellationToken ct = default);

    /// <summary>
    /// Extracts plain text from an already-open stream (e.g., decrypted in-memory).
    /// <paramref name="originalFileName"/> is used only to determine the format.
    /// </summary>
    Task<string> ExtractTextFromStreamAsync(Stream stream, string originalFileName, CancellationToken ct = default);
}
