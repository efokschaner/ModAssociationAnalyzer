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

    /// <summary> Full path in Penumbra's mod folder tree (e.g. "Outfits/Cool Mod"); empty/unknown otherwise. </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary> True when at least one referencing design stored a different name or directory than this mod has now. </summary>
    public bool HasMismatch { get; set; }
}

/// <summary>
/// Builds the reverse index mod -> designs by combining Penumbra's mod list with the
/// "Mods" (Mod Associations) array parsed out of each Glamourer design's serialized JSON.
/// Refresh is explicit (cheap to call, but iterates every design); the UI drives it.
/// </summary>
public sealed class ModAssociationIndex(PenumbraIpc penumbra, GlamourerIpc glamourer)
{
    public IReadOnlyList<ModEntry> Mods        { get; private set; } = [];
    public FolderNode              Root        { get; private set; } = new(string.Empty, string.Empty);
    public int                     DesignCount { get; private set; }
    public DateTime                LastRefresh { get; private set; }
    public string?                 LastError   { get; private set; }

    public void Refresh()
    {
        try
        {
            // Index installed mods by directory (the immutable id) and by display name. The name map
            // mirrors how Glamourer itself resolves associations (Interop/Penumbra/ModUsageInformer.
            // ModMatches): match on directory first, then on name. Names are not unique, so the first
            // installed mod with a given name wins the fallback.
            var byDir  = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);
            var byName = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var (dir, name) in penumbra.GetMods())
            {
                var mod = new ModEntry(dir, name, []) { Path = penumbra.GetModFullPath(dir, name) };
                byDir[dir] = mod;
                byName.TryAdd(name, mod);
            }

            var designs = glamourer.GetDesigns();
            DesignCount = designs.Count;

            foreach (var (id, designName) in designs)
            {
                if (glamourer.GetDesign(id)?["Mods"] is not JArray mods)
                    continue;

                foreach (var entry in mods.OfType<JObject>())
                {
                    var dir     = entry.Value<string>("Directory") ?? string.Empty;
                    var modName = entry.Value<string>("Name") ?? string.Empty;
                    if (dir.Length == 0 && modName.Length == 0)
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

                    // Match by directory, then fall back to name, exactly as Glamourer does.
                    ModEntry? mod = null;
                    if (dir.Length > 0)
                        byDir.TryGetValue(dir, out mod);
                    if (mod is null && modName.Length > 0)
                        byName.TryGetValue(modName, out mod);

                    if (mod is null)
                    {
                        // Referenced mod is not installed under this directory or name; surface it
                        // instead of dropping it, deduped by directory (or name when no directory).
                        var key = dir.Length > 0 ? dir : modName;
                        if (!byDir.TryGetValue(key, out mod))
                        {
                            mod = new ModEntry(key, modName.Length > 0 ? modName : dir, []) { Unknown = true };
                            byDir[key] = mod;
                        }
                    }
                    else
                    {
                        // Matched an installed mod, but flag it if the design stored a stale id
                        // (matched on one of directory/name while the other differs).
                        var dirMismatch  = dir.Length > 0     && !string.Equals(dir, mod.Directory, StringComparison.OrdinalIgnoreCase);
                        var nameMismatch = modName.Length > 0 && !string.Equals(modName, mod.Name, StringComparison.OrdinalIgnoreCase);
                        if (dirMismatch || nameMismatch)
                            mod.HasMismatch = true;
                    }

                    mod.Designs.Add(assoc);
                }
            }

            Mods        = byDir.Values.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
            Root        = ModTreeBuilder.Build(Mods);
            LastError   = null;
            LastRefresh = DateTime.Now;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }
}
