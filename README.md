# Mod Association Analyzer

A small Dalamud plugin that answers one question: **which Glamourer designs reference a
given Penumbra mod?**

Penumbra can already show which *collections* use a mod (the "Collections" tab under each
mod). Glamourer's designs have a "Mod Associations" feature, but there is no built-in way to
look at it from the mod's perspective. This plugin fills that gap.

It is **read-only** and takes **no source dependency on either plugin** — it only reads data
over their public IPC and asks them to focus a mod/design page. Nothing in Penumbra or
Glamourer is modified, which is why it can ship and be tested independently of either.

## How it works

- **Penumbra IPC** — `GetModList` enumerates installed mods (`directory -> name`);
  `GetModPath` returns each mod's full path in Penumbra's folder tree (e.g.
  `Outfits/Bikinis/Cool Mod`), which we use to reconstruct the same folder tree shown in
  Penumbra's Mods tab; `OpenMainWindow(Mods, directory, name)` jumps to a mod.
- **Glamourer IPC** — `GetDesignList` enumerates designs (`guid -> name`);
  `GetDesignJObject(guid)` returns the design's serialized JSON, whose top-level `"Mods"`
  array is exactly the Mod Associations data (directory, name, enabled/inherit/remove,
  priority); `OpenDesign(guid)` jumps to a design.

`ModAssociationIndex` builds a reverse index of designs per mod and reconstructs Penumbra's
folder tree from the per-mod full paths. Associations are matched to installed mods the same
way Glamourer itself does (`ModUsageInformer.ModMatches`): by **directory** first, then
falling back to **display name**. The left pane mirrors Penumbra's Mods tab — a filterable
folder tree (the filter input has a usage-filter dropdown on its right; filtering
auto-expands matching folders; right-click for Expand / Collapse All) — with each mod showing
its design-reference count and the name truncating before the count when space is tight.

> Note: Penumbra's own tree is built on the Luna `FileSystem<T>` / `FileSystemDrawer<T>`
> framework (with ImSharp), which can't be lifted out standalone, so the tree here is a
> minimal re-implementation over Dalamud's ImGui bindings fed by the IPC paths.

### Status indicators

- A mod referenced by a design but **not installed** in Penumbra is grouped under a red
  **"(Not installed)"** folder and marked with a red info pip.
- A mod whose association matched by directory **or** name while the *other* field differs
  (the design stored a stale id) is marked with a **yellow** info pip, in both the tree and
  the detail pane. Glamourer still applies such associations via the matching field, so they
  work in-game — the pip is just a heads-up that the design references it by an outdated id.

The typed IPC contracts come from the published [`Penumbra.Api`] and [`Glamourer.Api`]
NuGet packages — the same packages the host plugins publish for exactly this purpose.

## Known limitations (by design, for now)

- It is a **separate window** (open with `/modassoc`), not a tab embedded under each mod —
  no public IPC exposes Penumbra's current mod selection.
- **Refresh is on-demand** — Glamourer exposes no "design edited" event, so the view refreshes
  when the window is opened and via the Refresh button in the footer. After editing
  associations in Glamourer, reopen the window or hit Refresh.
- Cannot show whether a design is in an active **automation set** (not exposed via IPC).
- Needs both Penumbra and Glamourer installed; if either is unavailable the affected data is
  simply empty and a notice is shown.

If this proves useful, the index-building logic can later be promoted into a real Penumbra
"Designs" tab (which would require coordinated changes to Penumbra/Glamourer or a new
generic IPC).

## Building

Requires the XIVLauncher/Dalamud dev environment (the `Dalamud.NET.Sdk` resolves the local
Dalamud libraries). From the repo root:

```pwsh
dotnet build -c Release
```

The built `ModAssociationAnalyzer.dll` (plus its manifest and the two API dlls) lands in
`ModAssociationAnalyzer/bin/Release/`. In Dalamud, add the **`ModAssociationAnalyzer.dll`
file itself** — not the containing folder — under `/xlsettings` → Experimental → Dev Plugin
Locations, then enable the plugin in `/xlplugins`.

When loaded as a dev plugin, a rebuild makes Dalamud reload the plugin and the window
re-opens automatically, so the iterate-on-UI loop doesn't need a `/modassoc` each time.

[`Penumbra.Api`]: https://www.nuget.org/packages/Penumbra.Api
[`Glamourer.Api`]: https://www.nuget.org/packages/Glamourer.Api
