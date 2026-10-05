# UFO's Cheat Mods Bundle for Bannerlord v1.5.3

This branch maintains UFOdestiny's bundle for **Bannerlord v1.5.3 only**. Mod version: **1.0.19**. Earlier versions, including v1.5.0–v1.5.2, and future game versions are unsupported by this release. Historical metadata checks passed, but historical executable IL and gameplay were not verified; they do not justify a broader public compatibility claim.

Two separate editions are produced from the same source:

| Edition | Module ID | DLC requirement |
| --- | --- | --- |
| War Sails | `UFO15` | War Sails v1.3.3 with Bannerlord v1.5.3 |
| No War Sails | `UFONoWarSails15` | No NavalDLC module or assembly dependency; naval settings and code omitted |

Enable only one UFO edition. The new module IDs preserve the existing 1.4.8 packages and Workshop items. Existing saves can show a module mismatch when changing editions; keep a backup before migrating a save. MCM settings IDs and the stored campaign behavior identity remain unchanged.

## Dependencies

Use game-compatible releases of Harmony, ButterLib, UIExtenderEx and Mod Configuration Menu v5. Compilation pins Harmony 2.4.2, ButterLib 2.11.1, UIExtenderEx 2.13.2 and MCM 5.12.3. The current installation provides ButterLib 2.12.0; that version is not published as a NuGet package, so the published 2.11.1 API is the build baseline.

Both Workshop items declare [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2859188632), [UIExtenderEx](https://steamcommunity.com/sharedfiles/filedetails/?id=2859222409), [ButterLib](https://steamcommunity.com/sharedfiles/filedetails/?id=2859232415), and [Mod Configuration Menu v5](https://steamcommunity.com/sharedfiles/filedetails/?id=2859238197) as required items. The War Sails edition also declares the [War Sails DLC](https://store.steampowered.com/app/2927200/); the no-War-Sails edition has no DLC requirement.

## Build and package

The module targets .NET Framework 4.8 and C# 12. Build with a compatible .NET SDK; the repository's net10.0 audit tools require a .NET 10 SDK. Framework reference assemblies are restored from the exact `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3 package, so a machine-wide Framework targeting pack is unnecessary.

```powershell
./Tools/Release/Build-Release.ps1 -Edition WarSails
./Tools/Release/Build-Release.ps1 -Edition NoWarSails
```

Use `-DotNetPath C:\path\dotnet.exe` or the `UFO_DOTNET_PATH` environment variable for a portable SDK. `-ReferenceRoot C:\path\reference-snapshot` accepts a game-shaped v1.5.3 directory containing one copy of each managed reference DLL under `bin/Win64_Shipping_Client` and `Modules/<module>/bin/Win64_Shipping_Client`. Without it, explicitly pinned component NuGet packages supply the `1.5.3.122374-beta` references. The aggregate all-modules reference package is deliberately avoided because it includes NavalDLC.

Both commands stage the complete assets, ModuleData, videos, localization and newly compiled `UFO.dll` under `artifacts/packages/<module-id>`. Each package contains only the selected edition's DLL; game and dependency DLLs are excluded. `UFO-1.0.19-Bannerlord-1.5.3-{Edition}.zip` archives and SHA256 sidecars are written under `artifacts/`. Intermediate and output directories are separate for each edition. Normal builds do not deploy or replace the game's module directories.

Use `-OutputRoot` to choose a staging directory. Local deployment is explicit:

```powershell
./Tools/Release/Build-Release.ps1 -Edition WarSails -GameRoot 'C:\path\Mount & Blade II Bannerlord' -Deploy
```

This copies the staged package only to its new module folder and requires Bannerlord v1.5.3, plus NavalDLC v1.3.3 for War Sails. Existing UFO edition folders are retained. Disable other UFO editions in the launcher before testing.

## Compatibility checks and testing limits

The public release supports this exact game/DLC pair:

| Bannerlord | Reference package version | Steam build | Paired War Sails |
| --- | --- | --- | --- |
| v1.5.3 | `1.5.3.122374-beta` | 25302170 | v1.3.3 |

`Tools/HarmonyApiAudit` checks declared Harmony targets, parameter bindings, direct members, reflected members and optional naval targets. All eight static edition/reference audits passed across `1.5.1.120547-beta`, both v1.5.2 reference builds, and `1.5.3.122374-beta`; the seven auditor self-tests and 200 maintained behavior regression checks per edition also passed. Live v1.5.3 IL inspection validated nine War Sails implementation contracts and one no-War-Sails healing contract. These results are evidence for the release work, not support for earlier game versions. Metadata reference assemblies have no executable game code; historical IL was not verified and historical versions are excluded.

`Tools/ModuleDataAudit/Validate-ModuleData.ps1` validates XML registrations and content references against a game snapshot. The final source rebuild and verification results are recorded in the release notes.

Both final editions compiled against the v1.5.3 baseline with zero warnings and errors, passed their final checks and were installed in their new module folders. Both reached the v1.5.3 main menu, and game logs showed no UFO diagnostic errors. The no-War-Sails launch omitted NavalDLC. The publishing user confirmed successful playtesting on **2026-10-04**, satisfying the campaign runtime gate and authorizing publication. Both editions were published that day. Fresh Steam downloads matched all 26 files per edition by relative path, size and SHA256; public metadata, dependencies and anonymous item pages were also verified. Outcomes and unchanged DLL/archive hashes are recorded in `RELEASE-NOTES-1.0.19.md` and `Tools/Release/Verification-1.0.19.json`. This user-confirmed playtest does not establish exhaustive gameplay coverage or compatibility with historical versions.

Primary version provenance: [BUTR reference packages](https://www.nuget.org/packages/Bannerlord.ReferenceAssemblies), [client build registry](https://github.com/BUTR/Bannerlord.ReferenceAssemblies/blob/master/builds/261550.json), and [TaleWorlds beta hotfix notes](https://steamcommunity.com/app/261550/discussions/0/3762228679799915128/).

## Steam Workshop release

The two **Bannerlord v1.5.3** items were published on **2026-10-04**. Their public visibility and complete downloaded payloads are verified:

| Edition | Workshop item | Current visibility |
| --- | --- | --- |
| War Sails | [3813768338](https://steamcommunity.com/sharedfiles/filedetails/?id=3813768338) | Public |
| No War Sails | [3813768472](https://steamcommunity.com/sharedfiles/filedetails/?id=3813768472) | Public |

Use the corresponding `WorkshopUpdate{WarSails,NoWarSails}15.xml` template to update these existing items. Source templates retain private visibility as a safe default; future intended public updates must explicitly use `-Visibility Public`. The create templates are retained as records of initial creation; rerunning them would create duplicates. Older descriptors target older releases and must not be used for this publication.

```powershell
./Tools/Release/Prepare-Workshop.ps1 -Edition WarSails -ModuleRoot './artifacts/packages/UFO15' -ItemId 3813768338 -Visibility Public
./Tools/Release/Prepare-Workshop.ps1 -Edition NoWarSails -ModuleRoot './artifacts/packages/UFONoWarSails15' -ItemId 3813768472 -Visibility Public
```

This prepares update descriptors only. For a future release, complete its checks before uploading through Bannerlord's official `TaleWorlds.MountAndBlade.SteamWorkshop.exe`. `Prepare-Workshop.ps1` rejects original and previous UFO item IDs. The uploader requires a signed-in owning Steam account and Steam Cloud enabled. Verify metadata and downloaded content after uploading: the uploader can finish an upload before failing during redirected-console shutdown.

The 1.5 descriptors use `SteamWorkshop/image15.png`; descriptor preparation requires that preview to exist and be smaller than 1 MB. Keep the new edition title, supported versions, dependencies, testing status and credits in the item's description. [Official publishing documentation](https://moddocs.bannerlord.com/steam-workshop/uploading_updating_mod/)

Run the uploader from the game's `bin/Win64_Shipping_Client` directory with the prepared descriptor as its sole argument. With the current installation:

```powershell
Set-Location -LiteralPath 'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client'
& './TaleWorlds.MountAndBlade.SteamWorkshop.exe' 'C:\UFO-Bannerlord\artifacts\workshop\UpdateWarSails15.xml'
& './TaleWorlds.MountAndBlade.SteamWorkshop.exe' 'C:\UFO-Bannerlord\artifacts\workshop\UpdateNoWarSails15.xml'
```

Keep Steam running and signed in to the publishing account. Check each item's title, intended visibility, preview, description, supported-version tag and required dependencies. Request a low-priority Steam UGC `DownloadItem(itemId, false)` without subscribing, wait for installation to complete, then compare the full relative file tree, every file's size and SHA256 with the staged edition, including missing or extra files. The v1.0.19 downloads matched all 26 files for each edition with zero differences. The staged local packages and downloaded packages are byte-identical, so no second campaign playtest is needed solely for redistribution. Verify public metadata and the anonymous public pages after a public update. Local reports under `artifacts/workshop/` are ignored build artifacts; their key facts are summarized in tracked `Tools/Release/Verification-1.0.19.json`.

## Maintained behavior

The 1.5 editions retain the maintained perk save/reload recovery, explicit campaign-setting precedence and guarded character/inventory/party hotkeys. New compatibility adapters address changed 1.5 APIs rather than blindly reusing 1.4.8 reflection targets. Optional NavalDLC integration is compiled only into the War Sails edition.

Ship actions use hull IDs from `ship_hulls.xml`, figurehead prefab IDs and upgrade-piece IDs from `ship_upgrade_pieces.xml`. All-ships excludes story/quest hulls; all-upgrades selects the highest-value normal piece for each slot.

## Project structure

- `Bootstrap/`, `Behaviors/`: module lifecycle and campaign behaviors.
- `Patches/`, `Patching/`: game patches, registration and naval compatibility.
- `Settings/`, `Localization/`, `Diagnostics/`: settings, translations and failure reporting.
- `Extensions/`, `Infrastructure/`, `Models/`: shared helpers and model replacements.
- `Module/`: packaged content and default War Sails descriptor.
- `Tools/`: isolated audits, regression checks, release packaging and edition manifests.

## Credits

All original bundle work belongs to UFOdestiny and the original authors. This maintained fork does not claim authorship of their mods.

- [Bannerlord Cheats Reload](https://www.nexusmods.com/mountandblade2bannerlord/mods/6446)
- [Xorberax's Legacy](https://www.nexusmods.com/mountandblade2bannerlord/mods/3462)
- [Hero Enhancement](https://www.nexusmods.com/mountandblade2bannerlord/mods/4827)
- [Recruit Exile Clans](https://steamcommunity.com/sharedfiles/filedetails/?id=3255329103)
- [Keep Your Daughters](https://www.nexusmods.com/mountandblade2bannerlord/mods/5148)
- [Super Throwing Collection](https://steamcommunity.com/sharedfiles/filedetails/?id=2885230883)
- [loongspear](https://steamcommunity.com/sharedfiles/filedetails/?id=3017866291)
- [Super OP Arrows](https://bbs.mountblade.com.cn/download_1580.html)

Original translation credits: Russian by [MaG3ro](https://steamcommunity.com/id/MaG3ro) and Portuguese by Kyo. Language resources already present on this branch are included; translation completeness and accuracy are not newly certified by the compatibility audit.

Original Workshop: [3583201039](https://steamcommunity.com/sharedfiles/filedetails/?id=3583201039). Maintained v1.4.8 releases: [War Sails](https://steamcommunity.com/sharedfiles/filedetails/?id=3781136815), [No War Sails](https://steamcommunity.com/sharedfiles/filedetails/?id=3781381482).
