namespace ModAssociationAnalyzer;

/// <summary> A mod sitting at a leaf of the folder tree, with the name shown in the tree. </summary>
public sealed class ModLeaf(ModEntry mod, string displayName)
{
    public ModEntry Mod         { get; } = mod;
    public string   DisplayName { get; } = displayName;
}

/// <summary> A folder in the reconstructed Penumbra mod tree. </summary>
public sealed class FolderNode(string name, string fullKey)
{
    public string Name    { get; } = name;
    /// <summary> Path from the root, used only as a stable ImGui id. </summary>
    public string FullKey { get; } = fullKey;

    public SortedDictionary<string, FolderNode> Folders { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ModLeaf>                         Mods    { get; } = [];

    public FolderNode GetOrAddFolder(string childName)
    {
        if (Folders.TryGetValue(childName, out var node))
            return node;

        node = new FolderNode(childName, FullKey.Length == 0 ? childName : $"{FullKey}/{childName}");
        Folders[childName] = node;
        return node;
    }
}

/// <summary>
/// Rebuilds the Penumbra mod-tree structure from each mod's full path
/// (as reported by the GetModPath IPC). Mirrors how Penumbra's ModFileSystem
/// organises mods into folders, but without the Luna FileSystem framework.
/// </summary>
public static class ModTreeBuilder
{
    public const string NotInstalledFolder = "(Not installed)";

    public static FolderNode Build(IEnumerable<ModEntry> mods)
    {
        var root = new FolderNode(string.Empty, string.Empty);

        foreach (var mod in mods)
        {
            FolderNode folder;
            string     leafName;

            if (mod.Unknown)
            {
                // Referenced by a design but not in the current Penumbra list; group together.
                folder   = root.GetOrAddFolder(NotInstalledFolder);
                leafName = mod.Name;
            }
            else
            {
                var segments = mod.Path.Split('/',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                folder = root;
                for (var i = 0; i < segments.Length - 1; ++i)
                    folder = folder.GetOrAddFolder(segments[i]);

                leafName = segments.Length > 0 ? segments[^1] : mod.Name;
            }

            folder.Mods.Add(new ModLeaf(mod, leafName));
        }

        return root;
    }
}
