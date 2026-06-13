namespace TeLoConsiglio.Infrastructure.Services;

/// <summary>
/// Servizio di cifratura/decifratura file con AES-256-GCM.
/// Quando <see cref="IsEnabled"/> è false (chiave non configurata) le operazioni
/// passano i dati in chiaro senza trasformazione.
/// </summary>
public interface IFileEncryptor
{
    /// <summary>True se la chiave DOC_ENCRYPTION_KEY è configurata e il servizio è attivo.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Cifra il contenuto di <paramref name="plain"/> e scrive il file cifrato in <paramref name="filePath"/>.
    /// Se <see cref="IsEnabled"/> è false copia il flusso in chiaro.
    /// </summary>
    Task EncryptToFileAsync(Stream plain, string filePath, CancellationToken ct = default);

    /// <summary>
    /// Apre <paramref name="filePath"/> e restituisce uno stream già decifrato (MemoryStream).
    /// Se il file non ha il magic header TCE1 viene restituito in chiaro (retrocompatibilità).
    /// </summary>
    Task<Stream> OpenDecryptedReadAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Legge e decifra l'intero file, restituendo i byte in chiaro.
    /// Se il file non ha il magic header TCE1 viene restituito in chiaro (retrocompatibilità).
    /// </summary>
    Task<byte[]> DecryptAllBytesAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Controlla se il file ha il magic header "TCE1" (prime 4 byte).
    /// Utile per distinguere file cifrati da file pre-feature.
    /// </summary>
    bool IsLikelyEncrypted(string filePath);
}
