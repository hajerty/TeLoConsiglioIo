using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TeLoConsiglio.Infrastructure.Services;

/// <summary>
/// Implementazione di <see cref="IFileEncryptor"/> basata su AES-256-GCM.
///
/// Formato file cifrato (binario):
///   [Magic 4B "TCE1"] [Nonce 12B] [Tag 16B] [Ciphertext N byte]
///
/// La chiave viene letta dall'env var DOC_ENCRYPTION_KEY (base64 di 32 byte).
/// Se la chiave è assente <see cref="IsEnabled"/> è false e i file vengono
/// scritti/letti in chiaro (retrocompatibilità con istanze pre-feature).
/// </summary>
public sealed class AesGcmFileEncryptor : IFileEncryptor
{
    // Magic header che identifica i file cifrati con questo formato.
    private static readonly byte[] Magic = "TCE1"u8.ToArray(); // 4 byte
    private const int NonceSize = 12;  // AES-GCM standard nonce
    private const int TagSize = 16;    // AES-GCM authentication tag

    private readonly byte[]? _key;
    private bool _warnedOnce;
    private readonly ILogger<AesGcmFileEncryptor> _logger;

    public bool IsEnabled => _key != null;

    public AesGcmFileEncryptor(IConfiguration cfg, ILogger<AesGcmFileEncryptor> logger)
    {
        _logger = logger;

        var raw = cfg["DOC_ENCRYPTION_KEY"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            // Avviso emesso una volta all'avvio, non a ogni operazione.
            _warnedOnce = true;
            return;
        }

        try
        {
            var key = Convert.FromBase64String(raw);
            if (key.Length != 32)
                throw new InvalidOperationException(
                    $"DOC_ENCRYPTION_KEY deve essere esattamente 32 byte (256 bit) in base64. Trovati: {key.Length} byte.");
            _key = key;
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "DOC_ENCRYPTION_KEY non è base64 valido. Cifratura disabilitata.");
        }
    }

    /// <summary>
    /// Chiamato da Program.cs all'avvio per loggare il warning una sola volta.
    /// </summary>
    public void LogStartupWarningIfNeeded()
    {
        if (_warnedOnce && !IsEnabled)
        {
            _logger.LogWarning(
                "DOC_ENCRYPTION_KEY non configurata: i file caricati dagli utenti vengono salvati IN CHIARO su disco. " +
                "Per abilitare la cifratura AES-256-GCM, imposta DOC_ENCRYPTION_KEY con 32 byte in base64 " +
                "(genera con: openssl rand -base64 32). " +
                "ATTENZIONE: se si perde la chiave i file cifrati diventano irrecuperabili.");
        }
    }

    public async Task EncryptToFileAsync(Stream plain, string filePath, CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            // Chiave non configurata: salva in chiaro.
            using var fs = File.Create(filePath);
            await plain.CopyToAsync(fs, ct);
            return;
        }

        // Leggi tutto il plaintext in memoria (file max 20 MB per policy upload).
        var plainBytes = await ReadAllBytesAsync(plain, ct);

        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);
        var ciphertext = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key!, TagSize);
        aes.Encrypt(nonce, plainBytes, ciphertext, tag);

        // Scrivi: magic | nonce | tag | ciphertext
        using var file = File.Create(filePath);
        await file.WriteAsync(Magic, ct);
        await file.WriteAsync(nonce, ct);
        await file.WriteAsync(tag, ct);
        await file.WriteAsync(ciphertext, ct);
    }

    public async Task<Stream> OpenDecryptedReadAsync(string filePath, CancellationToken ct = default)
    {
        var bytes = await DecryptAllBytesAsync(filePath, ct);
        return new MemoryStream(bytes);
    }

    public async Task<byte[]> DecryptAllBytesAsync(string filePath, CancellationToken ct = default)
    {
        if (!IsEnabled || !IsLikelyEncrypted(filePath))
        {
            // File in chiaro (pre-feature o cifratura disabilitata).
            return await File.ReadAllBytesAsync(filePath, ct);
        }

        var raw = await File.ReadAllBytesAsync(filePath, ct);
        // Layout: magic(4) | nonce(12) | tag(16) | ciphertext(n)
        var headerLen = Magic.Length + NonceSize + TagSize;
        if (raw.Length < headerLen)
            throw new InvalidDataException($"File cifrato troppo corto: {filePath}");

        var nonce = raw.AsSpan(Magic.Length, NonceSize).ToArray();
        var tag = raw.AsSpan(Magic.Length + NonceSize, TagSize).ToArray();
        var ciphertext = raw.AsSpan(headerLen).ToArray();
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key!, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    public bool IsLikelyEncrypted(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        using var fs = File.OpenRead(filePath);
        if (fs.Length < Magic.Length) return false;
        var header = new byte[Magic.Length];
        var read = fs.Read(header, 0, header.Length);
        return read == Magic.Length && header.AsSpan().SequenceEqual(Magic.AsSpan());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken ct)
    {
        if (stream is MemoryStream ms)
            return ms.ToArray();

        using var buf = new MemoryStream();
        await stream.CopyToAsync(buf, ct);
        return buf.ToArray();
    }
}
