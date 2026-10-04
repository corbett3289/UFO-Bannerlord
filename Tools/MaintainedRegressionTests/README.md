# Maintained behavior regression checks

This runner loads the built UFO assembly and invokes its actual pure decision helpers. It checks supported game versions, naval settings isolation by edition, campaign setting authority, perk selection and load recovery scopes, the last leaderless troop casualty guard, and inherited/alternate view model lookup. These checks do not replace campaign and combat testing in Bannerlord.

Build with a .NET 10 SDK, then run once for each edition:

```powershell
dotnet build Tools/MaintainedRegressionTests/MaintainedRegressionTests.csproj -c Release
dotnet Tools/MaintainedRegressionTests/bin/Release/net10.0/MaintainedRegressionTests.dll artifacts/build/WarSails/UFO.dll <runtime-assembly-directory> ...
dotnet Tools/MaintainedRegressionTests/bin/Release/net10.0/MaintainedRegressionTests.dll artifacts/build/NoWarSails/UFO.dll <runtime-assembly-directory> ...
```

Supply directories containing the installed game's core, SandBox, StoryMode, CustomBattle, Harmony, UIExtenderEx, ButterLib and MCM runtime assemblies. Use actual runtime DLLs, since metadata-only reference assemblies cannot execute these checks. The runner does not initialize the game engine or modify an installed module.
