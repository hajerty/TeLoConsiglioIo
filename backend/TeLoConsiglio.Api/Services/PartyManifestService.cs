using System.Text.Json;

namespace TeLoConsiglio.Api.Services;

public record PartyManifest(string Key, string FullName, string LineaPoliticaMd);

public class PartyManifestService
{
    private readonly IReadOnlyDictionary<string, PartyManifest> _manifests;

    public PartyManifestService(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Resources", "party-manifests.json");
        if (!File.Exists(path))
        {
            _manifests = new Dictionary<string, PartyManifest>();
            return;
        }
        using var stream = File.OpenRead(path);
        var doc = JsonDocument.Parse(stream);
        var dict = new Dictionary<string, PartyManifest>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var key = prop.Name;
            var fullName = prop.Value.GetProperty("fullName").GetString() ?? key;
            var linea = prop.Value.GetProperty("lineaPoliticaMd").GetString() ?? "";
            dict[key] = new PartyManifest(key, fullName, linea);
        }
        _manifests = dict;
    }

    public IReadOnlyDictionary<string, PartyManifest> All => _manifests;

    public PartyManifest? GetManifest(string partyKey)
    {
        if (string.IsNullOrWhiteSpace(partyKey)) return null;
        _manifests.TryGetValue(partyKey, out var result);
        return result;
    }
}
