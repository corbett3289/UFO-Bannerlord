using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using UFO.Behavior;
using UFO.Bootstrap;
using UFO.Diagnostics;
using UFO.Extension;
using UFO.Model;
using UFO.Patching;
using UFO.Setting;

namespace UFO;

internal class SubModule : MBSubModuleBase
{
    private bool PatchesApplied = false;
    private Harmony patcher;
    private bool unsupportedGameVersion;
    private string versionWarning;

    internal static bool CampaignReady { get; private set; }

    internal static void MarkCampaignReady()
    {
        CampaignReady = Campaign.Current != null && Hero.MainHero != null;
    }

    internal static bool IsSupportedGameVersion(int major, int minor, int revision)
    {
        return major == 1 && minor == 5 && revision == 3;
    }

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        CheckGameVersion();
    }

    private void CheckGameVersion()
    {
        try
        {
            var native = ModuleHelper.GetModules().FirstOrDefault(module => module.Id == "Native");
            if (native == null)
            {
                versionWarning = "UFO could not identify the Native game version. Compatibility is unverified; this release supports Bannerlord v1.5.3 only.";
            }
            else
            {
                var version = native.Version;
                unsupportedGameVersion = !IsSupportedGameVersion(version.Major, version.Minor, version.Revision);
                if (unsupportedGameVersion)
                {
                    versionWarning = $"UFO requires Bannerlord v1.5.3. Detected Native {version}; UFO campaign features and patches are disabled.";
                }
            }
        }
        catch (Exception exception)
        {
            versionWarning = $"UFO could not verify the game version ({exception.Message}). This release supports Bannerlord v1.5.3 only.";
        }

        if (versionWarning != null)
        {
            try { ModDiagnostics.WriteError(new NotSupportedException(versionWarning), typeof(SubModule)); }
            catch { }
        }
    }

    protected override void OnBeforeInitialModuleScreenSetAsRoot()
    {
        L10N.LoadLanguage();
        if (versionWarning != null)
            InformationManager.DisplayMessage(new InformationMessage(versionWarning, unsupportedGameVersion ? Colors.Red : Colors.Yellow));
        if (!unsupportedGameVersion)
            InformationManager.DisplayMessage(new InformationMessage("UFO's Mod Loaded", Colors.Green));
    }

    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        base.OnGameStart(game, gameStarterObject);
        CampaignReady = false;

        if (!unsupportedGameVersion && game.GameType is Campaign)
        {
            CampaignStarterConfigurator.Configure(game, gameStarterObject);
        }
    }

    protected override void InitializeGameStarter(Game game, IGameStarter starterObject)
    {
        base.InitializeGameStarter(game, starterObject);
        if (!unsupportedGameVersion)
            CampaignStarterConfigurator.ConfigureCampaignStarter(starterObject);
    }

    public override void OnGameInitializationFinished(Game game)
    {
        base.OnGameInitializationFinished(game);
    }

    public override void OnAfterGameInitializationFinished(Game game, object starterObject)
    {
        base.OnAfterGameInitializationFinished(game, starterObject);

        if (!(game.GameType is Campaign) || unsupportedGameVersion)
        {
            return;
        }

        if (!PatchesApplied)
        {
            patcher = new Harmony("UFO");
            var failedPatches = new List<string>(PatchBootstrapper.Apply(patcher, typeof(SubModule).Assembly));
#if UFO_NAVALDLC
            failedPatches.AddRange(NavalDlcCompatibility.Apply(patcher));
#endif
            PatchesApplied = true;
            if (failedPatches.Any())
                InformationManager.ShowInquiry(new InquiryData(L10N.GetText("ModFailedLoadWarningTitle"), L10N.GetTextFormat("ModFailedLoadWarningMessage", string.Join(Environment.NewLine, failedPatches)), true, false, L10N.GetText("ModWarningMessageConfirm"), null, null, null));
        }

        // Restore readiness even if the campaign was replaced without repatching.
        MarkCampaignReady();

        //PatchInspector.PatchInformation();

        //InformationManager.DisplayMessage(new InformationMessage("UFO's Mod Patch Applied", Colors.Green));
    }

    public override void OnGameEnd(Game game)
    {
        CampaignReady = false;
        if (PatchesApplied)
        {
            (patcher ?? new Harmony("UFO")).UnpatchAll("UFO");
            PatchesApplied = false;
            patcher = null;
        }

        base.OnGameEnd(game);
    }

    internal static void LogError(Exception e, Type type)
    {
        string text;
        try
        {
            text = ModDiagnostics.WriteError(e, type);
        }
        catch
        {
            return;
        }
        try
        {
            InformationManager.ShowInquiry(new InquiryData(L10N.GetText("ModExceptionTitle"), L10N.GetTextFormat("ModExceptionMessage", text), isAffirmativeOptionShown: true, isNegativeOptionShown: false, L10N.GetText("ModWarningMessageConfirm"), null, null, null));
        }
        catch
        {
            try
            {
                Message.Show(L10N.GetTextFormat("ModExceptionMessage", text), Colors.Red);
            }
            catch
            {
            }
        }
    }

}


