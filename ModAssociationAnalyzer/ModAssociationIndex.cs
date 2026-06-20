using ModAssociationAnalyzer.Ipc;
using Newtonsoft.Json.Linq;

namespace ModAssociationAnalyzer;

/// <summary> One design that references a given mod, plus the association's settings. </summary>
public sealed record AssociatedDesign(
    Guid   DesignId,
    string DesignName,
    bool   Enabled,
    bool   Inherit,
    bool   Remove,
    int    Priority)
{
    public string StateLabel =>
        Remove  ? "Remove"
      : Inherit ? "Inherit"
      : Enabled ? "Enabled"
      :           "Disabled";
}

/// <summary> A Penumbra mod (or a mod only referenced by a design) and the designs that reference it. </summary>
public sealed record ModEntry(string Directory, string Name, List<AssociatedDesign> Designs)
{
    /// <summary> True when the mod is referenced by a design but is not in the current Penumbra mod list. </summary>
    public bool Unknown { get; init; }
}

/// <summary>
/// Builds the reverse index mod -> designs by combining Penumbra's mod list with the
/// "Mods" (Mod Associations) array parsed out of each Glamourer design's serialized JSON.
/// Refresh is explicit (cheap to call, but iterates every design); the UI drives it.
/// </summary>
public sealed class ModAssociationIndex(PenumbraIpc penumbra, GlamourerIpc glamourer)
{
    public IReadOnlyList<ModEntry> Mods        { get; private set; } = [];
    public int                     DesignCount { get; private set; }
    public DateTime                LastRefresh { get; private set; }
    public string?                 LastError   { get; private set; }

    public void Refresh()
    {
        try
        {
            // directory (immutable id, case-insensitive) -> entry
            var byDir = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var (dir, name) in penumbra.GetMods())
                byDir[dir] = new ModEntry(dir, name, []);

            var designs = glamourer.GetDesigns();
            DesignCount = designs.Count;

            foreach (var (id, designName) in designs)
            {
                if (glamourer.GetDesign(id)?["Mods"] is not JArray mods)
                    continue;

                foreach (var entry in mods.OfType<JObject>())
                {
                    var dir = entry.Value<string>("Directory") ?? string.Empty;
                    if (dir.Length == 0)
                        continue;

                    // Serialization only writes the field(s) relevant to the chosen state, so
                    // missing keys legitimately default to false/0 (see Glamourer Design.SerializeMods).
                    var assoc = new AssociatedDesign(
                        id,
                        designName,
                        Enabled: entry.Value<bool?>("Enabled") ?? false,
                        Inherit: entry.Value<bool?>("Inherit") ?? false,
                        Remove: entry.Value<bool?>("Remove") ?? false,
                        Priority: entry.Value<int?>("Priority") ?? 0);

                    if (!byDir.TryGetValue(dir, out var mod))
                    {
                        // The design references a mod that isn't in the current Penumbra list
                        // (uninstalled / renamed directory). Surface it rather than dropping it.
                        var fallbackName = entry.Value<string>("Name") ?? dir;
                        mod = new ModEntry(dir, fallbackName, []) { Unknown = true };
                        byDir[dir] = mod;
                    }

                    mod.Designs.Add(assoc);
                }
            }

            Mods        = byDir.Values.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
            LastError   = null;
            LastRefresh = DateTime.Now;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }
}
