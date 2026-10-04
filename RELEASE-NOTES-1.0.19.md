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

The local War Sails edition reached the v1.5.3 main menu, and game logs showed no UFO diagnostic errors. Both final editions were rebuilt against the v1.5.3 baseline, passed the final checks and were installed in their new module folders. Campaign testing is pending because computer screen capture is currently unavailable through the automation tools. Remaining runtime checks cover MCM initialization, a new campaign, save/reload perk recovery, hotkeys and combat; War Sails additionally requires ship and fleet checks. Historical executable IL and gameplay were not verified and older versions are excluded. Static or regression checks and a menu load do not substitute for a campaign playtest.

Supported reference: `1.5.3.122374-beta`, Steam build 25302170, paired with War Sails v1.3.3. The no-War-Sails package has no NavalDLC dependency and omits naval code/settings.

## Distribution

Workshop creation and publication are pending the runtime checks. A candidate source branch upload is separate from Workshop distribution. After the runtime checks pass, create two new private items, verify their content and IDs, then publish them. Never overwrite the original author's item or previous 1.4.7/1.4.8 items. New IDs, payload verification and final public visibility must be recorded before describing publication as complete.

All original bundle and mod credits remain with UFOdestiny and the original authors listed in the README.
