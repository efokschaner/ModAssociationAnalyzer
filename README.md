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

`ModAssociationIndex` builds a reverse index keyed on the mod **directory name** (the
immutable id Glamourer also matches on), so renames don't break the mapping, and
reconstructs Penumbra's folder tree from the per-mod full paths. The left pane mirrors
Penumbra's Mods tab: a filterable folder tree (filtering auto-expands matching folders),
with each mod showing its design-reference count. Designs that reference an
uninstalled/renamed mod are still surfaced under a "(Not installed)" folder.

> Note: Penumbra's own tree is built on the Luna `FileSystem<T>` / `FileSystemDrawer<T>`
> framework (with ImSharp), which can't be lifted out standalone, so the tree here is a
> minimal re-implementation over Dalamud's ImGui bindings fed by the IPC paths.

The typed IPC contracts come from the published [`Penumbra.Api`] and [`Glamourer.Api`]
NuGet packages — the same packages the host plugins publish for exactly this purpose.

## Known limitations (by design, for now)

- It is a **separate window** (open with `/modassoc`), not a tab embedded under each mod —
  no public IPC exposes Penumbra's current mod selection.
- **Manual refresh** — Glamourer exposes no "design edited" event, so use the Refresh button
  after editing associations.
- Cannot show whether a design is in an active **automation set** (not exposed via IPC).

If this proves useful, the index-building logic can later be promoted into a real Penumbra
"Designs" tab (which would require coordinated changes to Penumbra/Glamourer or a new
generic IPC).

## Building

Requires the XIVLauncher/Dalamud dev environment (the `Dalamud.NET.Sdk` resolves the local
Dalamud libraries). From the repo root:

```pwsh
dotnet build -c Release
```

The built `ModAssociationAnalyzer.dll` (plus manifest) lands in
`ModAssociationAnalyzer/bin/Release/`. Add that folder as a **dev plugin** in Dalamud
(`/xlsettings` → Experimental → Dev Plugin Locations), then enable it in `/xlplugins`.

[`Penumbra.Api`]: https://www.nuget.org/packages/Penumbra.Api
[`Glamourer.Api`]: https://www.nuget.org/packages/Glamourer.Api
