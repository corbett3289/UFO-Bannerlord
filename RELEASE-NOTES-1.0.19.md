# UFO v1.0.19 — Bannerlord v1.5.3 only

This compatibility release produces two new modules: `UFO15` for War Sails v1.3.3 and `UFONoWarSails15` without NavalDLC. Both support **Bannerlord v1.5.3 only**. Earlier game versions, including v1.5.0–v1.5.2, and future versions are unsupported. Existing v1.4.8 branches, module folders and Workshop items remain separate. Enable only one UFO edition; changing the module identity can produce an older-save module mismatch.

## Changes

- Pin the public build baseline to `1.5.3.122374-beta` and require Native v1.5.3; the War Sails edition requires NavalDLC v1.3.3.
- Retain maintained v1.0.18 campaign readiness, perk scope and hotkey recovery fixes, including explicit campaign choices overriding older global settings.
- Update changed Harmony/reflection targets and compile naval patches/settings only for the War Sails edition.
- Build deterministic .NET Framework 4.8/C# 12 assemblies with version `1.0.19.0`, separate edition outputs and no automatic deployment.
- Stage complete assets and localization while excluding game/framework/mod dependency DLLs. Provide edition ZIPs, SHA256 sidecars and new-item Workshop descriptors with initial private visibility.

## Verification status

Completed against the final v1.5.3-only binaries:

- Both editions compiled with zero warnings and zero errors.
- All eight static edition/reference audits passed across `1.5.1.120547-beta`, `1.5.2.120933-beta`, `1.5.2.121216-beta` and `1.5.3.122374-beta`.
- Seven auditor self-tests and 200 maintained behavior regression checks per edition passed.
- Live v1.5.3 IL inspection validated nine War Sails implementation contracts and one no-War-Sails healing contract.
- Each staged package had its expected module ID, module version `v1.0.19`, assembly version `1.0.19.0`, full source content and only `UFO.dll` as a binary.

Both final editions reached the v1.5.3 main menu, and game logs showed no UFO diagnostic errors. The no-War-Sails launch omitted NavalDLC. Both final editions were rebuilt against the v1.5.3 baseline, passed the final checks and were installed in their new module folders. The publishing user confirmed successful playtesting on **2026-10-04**, satisfying the campaign runtime gate and authorizing publication. This records the user's confirmation rather than an automated observation of every gameplay feature. Historical executable IL and gameplay were not verified and older versions are excluded; exhaustive gameplay coverage is not claimed.

Supported reference: `1.5.3.122374-beta`, Steam build 25302170, paired with War Sails v1.3.3. The no-War-Sails package has no NavalDLC dependency and omits naval code/settings.

For future regression playtests, test each edition separately using the clean module stacks in `Properties/launchSettings.json`:

1. Start a new non-Ironman Sandbox campaign and open UFO's MCM settings. Verify the naval settings group exists only in the War Sails edition and no failed-patch inquiry appears.
2. Change the campaign perk scope, advance campaign time, save under a new test name and reload. Check that the saved scope persists, including an explicit `No` choice, and that hotkeys still work after leaving and re-entering the campaign.
3. Smoke-test land combat, XP and daily healing. For War Sails, also check ship/fleet controls and generic speed, food, wage and healing settings while at sea.
4. Record future outcomes and any error reports against the audited DLL hashes in `Tools/Release/Verification-1.0.19.json`.

## Distribution

The campaign runtime gate is satisfied by the user's confirmed playtest. Both editions were published on **2026-10-04** (America/New_York): [War Sails 3813768338](https://steamcommunity.com/sharedfiles/filedetails/?id=3813768338) and [No War Sails 3813768472](https://steamcommunity.com/sharedfiles/filedetails/?id=3813768472). Both official public-update uploader runs reported `Uploading done!` and exited successfully. Fresh Steam metadata confirmed public visibility, and fresh downloads matched all 26 files per edition by relative path, size and SHA256 with no missing, extra or differing files. DLL and archive hashes are unchanged.

Both items require [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2859188632), [UIExtenderEx](https://steamcommunity.com/sharedfiles/filedetails/?id=2859222409), [ButterLib](https://steamcommunity.com/sharedfiles/filedetails/?id=2859232415), and [Mod Configuration Menu v5](https://steamcommunity.com/sharedfiles/filedetails/?id=2859238197). War Sails also requires DLC app [2927200](https://store.steampowered.com/app/2927200/); the no-War-Sails item has no app dependencies. Fresh dependency queries verified those exact requirements. Cookie-free public-page checks returned HTTP 200 for both items and verified the titles, version scope, credits, testing date, four required items and uploaded previews, with the DLC requirement only on War Sails.

Download verification used low-priority Steam UGC `DownloadItem(itemId, false)` without subscribing. The staged local and Steam packages are byte-identical, so a second campaign playtest solely for redistribution is unnecessary. The local ignored reports `artifacts/workshop/verify-WarSails-public.json`, `verify-NoWarSails-public.json` and `dependencies-public.json` provide detailed evidence; tracked `Tools/Release/Verification-1.0.19.json` summarizes their key facts and release hashes.

Use the corresponding update template and its recorded ID for subsequent uploads; do not rerun creation. Source templates remain private as a safe default, and intended public updates require explicit `-Visibility Public`. Verify downloaded content and public visibility after each update. Never overwrite the original author's item or previous 1.4.7/1.4.8 items.

All original bundle and mod credits remain with UFOdestiny and the original authors listed in the README.
