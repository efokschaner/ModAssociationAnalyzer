using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ModAssociationAnalyzer.Ipc;
using ModAssociationAnalyzer.Windows;

namespace ModAssociationAnalyzer;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager        CommandManager  { get; private set; } = null!;
    [PluginService] internal static IPluginLog             Log             { get; private set; } = null!;

    private const string CommandName = "/modassoc";

    public readonly WindowSystem WindowSystem = new("ModAssociationAnalyzer");

    private readonly PenumbraIpc         _penumbra;
    private readonly GlamourerIpc        _glamourer;
    private readonly ModAssociationIndex _index;
    private readonly MainWindow          _mainWindow;

    public Plugin()
    {
        _penumbra   = new PenumbraIpc(PluginInterface);
        _glamourer  = new GlamourerIpc(PluginInterface);
        _index      = new ModAssociationIndex(_penumbra, _glamourer);
        _mainWindow = new MainWindow(_index, _penumbra, _glamourer);

        WindowSystem.AddWindow(_mainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Mod Association Analyzer window.",
        });

        PluginInterface.UiBuilder.Draw       += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        // Dev convenience: when iterating on a dev plugin, a rebuild makes Dalamud reload us.
        // Re-open the window automatically on that reload so we don't need /modassoc each build.
        // Scoped to dev plugins so released installs never auto-pop the window.
        if (PluginInterface.IsDev && PluginInterface.Reason == PluginLoadReason.Reload)
            _mainWindow.IsOpen = true;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw       -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
        => ToggleMainUi();

    private void ToggleMainUi()
        => _mainWindow.Toggle();
}
