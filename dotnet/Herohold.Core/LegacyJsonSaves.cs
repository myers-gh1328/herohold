using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Herohold.Core;

/// <summary>An old save, with the canonical character document it kept; that copy is what its sheet showed.</summary>
public sealed record LegacySave(WorkspaceSnapshot Snapshot, JsonObject? Canonical);

/// <summary>Reads the JSON save files Herohold wrote before the SQLite store, for the one-time import. Never writes.</summary>
public static class LegacyJsonSaves
{
    public const int MaximumBytes = 2 * 1024 * 1024;
    public const int MaximumFiles = 1000;
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
        MaxDepth = 32,
        WriteIndented = true
    };

    /// <summary>Every save in <paramref name="directory"/>, ordered by id. Any damaged file stops the read.</summary>
    public static async Task<IReadOnlyList<LegacySave>> ReadAllAsync(string directory, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory)) return [];
        var files = Directory.EnumerateFiles(Path.GetFullPath(directory), "*.json", SearchOption.TopDirectoryOnly).Take(MaximumFiles + 1).ToList();
        if (files.Count > MaximumFiles) throw new InvalidDataException("The save folder holds more than 1,000 characters.");
        var snapshots = new List<LegacySave>();
        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            snapshots.Add(await ReadAsync(file, ct));
        }
        return snapshots.AsReadOnly();
    }

    /// <summary>
    /// A workspace document as Herohold writes it (a save or a workspace download). Documents from before the typed
    /// build kept the build as a canonical character document; the typed build is read from that copy.
    /// </summary>
    public static LegacySave ReadDocument(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var node = document.DeepClone().AsObject();
        var canonical = node["character"] is JsonObject saved && saved["canonicalBuild"] is JsonObject build ? build.DeepClone().AsObject() : null;
        (node["character"] as JsonObject)?.Remove("canonicalBuild");
        var snapshot = node.Deserialize<WorkspaceSnapshot>(Json) ?? throw new JsonException("The workspace document is empty.");
        if (snapshot.Character is not null && canonical is not null) snapshot = snapshot with { Character = snapshot.Character with { Build = BuildFacts.FromCanonical(canonical) } };
        return new(snapshot, canonical);
    }

    private static async Task<LegacySave> ReadAsync(string path, CancellationToken ct)
    {
        var id = Path.GetFileNameWithoutExtension(path);
        try { WorkspaceValidation.Id(id); }
        catch (ArgumentException ex) { throw new InvalidDataException("The save folder holds a file with an invalid character id: " + Path.GetFileName(path), ex); }
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Save files cannot be symbolic links.");
        if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("The save exceeds its 2 MB limit: " + Path.GetFileName(path));
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous);
            var node = await JsonNode.ParseAsync(stream, documentOptions: new() { MaxDepth = Json.MaxDepth }, cancellationToken: ct) as JsonObject
                ?? throw new InvalidDataException("The save file is empty: " + Path.GetFileName(path));
            var (snapshot, canonical) = ReadDocument(node);
            if (snapshot.SchemaVersion is not (1 or 2 or 3) || snapshot.Revision < 1 || snapshot.Character?.Id != id)
                throw new InvalidDataException("The save has an unsupported schema, invalid revision, or mismatched id: " + Path.GetFileName(path));
            WorkspaceValidation.Character(snapshot.Character);
            return new(snapshot with { SchemaVersion = 3 }, canonical);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            throw new InvalidDataException("The save is invalid: " + Path.GetFileName(path), ex);
        }
    }
}
