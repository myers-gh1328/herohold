using System.Text.Json;
using System.Text.Json.Serialization;

namespace Herohold.Core;

/// <summary>Checks a workspace before any store accepts it: valid character, safe revision, at most 2 MB of character data.</summary>
public static class WorkspaceLimits
{
    public const int MaximumBytes = 2 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public static void Validate(WorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaVersion is not (1 or 2 or 3)) throw new InvalidDataException("This workspace schema is not supported.");
        if (snapshot.Revision < 0 || snapshot.Revision == long.MaxValue)
            throw new WorkspaceConflictException("This workspace revision cannot advance safely.");
        WorkspaceValidation.Character(snapshot.Character);
        if (JsonSerializer.SerializeToUtf8Bytes(snapshot.Character, Json).Length > MaximumBytes)
            throw new InvalidDataException("The workspace exceeds its 2 MB storage limit.");
    }
}
