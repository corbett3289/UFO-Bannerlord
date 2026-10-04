param(
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord',
    [Parameter(Mandatory = $true)]
    [ValidateSet('WarSails', 'NoWarSails')]
    [string]$Edition,
    [string]$ReferenceRoot = '',
    [string]$OutputRoot = '',
    [string]$DotNetPath = 'dotnet',
    [switch]$Deploy
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $OutputRoot) { $OutputRoot = Join-Path $repoRoot 'artifacts' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot).TrimEnd('\', '/')
$GameRoot = [IO.Path]::GetFullPath($GameRoot).TrimEnd('\', '/')
if ($ReferenceRoot) { $ReferenceRoot = (Resolve-Path -LiteralPath $ReferenceRoot).Path }
if ($env:UFO_DOTNET_PATH -and $DotNetPath -eq 'dotnet') { $DotNetPath = $env:UFO_DOTNET_PATH }

if ($OutputRoot.StartsWith($GameRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    $OutputRoot.Equals($GameRoot, [StringComparison]::OrdinalIgnoreCase) -or
    $OutputRoot.StartsWith((Join-Path $repoRoot 'Module') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    $OutputRoot.Equals((Join-Path $repoRoot 'Module'), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputRoot must be outside the live game installation and source Module folder.'
}

$moduleId = if ($Edition -eq 'WarSails') { 'UFO15' } else { 'UFONoWarSails15' }
$intermediateRoot = Join-Path $OutputRoot ('obj\' + $Edition + '\')
$buildArgs = @(
    'build', (Join-Path $repoRoot 'UFO.csproj'), '--configuration', 'Release', '--nologo',
    '-p:Platform=x64', ('-p:Edition=' + $Edition), ('-p:OutputRoot=' + $OutputRoot),
    ('-p:BaseIntermediateOutputPath=' + $intermediateRoot),
    ('-p:MSBuildProjectExtensionsPath=' + $intermediateRoot),
    '-p:DeployToGameFolder=false'
)
if ($ReferenceRoot) { $buildArgs += '-p:ReferenceRoot=' + $ReferenceRoot }
& $DotNetPath @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'Release build failed; no package or deployment was created.' }

$binaryRoot = Join-Path $OutputRoot ('build\' + $Edition)
$moduleRoot = Join-Path $OutputRoot ('packages\' + $moduleId)
$sourceRoot = Join-Path $repoRoot 'Module'
$manifest = Join-Path $PSScriptRoot ('Manifests\SubModule.' + $Edition + '.xml')
$packagePrefix = $OutputRoot + [IO.Path]::DirectorySeparatorChar
if (-not [IO.Path]::GetFullPath($moduleRoot).StartsWith($packagePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Resolved package path escaped OutputRoot.'
}
if (Test-Path -LiteralPath $moduleRoot) { Remove-Item -LiteralPath $moduleRoot -Recurse -Force }
New-Item -ItemType Directory -Path $moduleRoot -Force | Out-Null

foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File) {
    $relative = $file.FullName.Substring($sourceRoot.Length + 1)
    # Never ship an old module binary or dependency DLL from the source tree.
    if ($relative -eq 'SubModule.xml' -or $file.Extension -in '.dll', '.pdb' -or $relative -match '(^|\\)(obj|ref)(\\|$)') { continue }
    $destination = Join-Path $moduleRoot $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
}
$packageBin = Join-Path $moduleRoot 'bin\Win64_Shipping_Client'
New-Item -ItemType Directory -Path $packageBin -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $binaryRoot 'UFO.dll') -Destination $packageBin -Force
Copy-Item -LiteralPath $manifest -Destination (Join-Path $moduleRoot 'SubModule.xml') -Force

$descriptor = [xml](Get-Content -LiteralPath (Join-Path $moduleRoot 'SubModule.xml') -Raw)
if ($descriptor.Module.Id.value -ne $moduleId -or $descriptor.Module.Version.value -ne 'v1.0.19') {
    throw 'Package module identity or version is incorrect.'
}
$packageDlls = @(Get-ChildItem -LiteralPath $moduleRoot -Recurse -Filter '*.dll' -File)
if ($packageDlls.Count -ne 1 -or $packageDlls[0].Name -ne 'UFO.dll') { throw 'The release must contain only UFO.dll.' }
$assembly = [Reflection.AssemblyName]::GetAssemblyName($packageDlls[0].FullName)
if ($assembly.Version.ToString() -ne '1.0.19.0') { throw 'The release assembly version is incorrect.' }
if ($Edition -eq 'NoWarSails' -and $descriptor.Module.DependedModules.DependedModule.Id -contains 'NavalDLC') {
    throw 'NoWarSails package unexpectedly requires NavalDLC.'
}

$zipPath = Join-Path $OutputRoot ('UFO-1.0.19-Bannerlord-1.5.3-' + $Edition + '.zip')
Compress-Archive -LiteralPath $moduleRoot -DestinationPath $zipPath -Force
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($zipPath + '.sha256') -Value ($hash + '  ' + [IO.Path]::GetFileName($zipPath)) -Encoding ascii

if ($Deploy) {
    $nativeManifest = Join-Path $GameRoot 'Modules\Native\SubModule.xml'
    if (-not (Test-Path -LiteralPath $nativeManifest)) { throw 'Deploy requires a valid Bannerlord GameRoot.' }
    $nativeVersion = ([xml](Get-Content -LiteralPath $nativeManifest -Raw)).Module.Version.value
    if ($nativeVersion -notmatch '^v1\.5\.3(?:\.\d+)?$') { throw "Deployment supports Bannerlord v1.5.3 only; found $nativeVersion" }
    if ($Edition -eq 'WarSails') {
        $navalManifest = Join-Path $GameRoot 'Modules\NavalDLC\SubModule.xml'
        if (-not (Test-Path -LiteralPath $navalManifest)) { throw 'WarSails deployment requires NavalDLC v1.3.3.' }
        $navalVersion = ([xml](Get-Content -LiteralPath $navalManifest -Raw)).Module.Version.value
        if ($navalVersion -notmatch '^v1\.3\.3(?:\.\d+)?$') { throw "Deployment requires NavalDLC v1.3.3; found $navalVersion" }
    }
    $modulesRoot = [IO.Path]::GetFullPath((Join-Path $GameRoot 'Modules')).TrimEnd('\', '/')
    $deployRoot = [IO.Path]::GetFullPath((Join-Path $modulesRoot $moduleId))
    if (-not $deployRoot.StartsWith($modulesRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Resolved deployment path escaped the game Modules directory.'
    }
    New-Item -ItemType Directory -Path $deployRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $moduleRoot '*') -Destination $deployRoot -Recurse -Force
    Write-Host "Deployed $moduleId to $deployRoot. Enable only one UFO edition."
}

[pscustomobject]@{ Edition = $Edition; ModuleId = $moduleId; ModuleRoot = $moduleRoot; Archive = $zipPath; SHA256 = $hash }
