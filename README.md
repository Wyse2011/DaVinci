# DaVinci

Client-side, visual-only Dalamud plugin for recoloring housing furniture.
Nothing in this project changes server-side housing data or sends requests to the game server.

Current stage: **Phase 0 – build and probe.**

## Layout

```
DaVinci/
  Plugin.cs                         Entry point, /davinci, house enter/leave detection (polled every 500 ms)
  Housing/HouseKey.cs               Wraps the game's 64-bit HouseId (world, district, ward, plot/apartment, room)
  Housing/FurnitureKey.cs           Stable per-placement id: furniture id + quantized position/rotation
  Housing/HousingScanner.cs         Current house, selected furnishing, full room scan
  Materials/FurnitureWalker.cs      SharedGroup -> BgPart -> BgObject walk, shared by everything below
  Materials/MaterialInspector.cs    Snapshot of models, materials, shaders, color tables
  Materials/StainTester.cs          Experiment: any RGB through the game's per-item stain slot
  Permissions/PermissionProbe.cs    HasHousePermissions, housing-functions state, owned/shared estates
  Ownership/SignboardWatcher.cs     Passive capture of estate placard data (owner, FC tag, greeting)
  Diagnostics/ProbeReport.cs        Copyable text report (names redacted by default)
  Model/Overrides.cs, OverrideStore.cs   Override data model and per-house JSON storage
  Windows/MainWindow.cs             Tabs: Selection, Permissions, Ownership, Room, Report
  Windows/PaintWindow.cs            Paint popup (saves overrides; rendering comes in Phase 1)
```

## Building and loading

Every push to `main` is compiled by GitHub Actions (see the Actions tab); the built plugin is
attached to each run as a downloadable artifact.

Locally:

1. Open `DaVinci.slnx` in Visual Studio 2022 (17.13+) or Rider, with the .NET 10 SDK installed.
2. Build. Output lands in `DaVinci/bin/x64/Debug/`.
3. In game: `/xlsettings` -> Experimental -> Dev Plugin Locations -> add the path to `DaVinci.dll`.
4. `/xlplugins` -> Dev Tools -> enable DaVinci. Open with `/davinci`. Logs are in `/xllog`.

If it doesn't compile, the likeliest spots are Dalamud API differences in ImRaii helpers or hook
signatures; paste the errors back and they can be fixed quickly.

## Phase 0 test checklist

Do these in a house you can decorate, then repeat the marked ones as a visitor in a friend's house.
After each test, use the Report tab to copy a report.

**A. Basics**
1. Does the window show the correct ward, plot, and room?
2. Room tab -> Scan room: does the count roughly match your item count? Do names resolve?
3. Leave and re-enter: does a selected item keep the same Key?

**B. Materials** (select 3–4 different items, including one dyeable and one not)
4. Which shaders appear (bg.shpk, bgcolorchange.shpk, others)?
5. Do materials have color tables? What sizes?
6. Select two copies of the same item: is the material Handle address identical?

**C. Stain color test**
7. Apply a test color to a dyeable item. Does it change on screen?
8. Try it on an undyed dyeable item, and on a non-dyeable item. What happens?
9. Does the color survive: moving the camera away, rotating the item, exiting edit mode, a few minutes passing?
10. Apply to several copies of the same item with different colors: does each keep its own color?
11. Restore: does the original come back?

**D. Permissions** (Permissions tab)
12. In your own house: what do the YES/NO flags show, inside and outside edit mode?
13. In an FC house, private chambers, apartment (any you have access to): what Relationship is shown?
14. *As a visitor* in a house you can't decorate: are both flags NO, and is Paint disabled?

**E. Ownership** (Ownership tab)
15. Enter a house: does placard data appear by itself, or only after viewing the placard outside first?
16. Does it show for apartments and private chambers?
17. *As a visitor*: same questions.

These answers decide how Phase 1 rendering is built and how visitor-side owner verification works.
