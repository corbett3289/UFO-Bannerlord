using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using UFO.Extension;

namespace UFO.Behavior;

public sealed class AutoChoosePerks : CampaignBehaviorBase
{
    private bool _isApplyingPerks;

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, ApplyPlayerClanPerks);
        CampaignEvents.DailyTickHeroEvent.AddNonSerializedListener(this, ApplyEligiblePerks);
        CampaignEvents.PerkOpenedEvent.AddNonSerializedListener(this, OnPerkOpened);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnPerkOpened(Hero hero, PerkObject perk)
    {
        ApplyEligiblePerks(hero);
    }

    private void ApplyPlayerClanPerks()
    {
        Clan playerClan = Hero.MainHero?.Clan;
        if (playerClan == null)
        {
            return;
        }

        ApplyEligiblePerks(Hero.MainHero);

        foreach (Hero hero in playerClan.Heroes)
        {
            if (hero != Hero.MainHero)
            {
                ApplyEligiblePerks(hero);
            }
        }
    }

    private void ApplyEligiblePerks(Hero hero)
    {
        if (_isApplyingPerks)
        {
            return;
        }

        try
        {
            _isApplyingPerks = true;
            hero.AddBothBranchPerks();
        }
        finally
        {
            _isApplyingPerks = false;
        }
    }
}
