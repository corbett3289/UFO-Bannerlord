using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using UFO.Extension;
using UFO.Setting;

namespace UFO.Behavior;

public sealed class AutoChoosePerks : CampaignBehaviorBase
{
    private bool _isApplyingPerks;
    private AutoChoosePerk_Type _savedScope = AutoChoosePerk_Type.No;
    private bool _hasSavedScope;

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, ApplyPlayerClanPerks);
        CampaignEvents.DailyTickHeroEvent.AddNonSerializedListener(this, ApplyEligiblePerks);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, RecoverPlayerClanPerks);
        CampaignEvents.PerkOpenedEvent.AddNonSerializedListener(this, OnPerkOpened);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    public override void SyncData(IDataStore dataStore)
    {
        int scope = (int)_savedScope;
        if (dataStore.IsSaving)
        {
            scope = (int)GetConfiguredScope();
        }

        dataStore.SyncData("UFO_AutoChoosePerksScope", ref scope);
        if (dataStore.IsLoading && scope >= (int)AutoChoosePerk_Type.Clan && scope <= (int)AutoChoosePerk_Type.No)
        {
            _savedScope = (AutoChoosePerk_Type)scope;
            _hasSavedScope = true;
        }
    }

    private void OnPerkOpened(Hero hero, PerkObject perk)
    {
        ApplyEligiblePerks(hero);
    }

    private void ApplyPlayerClanPerks()
    {
        ApplyPlayerClanPerks(GetConfiguredScope());
    }

    private void RecoverPlayerClanPerks()
    {
        SubModule.MarkCampaignReady();
        ApplyPlayerClanPerks(GetConfiguredScope());
    }

    private void ApplyPlayerClanPerks(AutoChoosePerk_Type scope)
    {
        Clan playerClan = Hero.MainHero?.Clan;
        if (playerClan == null)
        {
            return;
        }

        ApplyEligiblePerks(Hero.MainHero, scope);

        foreach (Hero hero in playerClan.Heroes)
        {
            if (hero != Hero.MainHero)
            {
                ApplyEligiblePerks(hero, scope);
            }
        }
    }

    private void OnGameLoaded(CampaignGameStarter starter)
    {
        RestoreAfterLoad();
    }

    private void OnGameLoadFinished()
    {
        RestoreAfterLoad();
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        RestoreAfterLoad();
    }

    private void RestoreAfterLoad()
    {
        SubModule.MarkCampaignReady();
        AutoChoosePerk_Type configuredScope = GetConfiguredScope();
        AutoChoosePerk_Type scope = configuredScope != AutoChoosePerk_Type.No || !_hasSavedScope
            ? configuredScope
            : _savedScope;

        if (scope == AutoChoosePerk_Type.All)
        {
            foreach (Hero hero in Hero.AllAliveHeroes)
            {
                ApplyEligiblePerks(hero, scope);
            }
        }
        else
        {
            ApplyPlayerClanPerks(scope);
        }
    }

    private AutoChoosePerk_Type GetConfiguredScope()
    {
        try
        {
            AutoChoosePerk_Type scope = SettingsManager.AutoChoosePerk.Value;
            if (scope != AutoChoosePerk_Type.No)
            {
                _savedScope = scope;
                _hasSavedScope = true;
            }
            return scope;
        }
        catch (InvalidOperationException)
        {
            return _hasSavedScope ? _savedScope : AutoChoosePerk_Type.No;
        }
    }

    private void ApplyEligiblePerks(Hero hero)
    {
        ApplyEligiblePerks(hero, GetConfiguredScope());
    }

    private void ApplyEligiblePerks(Hero hero, AutoChoosePerk_Type scope)
    {
        if (_isApplyingPerks)
        {
            return;
        }

        try
        {
            _isApplyingPerks = true;
            hero.AddBothBranchPerks(scope);
        }
        finally
        {
            _isApplyingPerks = false;
        }
    }
}
