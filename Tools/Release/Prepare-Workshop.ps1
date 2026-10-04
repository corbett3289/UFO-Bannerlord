param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('WarSails', 'NoWarSails')]
    [string]$Edition,
    [Parameter(Mandatory = $true)]
    [string]$ModuleRoot,
    [string]$OutputPath = '',
    [string]$ItemId = '',
    [ValidateSet('Private', 'Public')]
    [string]$Visibility = 'Private'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$ModuleRoot = (Resolve-Path -LiteralPath $ModuleRoot).Path
$expectedId = if ($Edition -eq 'WarSails') { 'UFO15' } else { 'UFONoWarSails15' }
$manifest = [xml](Get-Content -LiteralPath (Join-Path $ModuleRoot 'SubModule.xml') -Raw)
if ($manifest.Module.Id.value -ne $expectedId -or $manifest.Module.Version.value -ne 'v1.0.19') {
    throw 'Workshop payload must be the staged v1.0.19 package for the selected edition.'
}
$nativeDependency = @($manifest.Module.DependedModules.DependedModule | Where-Object { $_.Id -eq 'Native' })
$nativeMetadata = @($manifest.Module.DependedModuleMetadatas.DependedModuleMetadata | Where-Object { $_.id -eq 'Native' })
if ($nativeDependency.Count -ne 1 -or $nativeDependency[0].DependentVersion -ne 'v1.5.3' -or
    $nativeMetadata.Count -ne 1 -or $nativeMetadata[0].version -ne 'v1.5.3') {
    throw 'Workshop payload must target Bannerlord v1.5.3 only; rebuild the narrowed release package.'
}
if ($Edition -eq 'WarSails') {
    $navalDependency = @($manifest.Module.DependedModules.DependedModule | Where-Object { $_.Id -eq 'NavalDLC' })
    $navalMetadata = @($manifest.Module.DependedModuleMetadatas.DependedModuleMetadata | Where-Object { $_.id -eq 'NavalDLC' })
    if ($navalDependency.Count -ne 1 -or $navalDependency[0].DependentVersion -ne 'v1.3.3' -or
        $navalMetadata.Count -ne 1 -or $navalMetadata[0].version -ne 'v1.3.3') {
        throw 'WarSails Workshop payload must target NavalDLC v1.3.3 only.'
    }
} elseif ($manifest.Module.DependedModules.DependedModule.Id -contains 'NavalDLC') {
    throw 'NoWarSails Workshop payload must not require NavalDLC.'
}
if ($ItemId -and ($ItemId -notmatch '^\d+$' -or $ItemId -in @('3583201039', '3767139118', '3768535938', '3781136815', '3781381482'))) {
    throw 'ItemId must identify a new 1.5 item, never an original or older UFO release.'
}
if (-not $ItemId -and $Visibility -eq 'Public') { throw 'Create the item privately first, then use its confirmed ItemId to publish.' }
$task = if ($ItemId) { 'Update' } else { 'Create' }
$templatePath = Join-Path $repoRoot ('SteamWorkshop\Workshop' + $task + $Edition + '15.xml')
$xml = [xml](Get-Content -LiteralPath $templatePath -Raw)
$previewPath = Join-Path $repoRoot 'SteamWorkshop\image15.png'
if (-not (Test-Path -LiteralPath $previewPath -PathType Leaf)) { throw 'The 1.5 Workshop preview is missing: SteamWorkshop/image15.png.' }
if ((Get-Item -LiteralPath $previewPath).Length -ge 1000000) { throw 'The 1.5 Workshop preview must be smaller than 1 MB.' }
if ($ItemId) { $xml.Tasks.GetItem.ItemId.Value = $ItemId }
$xml.Tasks.UpdateItem.ModuleFolder.Value = $ModuleRoot
$xml.Tasks.UpdateItem.Image.Value = $previewPath
$xml.Tasks.UpdateItem.Visibility.Value = $Visibility
if (-not $OutputPath) { $OutputPath = Join-Path $repoRoot ('artifacts\workshop\' + $task + $Edition + '15.xml') }
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null
$xml.Save($OutputPath)
Write-Host "Prepared $Visibility Workshop $task descriptor: $OutputPath"
Write-Host 'This script does not upload anything. Run the official uploader only after release checks pass.'
