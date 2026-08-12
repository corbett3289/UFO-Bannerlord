$ErrorActionPreference = "Stop"

$game = "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord"
$probeDirs = @(
    (Join-Path $game "bin\Win64_Shipping_Client"),
    (Join-Path $game "Modules\Native\bin\Win64_Shipping_Client"),
    (Join-Path $game "Modules\SandBox\bin\Win64_Shipping_Client"),
    (Join-Path $game "Modules\SandBoxCore\bin\Win64_Shipping_Client"),
    (Join-Path $game "Modules\StoryMode\bin\Win64_Shipping_Client"),
    (Join-Path $game "Modules\CustomBattle\bin\Win64_Shipping_Client"),
    (Join-Path $game "Modules\BirthAndDeath\bin\Win64_Shipping_Client"),
    (Join-Path $game "Modules\Bannerlord.Harmony\bin\Win64_Shipping_Client"),
    (Join-Path $game "Modules\Bannerlord.UIExtenderEx\bin\Win64_Shipping_Client"),
    "C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\2859232415\bin\Win64_Shipping_Client",
    "C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client"
)

$assemblyMap = @{}
foreach ($dir in $probeDirs) {
    if (-not (Test-Path -LiteralPath $dir)) {
        continue
    }

    foreach ($file in Get-ChildItem -LiteralPath $dir -Filter "*.dll" -File) {
        try {
            $name = [Reflection.AssemblyName]::GetAssemblyName($file.FullName).Name
            if (-not $assemblyMap.ContainsKey($name)) {
                $assemblyMap[$name] = $file.FullName
            }
        } catch {
        }
    }
}

$resolverScript = {
    param($sender, $args)
    $name = ([Reflection.AssemblyName]::new($args.Name)).Name
    if ($assemblyMap.ContainsKey($name)) {
        return [Reflection.Assembly]::LoadFrom($assemblyMap[$name])
    }
    return $null
}.GetNewClosure()
$resolver = [ResolveEventHandler]$resolverScript

[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    foreach ($path in $assemblyMap.Values) {
        try {
            [void][Reflection.Assembly]::LoadFrom($path)
        } catch {
        }
    }
    $harmonyAssembly = [Reflection.Assembly]::LoadFrom($assemblyMap["0Harmony"])
    $ufoAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot "Module\bin\Win64_Shipping_Client\UFO.dll"))
    $navalReferences = @($ufoAssembly.GetReferencedAssemblies() |
        Where-Object { $_.Name -like "NavalDLC*" })
    $harmonyType = $harmonyAssembly.GetType("HarmonyLib.Harmony", $true)
    $patchAttributeType = $harmonyAssembly.GetType("HarmonyLib.HarmonyPatch", $true)
    $harmony = [Activator]::CreateInstance($harmonyType, [object[]]@("ufo.compatibility.audit"))
    $createProcessor = $harmonyType.GetMethods() |
        Where-Object { $_.Name -eq "CreateClassProcessor" -and $_.GetParameters().Count -eq 1 } |
        Select-Object -First 1
    try {
        $ufoTypes = $ufoAssembly.GetTypes()
    } catch [Reflection.ReflectionTypeLoadException] {
        $_.Exception.LoaderExceptions | ForEach-Object { Write-Error $_.Message }
        throw
    }
    $patchTypes = @($ufoTypes |
        Where-Object { $_.GetCustomAttributes($patchAttributeType, $false).Count -gt 0 })
    $failures = @()
    $applied = 0

    foreach ($type in $patchTypes) {
        try {
            $processorArgs = [object[]]::new(1)
            $processorArgs[0] = $type.psobject.BaseObject
            $processor = $createProcessor.Invoke($harmony, $processorArgs)
            $patchMethod = $processor.GetType().GetMethod("Patch", [Type]::EmptyTypes)
            [void]$patchMethod.Invoke($processor, $null)
            $applied++
        } catch {
            $cause = if ($_.Exception.InnerException) { $_.Exception.InnerException } else { $_.Exception }
            $failures += ($type.FullName + ": " + $cause.Message)
        }
    }

    $unpatchSelf = $harmonyType.GetMethod("UnpatchSelf", [Type]::EmptyTypes)
    if ($unpatchSelf) {
        [void]$unpatchSelf.Invoke($harmony, $null)
    } else {
        $unpatchAll = $harmonyType.GetMethods() |
            Where-Object { $_.Name -eq "UnpatchAll" -and $_.GetParameters().Count -eq 1 } |
            Select-Object -First 1
        [void]$unpatchAll.Invoke($harmony, [object[]]@("ufo.compatibility.audit"))
    }

    $bindingFlags = [Reflection.BindingFlags]::Instance -bor
        [Reflection.BindingFlags]::Static -bor
        [Reflection.BindingFlags]::Public -bor
        [Reflection.BindingFlags]::NonPublic
    $memberFailures = @()
    $memberChecks = 0

    function Get-AuditType([string]$fullName) {
        foreach ($assembly in [AppDomain]::CurrentDomain.GetAssemblies()) {
            $result = $assembly.GetType($fullName, $false)
            if ($result) {
                return $result
            }
        }
        throw "Type not found: $fullName"
    }

    function Test-AuditField([string]$typeName, [string]$fieldName) {
        $script:memberChecks++
        try {
            $type = Get-AuditType $typeName
            if (-not $type.GetField($fieldName, $bindingFlags)) {
                $script:memberFailures += "$typeName.$fieldName"
            }
        } catch {
            $script:memberFailures += "$typeName.$fieldName"
        }
    }

    function Test-AuditMethod([string]$typeName, [string]$methodName, [int]$parameterCount) {
        $script:memberChecks++
        try {
            $type = Get-AuditType $typeName
            $method = $type.GetMethods($bindingFlags) |
                Where-Object { $_.Name -eq $methodName -and $_.GetParameters().Count -eq $parameterCount } |
                Select-Object -First 1
            if (-not $method) {
                $script:memberFailures += "$typeName.$methodName($parameterCount parameters)"
            }
        } catch {
            $script:memberFailures += "$typeName.$methodName($parameterCount parameters)"
        }
    }

    Test-AuditField "TaleWorlds.CampaignSystem.Hero" "_defaultAge"
    Test-AuditField "TaleWorlds.CampaignSystem.Hero" "_birthDay"
    Test-AuditField "TaleWorlds.CampaignSystem.Settlements.Locations.Location" "_characterList"
    Test-AuditField "TaleWorlds.CampaignSystem.Settlements.Locations.Location" "_aiCanExit"
    Test-AuditField "TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages.EncyclopediaHeroPageVM" "_hero"
    Test-AuditField "TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPInventoryVM" "_selectedItem"
    Test-AuditMethod "TaleWorlds.CampaignSystem.ViewModelCollection.Party.PartyVM" "InitializeTroopLists" 0
    Test-AuditField "SandBox.GauntletUI.GauntletInventoryScreen" "_dataSource"
    Test-AuditField "SandBox.GauntletUI.GauntletCharacterDeveloperScreen" "_dataSource"
    Test-AuditField "SandBox.GauntletUI.GauntletPartyScreen" "_dataSource"
    Test-AuditMethod "TaleWorlds.CampaignSystem.CampaignBehaviors.WorkshopsCampaignBehavior" "TickOneProductionCycleForPlayerWorkshop" 3
    Test-AuditMethod "TaleWorlds.CampaignSystem.CampaignBehaviors.WorkshopsCampaignBehavior" "TickOneProductionCycleForNotableWorkshop" 3

    Write-Output ("PatchClasses=" + $patchTypes.Count)
    Write-Output ("Applied=" + $applied)
    Write-Output ("Failed=" + $failures.Count)
    Write-Output ("ReflectedMembers=" + $memberChecks)
    Write-Output ("MissingMembers=" + $memberFailures.Count)
    Write-Output ("NavalDLCReferences=" + $navalReferences.Count)
    $failures | ForEach-Object { Write-Output $_ }
    $memberFailures | ForEach-Object { Write-Output $_ }

    if ($failures.Count -ne 0 -or $applied -ne 184 -or $memberFailures.Count -ne 0 -or $navalReferences.Count -ne 0) {
        exit 1
    }
} finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
}
