# UFO compatibility audit

This .NET 10 tool reads assembly metadata without starting Bannerlord or applying patches. Supply one isolated official reference snapshot and the exact dependency framework directories used for compilation. It does not search neighboring mods or choose an assembly from another game version.

```powershell
dotnet build Tools/HarmonyApiAudit/HarmonyApiAudit.csproj -c Release
dotnet run --project Tools/HarmonyApiAudit/HarmonyApiAudit.csproj -c Release -- --self-test
```

Audit a staged binary. Repeat `--support-dir` for Harmony, MCM, ButterLib, and UIExtenderEx; select their exact `lib/net48`, `lib/net472`, or `lib/netstandard2.0` directories rather than the whole package cache.

```powershell
dotnet run --project Tools/HarmonyApiAudit/HarmonyApiAudit.csproj -c Release -- `
  artifacts/build/WarSails/UFO.dll C:/References/1.5.1.120547-beta `
  --support-dir C:/Dependencies/Harmony-net48 `
  --support-dir C:/Dependencies/MCM-netstandard2.0 `
  --support-dir C:/Dependencies/ButterLib-net472 `
  --support-dir C:/Dependencies/UIExtenderEx-netstandard2.0
```

Use `--without-dlc` for the no-War-Sails binary. This excludes NavalDLC assemblies and rejects an integration class or hard DLC dependency in that edition.

Use `--implementation-dir C:/Snapshots/Bannerlord-1.5.3` to verify Native's single member-hero healing call, sea-dependent perk/trait/rounding flow, and the eight DLC model forwarding contracts. A full installed-game root is supported: only its Shipping Client binaries and official modules are inspected. Metadata-only reference packs cannot verify these IL contracts; the output explicitly reports that limitation.

The audit checks merged class/method Harmony target attributes, explicit overloads including by-reference arguments, callback names and types, declarative Naval targets, reflected private members, and direct game method/field references in the staged binary. Arbitrary runtime `TargetMethod` discovery is rejected unless represented by an explicit metadata contract; it is never executed.

`--inspect Fully.Qualified.Type [Member]` prints metadata for troubleshooting. Exit codes are `0` for passing checked contracts, `1` for incompatibilities, `2` for an audit/environment error, and `64` for missing required arguments. Every normal audit prints the staged binary's SHA256 so results can be associated with the release artifact.
