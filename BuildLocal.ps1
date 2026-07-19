param(
    [switch]$Deploy
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $MyInvocation.MyCommand.Path
$game = "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord"
$framework = "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.1"
$csc = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe"
$workshopModules = @(
    "C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\3767139118"
)
$output = Join-Path $repo "Module\bin\Win64_Shipping_Client\UFO.dll"

function Add-ManagedDlls($dir, $pattern = "*.dll") {
    if (Test-Path -LiteralPath $dir) {
        foreach ($dll in Get-ChildItem -LiteralPath $dir -Filter $pattern -File) {
            try {
                [void][Reflection.AssemblyName]::GetAssemblyName($dll.FullName)
                $dll.FullName
            } catch {
            }
        }
    }
}

function Copy-DeployFile($source, $destination) {
    try {
        Copy-Item -LiteralPath $source -Destination $destination -Force
    } catch {
        Write-Warning "Could not copy $source to $destination. The game or launcher may have the module loaded. $($_.Exception.Message)"
    }
}

New-Item -ItemType Directory -Force -Path (Split-Path $output) | Out-Null

$refFiles = @()
$refFiles += Add-ManagedDlls $framework
$refFiles += Add-ManagedDlls (Join-Path $framework "Facades")

$gameBin = Join-Path $game "bin\Win64_Shipping_Client"
$refFiles += Add-ManagedDlls $gameBin "TaleWorlds*.dll" | Where-Object { [IO.Path]::GetFileName($_) -ne "TaleWorlds.Native.dll" }
$refFiles += Join-Path $gameBin "Newtonsoft.Json.dll"

foreach ($module in @("Native", "SandBox", "SandBoxCore", "StoryMode", "CustomBattle", "BirthAndDeath", "NavalDLC", "Bannerlord.Harmony", "Bannerlord.UIExtenderEx")) {
    $dir = Join-Path $game "Modules\$module\bin\Win64_Shipping_Client"
    $refFiles += Add-ManagedDlls $dir | Where-Object {
        [IO.Path]::GetFileName($_) -notmatch "^(System|Microsoft)\." -and
        [IO.Path]::GetFileName($_) -ne "TaleWorlds.Native.dll"
    }
}

$refFiles += "C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\2859232415\bin\Win64_Shipping_Client\Bannerlord.ButterLib.dll"
$refFiles += "C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client\MCMv5.dll"
$refFiles += "C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client\MCM.UI.Adapter.MCMv5.dll"

$refs = $refFiles |
    Where-Object { Test-Path -LiteralPath $_ } |
    Sort-Object -Unique |
    ForEach-Object { '/reference:"{0}"' -f $_ }

$sources = Get-ChildItem -LiteralPath $repo -Recurse -Filter "*.cs" -File |
    ForEach-Object { '"{0}"' -f $_.FullName }

$rsp = Join-Path $env:TEMP ("ufo-csc-" + [guid]::NewGuid().ToString("n") + ".rsp")
$args = @(
    "/nologo",
    "/target:library",
    "/platform:x64",
    "/langversion:12.0",
    "/optimize+",
    "/debug:full",
    "/deterministic+",
    "/nostdlib+",
    ('/out:"{0}"' -f $output)
) + $refs + $sources

$args | Set-Content -LiteralPath $rsp -Encoding ASCII
try {
    & $csc ("@" + $rsp)
} finally {
    Remove-Item -LiteralPath $rsp -Force -ErrorAction SilentlyContinue
}

if ($Deploy) {
    foreach ($workshopModule in $workshopModules) {
        if (-not (Test-Path -LiteralPath $workshopModule)) {
            continue
        }
        New-Item -ItemType Directory -Force -Path (Join-Path $workshopModule "bin\Win64_Shipping_Client") | Out-Null
        Copy-DeployFile (Join-Path $repo "Module\SubModule.xml") (Join-Path $workshopModule "SubModule.xml")
        Copy-DeployFile (Join-Path $repo "Module\bin\Win64_Shipping_Client\UFO.dll") (Join-Path $workshopModule "bin\Win64_Shipping_Client\UFO.dll")
        Copy-DeployFile (Join-Path $repo "Module\bin\Win64_Shipping_Client\UFO.pdb") (Join-Path $workshopModule "bin\Win64_Shipping_Client\UFO.pdb")

        $moduleDataSource = Join-Path $repo "Module\ModuleData"
        $moduleDataDestination = Join-Path $workshopModule "ModuleData"
        if (Test-Path -LiteralPath $moduleDataSource) {
            New-Item -ItemType Directory -Force -Path $moduleDataDestination | Out-Null
            Copy-Item -Path (Join-Path $moduleDataSource "*") -Destination $moduleDataDestination -Recurse -Force
        }
    }
}
