using Microsoft.AspNetCore.Http;

namespace TeLoConsiglio.Api.Auth;

/// <summary>
/// Whitelist semplice di estensioni / mime / dimensione per gli upload utente.
/// </summary>
public static class UploadValidator
{
    public const long MaxSizeBytes = 20L * 1024 * 1024; // 20 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".txt", ".md", ".docx"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "text/plain",
        "text/markdown",
        "application/octet-stream", // alcuni client mandano questo, lo accettiamo se ext OK
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    public static (bool ok, string? error, string safeExtension) Validate(IFormFile file)
    {
        if (file.Length <= 0)
            return (false, "File vuoto", "");
        if (file.Length > MaxSizeBytes)
            return (false, $"File troppo grande (max {MaxSizeBytes / (1024 * 1024)} MB)", "");

        // Sanifichiamo il nome: prendiamo solo l'estensione dal *basename* (no path)
        var baseName = Path.GetFileName(file.FileName ?? "");
        var ext = Path.GetExtension(baseName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            return (false, "Estensione non consentita. Ammessi: .pdf, .txt, .md, .docx", "");

        // Protezione path-traversal: rifiuta se nome contiene NUL o caratteri di separazione
        if (baseName.Contains('\0') || baseName.Contains('/') || baseName.Contains('\\'))
            return (false, "Nome file non valido", "");

        var mime = (file.ContentType ?? "").ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(mime) && !AllowedMimeTypes.Contains(mime))
            return (false, $"MIME type non consentito: {mime}", "");

        return (true, null, ext);
    }
}
