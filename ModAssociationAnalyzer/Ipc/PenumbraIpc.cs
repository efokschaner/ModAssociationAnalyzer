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
    private readonly ApiVersion     _apiVersion;
    private readonly GetModList     _getModList;
    private readonly OpenMainWindow _openMainWindow;

    public PenumbraIpc(IDalamudPluginInterface pi)
    {
        _apiVersion     = new ApiVersion(pi);
        _getModList     = new GetModList(pi);
        _openMainWindow = new OpenMainWindow(pi);
    }

    private bool _available;
    private long _nextCheckTick;

    /// <summary>
    /// Whether Penumbra is installed and its IPC is responding. Cached and re-checked at most
    /// once per second so it can be read every frame without an IPC round-trip per tick.
    /// </summary>
    public bool Available
    {
        get
        {
            var now = Environment.TickCount64;
            if (now < _nextCheckTick)
                return _available;

            _nextCheckTick = now + 1000;
            try
            {
                var (breaking, _) = _apiVersion.Invoke();
                _available = breaking > 0;
            }
            catch
            {
                _available = false;
            }

            return _available;
        }
    }

    /// <summary> Maps mod directory name (immutable id) to current display name. </summary>
    public IReadOnlyDictionary<string, string> GetMods()
    {
        try
        {
            return _getModList.Invoke();
        }
        catch
        {
            return new Dictionary<string, string>();
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
