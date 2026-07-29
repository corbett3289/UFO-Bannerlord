using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using UFO.Extension;

namespace UFO.Behavior;

public sealed class AutoChoosePerks : CampaignBehaviorBase
{
    private bool _isApplyingPerks;

    public override void RegisterEvents()
    {
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
