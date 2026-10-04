using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using UFO.Behavior;
using UFO.Model;

namespace UFO.Bootstrap;

internal static class CampaignStarterConfigurator
{
    internal static void Configure(Game game, IGameStarter starter)
    {
        if (game.GameType is not Campaign || starter is not CampaignGameStarter campaignStarter)
            return;

        campaignStarter.AddBehavior(new SavingWeaponProperties.CustomBehavior());
        campaignStarter.AddBehavior(new AddMoney());
        campaignStarter.AddBehavior(new AutoChoosePerks());
        ReplaceModel<DefaultCharacterDevelopmentModel, ModifiedCharacterDevelopmentModel>(starter);
    }

    internal static void ConfigureCampaignStarter(IGameStarter starter)
    {
        if (starter is CampaignGameStarter campaignStarter)
            campaignStarter.AddBehavior(new RecruitExileClan());
    }

    private static void ReplaceModel<TBase, TReplacement>(IGameStarter starter)
        where TBase : GameModel
        where TReplacement : TBase
    {
        if (starter.Models.OfType<TReplacement>().Any())
            return;

        // Registering a later model is Bannerlord's supported override mechanism.
        starter.AddModel(Activator.CreateInstance<TReplacement>());
    }
}
