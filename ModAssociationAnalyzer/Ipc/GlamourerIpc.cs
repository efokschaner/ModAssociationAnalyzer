using Dalamud.Plugin;
using Glamourer.Api.IpcSubscribers;
using Newtonsoft.Json.Linq;

namespace ModAssociationAnalyzer.Ipc;

/// <summary>
/// Thin wrapper over the public Glamourer IPC we need. We enumerate designs and read each
/// design's serialized JSON (whose "Mods" array is the Mod Associations data), and we can
/// ask Glamourer to focus a design. We never mutate Glamourer state.
/// </summary>
public sealed class GlamourerIpc
{
    private readonly ApiVersion       _apiVersion;
    private readonly GetDesignList    _getDesignList;
    private readonly GetDesignJObject _getDesignJObject;
    private readonly OpenDesign       _openDesign;

    public GlamourerIpc(IDalamudPluginInterface pi)
    {
        _apiVersion       = new ApiVersion(pi);
        _getDesignList    = new GetDesignList(pi);
        _getDesignJObject = new GetDesignJObject(pi);
        _openDesign       = new OpenDesign(pi);
    }

    /// <summary> Whether Glamourer is installed and its IPC is responding. </summary>
    public bool Available
    {
        get
        {
            try
            {
                var (major, _) = _apiVersion.Invoke();
                return major > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary> Maps design id to current display name. </summary>
    public IReadOnlyDictionary<Guid, string> GetDesigns()
    {
        try
        {
            return _getDesignList.Invoke();
        }
        catch
        {
            return new Dictionary<Guid, string>();
        }
    }

    /// <summary> Full serialized design JSON, or null if unavailable. The "Mods" array holds associations. </summary>
    public JObject? GetDesign(Guid id)
    {
        try
        {
            return _getDesignJObject.Invoke(id);
        }
        catch
        {
            return null;
        }
    }

    /// <summary> Open Glamourer focused on the given design. </summary>
    public void OpenDesignPage(Guid id)
    {
        try
        {
            _openDesign.Invoke(id);
        }
        catch
        {
            // Glamourer unavailable; nothing actionable here.
        }
    }
}
