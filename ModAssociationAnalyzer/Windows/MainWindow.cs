using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ModAssociationAnalyzer.Ipc;

namespace ModAssociationAnalyzer.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private static readonly Vector4 Warning    = new(1.00f, 0.65f, 0.20f, 1f);
    private static readonly Vector4 Error      = new(1.00f, 0.35f, 0.35f, 1f);
    private static readonly Vector4 Muted      = new(0.65f, 0.65f, 0.65f, 1f);
    private static readonly Vector4 FolderColor = new(0.86f, 0.70f, 0.20f, 1f);

    private readonly ModAssociationIndex _index;
    private readonly PenumbraIpc         _penumbra;
    private readonly GlamourerIpc        _glamourer;

    private string _filter           = string.Empty;
    private bool   _onlyWithDesigns   = true;
    private string? _selectedDir;

    public MainWindow(ModAssociationIndex index, PenumbraIpc penumbra, GlamourerIpc glamourer)
        : base("Mod Association Analyzer###ModAssociationAnalyzerMain")
    {
        _index     = index;
        _penumbra  = penumbra;
        _glamourer = glamourer;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    { }

    public override void OnOpen()
    {
        if (_index.LastRefresh == default)
            _index.Refresh();
    }

    public override void Draw()
    {
        DrawToolbar();
        ImGui.Separator();

        if (!_penumbra.Available)
            ImGui.TextColored(Warning, "Penumbra is not available - the mod list will be empty.");
        if (!_glamourer.Available)
            ImGui.TextColored(Warning, "Glamourer is not available - no designs can be read.");
        if (_index.LastError is { } err)
            ImGui.TextColored(Error, $"Error during refresh: {err}");

        DrawSplit();
    }

    private void DrawToolbar()
    {
        if (ImGui.Button("Refresh"))
            _index.Refresh();

        ImGui.SameLine();
        if (_index.LastRefresh == default)
            ImGui.TextColored(Muted, "Not loaded yet.");
        else
            ImGui.TextColored(Muted,
                $"{_index.DesignCount} designs scanned, last refreshed {_index.LastRefresh:HH:mm:ss}.");

        ImGui.SameLine();
        ImGui.Checkbox("Only mods used by a design", ref _onlyWithDesigns);

        ImGui.SetNextItemWidth(220 * ImGuiHelpers.GlobalScale);
        ImGui.InputTextWithHint("##filter", "Filter mods...", ref _filter, 256);
    }

    private void DrawSplit()
    {
        var leftWidth = MathF.Max(200, ImGui.GetContentRegionAvail().X * 0.40f);

        using (var left = Child("##mods", new Vector2(leftWidth, 0), true))
        {
            if (left)
                DrawModList();
        }

        ImGui.SameLine();

        using (var right = Child("##designs", new Vector2(0, 0), true))
        {
            if (right)
                DrawDesignsForSelection();
        }
    }

    private void DrawModList()
        => DrawFolder(_index.Root);

    private void DrawFolder(FolderNode folder)
    {
        foreach (var sub in folder.Folders.Values)
        {
            if (!FolderVisible(sub))
                continue;

            // While filtering, keep matching folders open so hits are visible without manual expanding.
            if (_filter.Length > 0)
                ImGui.SetNextItemOpen(true, ImGuiCond.Always);

            ImGui.PushStyleColor(ImGuiCol.Text, FolderColor);
            var expanded = ImGui.TreeNodeEx($"{sub.Name}###f:{sub.FullKey}", ImGuiTreeNodeFlags.SpanAvailWidth);
            ImGui.PopStyleColor();

            if (expanded)
            {
                DrawFolder(sub);
                ImGui.TreePop();
            }
        }

        foreach (var leaf in folder.Mods)
        {
            if (LeafVisible(leaf))
                DrawLeaf(leaf);
        }
    }

    private void DrawLeaf(ModLeaf leaf)
    {
        var flags = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (leaf.Mod.Directory == _selectedDir)
            flags |= ImGuiTreeNodeFlags.Selected;

        ImGui.TreeNodeEx($"{leaf.DisplayName} ({leaf.Mod.Designs.Count})###m:{leaf.Mod.Directory}", flags);
        if (ImGui.IsItemClicked())
            _selectedDir = leaf.Mod.Directory;

        if (leaf.Mod.Unknown && ImGui.IsItemHovered())
            ImGui.SetTooltip($"Referenced by a design but not in your current Penumbra mod list.\nDirectory: {leaf.Mod.Directory}");
    }

    private bool LeafVisible(ModLeaf leaf)
    {
        if (_onlyWithDesigns && leaf.Mod.Designs.Count == 0)
            return false;
        if (_filter.Length == 0)
            return true;

        return leaf.DisplayName.Contains(_filter, StringComparison.OrdinalIgnoreCase)
         || leaf.Mod.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)
         || leaf.Mod.Path.Contains(_filter, StringComparison.OrdinalIgnoreCase);
    }

    private bool FolderVisible(FolderNode folder)
        => folder.Mods.Any(LeafVisible) || folder.Folders.Values.Any(FolderVisible);

    private void DrawDesignsForSelection()
    {
        var mod = _index.Mods.FirstOrDefault(m => m.Directory == _selectedDir);
        if (mod is null)
        {
            ImGui.TextColored(Muted, "Select a mod on the left to see the designs that reference it.");
            return;
        }

        ImGui.TextUnformatted(mod.Name);
        ImGui.TextColored(Muted, mod.Directory);
        if (ImGui.Button("Open in Penumbra"))
            _penumbra.OpenMod(mod.Directory, mod.Name);
        ImGui.Separator();

        if (mod.Designs.Count == 0)
        {
            ImGui.TextColored(Muted, "No Glamourer designs reference this mod.");
            return;
        }

        using var table = Table("##designTable", 3);
        if (!table)
            return;

        ImGui.TableSetupColumn("Design", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("State",  ImGuiTableColumnFlags.WidthFixed, 70 * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("Priority", ImGuiTableColumnFlags.WidthFixed, 60 * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();

        foreach (var design in mod.Designs.OrderBy(d => d.DesignName, StringComparer.OrdinalIgnoreCase))
        {
            ImGui.TableNextColumn();
            if (ImGui.Selectable($"{design.DesignName}###{design.DesignId}", false, ImGuiSelectableFlags.SpanAllColumns))
                _glamourer.OpenDesignPage(design.DesignId);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Click to open this design in Glamourer.");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(design.StateLabel);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(design.Enabled ? design.Priority.ToString() : "-");
        }
    }

    // --- small RAII-style helpers so we don't leak ImGui begin/end pairs ---

    private static ChildScope Child(string id, Vector2 size, bool border)
        => new(ImGui.BeginChild(id, size, border, ImGuiWindowFlags.None));

    private static TableScope Table(string id, int columns)
        => new(ImGui.BeginTable(id, columns, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH));

    private readonly struct ChildScope(bool open) : IDisposable
    {
        public static implicit operator bool(ChildScope s) => s._open;
        private readonly bool _open = open;
        public void Dispose() => ImGui.EndChild();
    }

    private readonly struct TableScope(bool open) : IDisposable
    {
        private readonly bool _open = open;
        public static implicit operator bool(TableScope s) => s._open;
        public void Dispose()
        {
            if (_open)
                ImGui.EndTable();
        }
    }
}
