using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using UFO.Setting;

namespace UFO.Extension
{
    public static class HeroEnhanceExtensions
    {
        public static readonly TextObject RaidedText = new TextObject("{=RVas572P}Raided");

        private static Clan PlayerClanOrNull()
        {
            return Hero.MainHero?.Clan;
        }

        private static bool CampaignHeroesReady()
        {
            return SubModule.CampaignReady;
        }

        private static bool IsHeroReady(Hero hero)
        {
            return CampaignHeroesReady() && hero != null && hero.IsInitialized;
        }

        private static bool IsPlayerClanMember(Hero hero)
        {
            Clan playerClan = PlayerClanOrNull();
            if (hero == null || playerClan == null)
            {
                return false;
            }

            if (hero == Hero.MainHero || hero.Clan == playerClan || hero.IsPlayerCompanion)
            {
                return true;
            }

            foreach (Hero clanHero in playerClan.Heroes)
            {
                if (clanHero == hero)
                {
                    return true;
                }
            }

            return false;
        }

        public static int AgeScale(this Hero hero)
        {
            if (hero == null || Game.Current?.PlayerTroop == null)
            {
                return -1;
            }
            if (SettingsManager.EnableEverYoung.Value && hero == Hero.MainHero)
            {
                return -1;
            }
            if (!IsPlayerClanMember(hero))
            {
                return 1;
            }
            int num = hero.GetSkillValue(DefaultSkills.Athletics) + hero.GetSkillValue(DefaultSkills.Charm);
            int num2 = 2;
            if (num >= SettingsManager.EverYoungSkillNeed.Value)
            {
                return -1;
            }
            if (!hero.IsWanderer)
            {
                num2++;
            }
            return num2;
        }

        public static bool CombatEnhanceDisable()
        {
            return SettingsManager.CombatAttributeRatePlayer.Value == 0f && SettingsManager.CombatAttributeRateClanMember.Value == 0f && SettingsManager.CombatAttributeRateOther.Value == 0f;
        }

        public static bool StrategyEnhanceDisable()
        {
            return SettingsManager.StrategyAttributeRatePlayer.Value == 0f && SettingsManager.StrategyAttributeRateClanMember.Value == 0f && SettingsManager.StrategyAttributeRateOther.Value == 0f;
        }

        public static float CombatEnhanceRate(this CharacterObject character)
        {
            if (character == null)
            {
                return 0f;
            }
            Hero heroObject = character.HeroObject;
            return heroObject.CombatEnhanceRate();
        }

        public static float CombatEnhanceRate(this Agent agent)
        {
            if (agent == null)
            {
                return 0f;
            }
            CharacterObject character = agent.Character as CharacterObject;
            return character.CombatEnhanceRate();
        }

        public static float CombatEnhanceRate(this Hero hero)
        {
            if (!IsHeroReady(hero))
            {
                return 0f;
            }
            if (hero == Hero.MainHero)
            {
                return SettingsManager.CombatAttributeRatePlayer.Value;
            }
            if (IsPlayerClanMember(hero))
            {
                return SettingsManager.CombatAttributeRateClanMember.Value;
            }
            return SettingsManager.CombatAttributeRateOther.Value;
        }

        public static float StrategyEnhanceRate(this CharacterObject character)
        {
            if (character == null)
            {
                return 0f;
            }
            Hero heroObject = character.HeroObject;
            return heroObject.StrategyEnhanceRate();
        }

        public static float StrategyEnhanceRate(this Agent agent)
        {
            if (agent == null)
            {
                return 0f;
            }
            CharacterObject character = agent.Character as CharacterObject;
            return character.StrategyEnhanceRate();
        }

        public static float StrategyEnhanceRate(this Hero hero)
        {
            if (!IsHeroReady(hero))
            {
                return 0f;
            }
            if (hero == Hero.MainHero)
            {
                return SettingsManager.StrategyAttributeRatePlayer.Value;
            }
            if (IsPlayerClanMember(hero))
            {
                return SettingsManager.StrategyAttributeRateClanMember.Value;
            }
            return SettingsManager.StrategyAttributeRateOther.Value;
        }

        public static int EnhanceType(this CharacterObject character)
        {
            if (character == null)
            {
                return -1;
            }
            Hero heroObject = character.HeroObject;
            return heroObject.EnhanceType();
        }

        public static int EnhanceType(this Hero hero)
        {
            if (!IsHeroReady(hero))
            {
                return -1;
            }
            if (hero == Hero.MainHero)
            {
                return 1;
            }
            if (IsPlayerClanMember(hero))
            {
                return 0;
            }
            return 2;
        }

        public static void AddBothBranchPerks(this Hero hero)
        {
            if (!IsHeroReady(hero) || !hero.IsAlive || hero.HeroDeveloper == null)
            {
                return;
            }

            if (!ShouldAddBothBranchPerks(SettingsManager.AutoChoosePerk.Value, hero))
            {
                return;
            }

            foreach (PerkObject item in PerkObject.All)
            {
                SkillObject skill = item.Skill;
                if (skill != null &&
                    hero.GetSkillValue(skill) >= item.RequiredSkillValue &&
                    !hero.GetPerkValue(item))
                {
                    hero.HeroDeveloper.AddPerk(item);
                }
            }
        }

        internal static bool ShouldAddBothBranchPerks(AutoChoosePerk_Type scope, Hero hero)
        {
            if (hero == null)
            {
                return false;
            }

            switch (scope)
            {
                case AutoChoosePerk_Type.Clan:
                    return IsPlayerClanMember(hero);
                case AutoChoosePerk_Type.Player:
                    return hero == Hero.MainHero;
                case AutoChoosePerk_Type.All:
                    return true;
                default:
                    return false;
            }
        }

        internal static bool ShouldAddBothBranchPerks(AutoChoosePerk_Type scope, int heroType)
        {
            switch (scope)
            {
                case AutoChoosePerk_Type.Clan:
                    return heroType == 0 || heroType == 1;
                case AutoChoosePerk_Type.Player:
                    return heroType == 1;
                case AutoChoosePerk_Type.All:
                    return heroType >= 0;
                default:
                    return false;
            }
        }

        public static float TrueAge(this Hero hero)
        {
            if (CampaignOptions.IsLifeDeathCycleDisabled)
            {
                return Traverse.Create(hero).Field("_defaultAge").GetValue<float>();
            }
            CampaignTime value = Traverse.Create(hero).Field("_birthDay").GetValue<CampaignTime>();
            if (hero.IsAlive)
            {
                return value.ElapsedYearsUntilNow;
            }
            return (float)(hero.DeathDay - value).ToYears;
        }
    }

}
