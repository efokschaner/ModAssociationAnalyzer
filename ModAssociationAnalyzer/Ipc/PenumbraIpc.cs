using Dalamud.Plugin;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;

namespace ModAssociationAnalyzer.Ipc;

/// <summary>
/// Thin wrapper over the public Penumbra IPC we need. We only read the mod list and
/// ask Penumbra to focus a mod page; we never mutate Penumbra state.
/// </summary>
public sealed class PenumbraIpc
{
    private readonly GetModList     _getModList;
    private readonly GetModPath     _getModPath;
    private readonly OpenMainWindow _openMainWindow;

    public PenumbraIpc(IDalamudPluginInterface pi)
    {
        _getModList     = new GetModList(pi);
        _getModPath     = new GetModPath(pi);
        _openMainWindow = new OpenMainWindow(pi);
    }

    /// <summary>
    /// Whether Penumbra was loaded and responding the last time we read from it. There is no
    /// separate liveness probe: the act of reading data is the check (an IPC call against an
    /// unloaded plugin throws), so this reflects the result of the most recent <see cref="GetMods"/>.
    /// </summary>
    public bool Available { get; private set; }

    /// <summary> Maps mod directory name (immutable id) to current display name. </summary>
    public IReadOnlyDictionary<string, string> GetMods()
    {
        try
        {
            var mods  = _getModList.Invoke();
            Available = true;
            return mods;
        }
        catch
        {
            Available = false;
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// The mod's full path in Penumbra's mod folder tree (e.g. "Outfits/Bikinis/Cool Mod").
    /// Falls back to the bare mod name if Penumbra has no custom sort path or is unavailable.
    /// </summary>
    public string GetModFullPath(string directory, string name)
    {
        try
        {
            var (ec, path, _, _) = _getModPath.Invoke(directory, name);
            return ec == PenumbraApiEc.Success && path.Length > 0 ? path : name;
        }
        catch
        {
            return name;
        }
    }

    /// <summary> Open Penumbra's main window focused on the given mod. </summary>
    public void OpenMod(string directory, string name)
    {
        try
        {
            _openMainWindow.Invoke(TabType.Mods, directory, name);
        }
        catch
        {
            // Penumbra unavailable or mod missing; nothing actionable here.
        }
    }
}
