﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BannerKings.Managers.Skills;
using BannerKings.Managers.Titles;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace BannerKings.Behaviours
{
    public class BKClanBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, DailyClanTick);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
            CampaignEvents.OnHeroGetsBusyEvent.AddNonSerializedListener(this, OnHeroGetsBusy);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnHeroGetsBusy(Hero hero, HeroGetsBusyReasons heroGetsBusyReason)
        {
            if (hero.CompanionOf != null)
            {
                RemovePartyRoleIfExist(hero);
            }
        }

        private void OnHeroPrisonerTaken(PartyBase party, Hero prisoner)
        {
            if (prisoner.CompanionOf != null)
            {
                RemovePartyRoleIfExist(prisoner);
            }
        }

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail,
            bool showNotification = true)
        {
            if (victim.CompanionOf != null)
            {
                RemovePartyRoleIfExist(victim);
            }
        }

        private void RemovePartyRoleIfExist(Hero hero)
        {
            foreach (var warPartyComponent in hero.Clan.WarPartyComponents)
            {
                if (warPartyComponent.MobileParty.GetHeroPerkRole(hero) != SkillEffect.PerkRole.None)
                {
                    warPartyComponent.MobileParty.RemoveHeroPerkRole(hero);
                }
            }
        }

        private void DailyClanTick(Clan clan)
        {
            if (clan.IsEliminated || clan.IsBanditFaction || clan.Kingdom == null ||
                BannerKingsConfig.Instance.TitleManager == null)
            {
                return;
            }

            BannerKingsConfig.Instance.CourtManager.UpdateCouncil(clan);
            var councillours = BannerKingsConfig.Instance.CourtManager.GetCouncilloursCount(clan);
            if (councillours != 0)
            {
                clan.Leader.AddSkillXp(BKSkills.Instance.Lordship, councillours * 2f);
            }

            if (clan == Clan.PlayerClan || clan.IsUnderMercenaryService || clan.IsMinorFaction || clan.IsBanditFaction)
            {
                return;
            }

            EvaluateRecruitKnight(clan);
            EvaluateRecruitCompanion(clan);
            SetCompanionParty(clan);
        }

        private void SetCompanionParty(Clan clan)
        {
            foreach (var companion in clan.Companions)
            {
                if ((companion.PartyBelongedTo != null && companion.PartyBelongedTo.LeaderHero == companion &&
                     !companion.IsPrisoner && companion.IsReady) ||
                    (companion.PartyBelongedTo != null && companion.PartyBelongedTo.LeaderHero.Clan == companion.Clan &&
                     !companion.IsPrisoner &&
                     companion.PartyBelongedTo.GetHeroPerkRole(companion) == SkillEffect.PerkRole.None))
                {
                    SkillEffect.PerkRole role;
                    if (companion.GetSkillValue(DefaultSkills.Medicine) >= 80)
                    {
                        role = SkillEffect.PerkRole.Surgeon;
                    }
                    else if (companion.GetSkillValue(DefaultSkills.Engineering) >= 80)
                    {
                        role = SkillEffect.PerkRole.Engineer;
                    }
                    else if (companion.GetSkillValue(DefaultSkills.Steward) >= 80)
                    {
                        role = SkillEffect.PerkRole.Quartermaster;
                    }
                    else if (companion.GetSkillValue(DefaultSkills.Scouting) >= 80)
                    {
                        role = SkillEffect.PerkRole.Scout;
                    }
                    else
                    {
                        role = SkillEffect.PerkRole.None;
                    }


                    if (clan.WarPartyComponents.Count > 0)
                    {
                        var warParty =
                            clan.WarPartyComponents.GetRandomElementWithPredicate(x => IsRoleFree(x.MobileParty, role));
                        if (warParty != null)
                        {
                            AssignToRole(warParty.MobileParty, role, companion);
                        }
                        else
                        {
                            AssignToRole(clan.WarPartyComponents.GetRandomElement().MobileParty,
                                SkillEffect.PerkRole.None, companion);
                        }
                    }
                }
            }
        }

        private bool IsRoleFree(MobileParty party, SkillEffect.PerkRole role)
        {
            if (role != SkillEffect.PerkRole.None)
            {
                if (role == SkillEffect.PerkRole.Scout)
                {
                    return party.EffectiveScout == party.LeaderHero || party.EffectiveScout == null;
                }

                if (role == SkillEffect.PerkRole.Engineer)
                {
                    return party.EffectiveEngineer == party.LeaderHero || party.EffectiveEngineer == null;
                }

                if (role == SkillEffect.PerkRole.Quartermaster)
                {
                    return party.EffectiveQuartermaster == party.LeaderHero || party.EffectiveQuartermaster == null;
                }

                if (role == SkillEffect.PerkRole.Surgeon)
                {
                    return party.EffectiveSurgeon == party.LeaderHero || party.EffectiveSurgeon == null;
                }
            }

            return true;
        }

        private void AssignToRole(MobileParty party, SkillEffect.PerkRole role, Hero hero)
        {
            AddHeroToPartyAction.Apply(hero, party, false);
            if (role == SkillEffect.PerkRole.Scout && party.EffectiveScout != party.LeaderHero)
            {
                party.SetPartyScout(hero);
            }
            else if (role == SkillEffect.PerkRole.Engineer && party.EffectiveEngineer != party.LeaderHero)
            {
                party.SetPartyEngineer(hero);
            }
            else if (role == SkillEffect.PerkRole.Quartermaster && party.EffectiveQuartermaster != party.LeaderHero)
            {
                party.SetPartyQuartermaster(hero);
            }
            else if (role == SkillEffect.PerkRole.Surgeon && party.EffectiveSurgeon != party.LeaderHero)
            {
                party.SetPartySurgeon(hero);
            }
        }

        private void EvaluateRecruitCompanion(Clan clan)
        {
            if (clan.Leader.PartyBelongedTo == null || clan.Leader.IsPrisoner ||
                clan.Companions.Count >= clan.CompanionLimit)
            {
                return;
            }

            var warParty = clan.WarPartyComponents.FirstOrDefault(x => x.Leader == clan.Leader);
            if (warParty == null || warParty.MobileParty == null)
            {
                return;
            }

            var mobileParty = warParty.MobileParty;
            if (!mobileParty.IsActive || !mobileParty.IsReady)
            {
                return;
            }

            var candidates = new List<(SkillEffect.PerkRole, float)>();

            if (IsRoleFree(mobileParty, SkillEffect.PerkRole.Scout))
            {
                candidates.Add(new ValueTuple<SkillEffect.PerkRole, float>(SkillEffect.PerkRole.Scout, 1f));
            }

            if (IsRoleFree(mobileParty, SkillEffect.PerkRole.Surgeon))
            {
                candidates.Add(new ValueTuple<SkillEffect.PerkRole, float>(SkillEffect.PerkRole.Surgeon, 1f));
            }

            if (IsRoleFree(mobileParty, SkillEffect.PerkRole.Engineer))
            {
                candidates.Add(new ValueTuple<SkillEffect.PerkRole, float>(SkillEffect.PerkRole.Engineer, 1f));
            }

            if (IsRoleFree(mobileParty, SkillEffect.PerkRole.Quartermaster))
            {
                candidates.Add(new ValueTuple<SkillEffect.PerkRole, float>(SkillEffect.PerkRole.Quartermaster, 1f));
            }

            if (candidates.Count == 0)
            {
                return;
            }

            var result = MBRandom.ChooseWeighted(candidates);
            var traits = new Dictionary<SkillEffect.PerkRole, List<TraitObject>>
            {
                {
                    SkillEffect.PerkRole.Scout,
                    new List<TraitObject>
                    {
                        DefaultTraits.WoodsScoutSkills, DefaultTraits.SteppeScoutSkills, DefaultTraits.HillScoutSkills,
                        DefaultTraits.DesertScoutSkills
                    }
                },
                {SkillEffect.PerkRole.Surgeon, new List<TraitObject> {DefaultTraits.Surgery}},
                {SkillEffect.PerkRole.Engineer, new List<TraitObject> {DefaultTraits.Siegecraft}},
                {SkillEffect.PerkRole.Quartermaster, new List<TraitObject> {DefaultTraits.Manager}}
            };

            var template = GetAdequateTemplate(traits[result], clan.Culture);
            if (template == null)
            {
                return;
            }

            var equipment = GetEquipmentIfPossible(clan, false);
            if (equipment == null)
            {
                return;
            }

            var hero = HeroCreator.CreateSpecialHero(template, null, null, null,
                Campaign.Current.Models.AgeModel.HeroComesOfAge + 5 + MBRandom.RandomInt(27));
            EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, equipment);
            hero.CompanionOf = clan;
            AssignToRole(mobileParty, result, hero);
        }

        private CharacterObject GetAdequateTemplate(List<TraitObject> traits, CultureObject culture)
        {
            CharacterObject template = null;
            foreach (var trait in traits)
            {
                if (template == null)
                {
                    template = (from x in culture.NotableAndWandererTemplates
                        where x.Occupation == Occupation.Wanderer && x.GetTraitLevel(trait) >= 2
                        select x).GetRandomElementInefficiently();
                }
            }

            return template;
        }

        private Equipment GetEquipmentIfPossible(Clan clan, bool noble, Town town = null)
        {
            var source = from e in MBObjectManager.Instance.GetObjectTypeList<MBEquipmentRoster>()
                where e.EquipmentCulture == clan.Culture
                select e;
            if (source == null)
            {
                return null;
            }

            var roster = (from e in source where e.EquipmentCulture == clan.Culture select e).ToList()
                .GetRandomElementWithPredicate(x =>
                    noble
                        ? x.HasEquipmentFlags(EquipmentFlags.IsMediumTemplate)
                        : x.StringId.Contains("bannerkings_companion"));

            if (roster == null)
            {
                return null;
            }

            if (town == null)
            {
                town = Town.AllTowns.FirstOrDefault(x => x.Culture == clan.Culture);
            }

            if (town != null)
            {
                var price = GetPrice(town.Settlement, roster);
                if (clan.Leader.Gold >= price * 2f)
                {
                    return roster.AllEquipments.GetRandomElement();
                }
            }

            return null;
        }


        private void EvaluateRecruitKnight(Clan clan)
        {
            if (clan.WarPartyComponents.Count < clan.CommanderLimit && clan.Companions.Count < clan.CompanionLimit &&
                clan.Settlements.Count(x => x.IsVillage) > 1 && clan.Influence >= BannerKingsConfig.Instance.TitleModel
                    .GetGrantKnighthoodCost(clan.Leader).ResultNumber)
            {
                var village = clan.Settlements.GetRandomElementWithPredicate(x => x.IsVillage);
                if (village == null)
                {
                    return;
                }

                var clanTitles = BannerKingsConfig.Instance.TitleManager.GetAllDeJure(clan);
                var title = BannerKingsConfig.Instance.TitleManager.GetTitle(village);
                if (clanTitles.Count == 0 || title == null || !clanTitles.Contains(title) ||
                    title.deJure != clan.Leader)
                {
                    return;
                }

                CharacterObject template;
                var genderLaw = title.contract.GenderLaw;
                if (genderLaw == GenderLaw.Agnatic)
                {
                    template = (from e in clan.Culture.NotableAndWandererTemplates
                        where e.Occupation == Occupation.Wanderer && !e.IsFemale
                        select e).GetRandomElementInefficiently();
                }

                else
                {
                    template = (from e in clan.Culture.NotableAndWandererTemplates
                        where e.Occupation == Occupation.Wanderer
                        select e).GetRandomElementInefficiently();
                }

                if (template == null)
                {
                    return;
                }

                var settlement = clan.Settlements.FirstOrDefault();
                if (settlement == null)
                {
                    settlement = Town.AllTowns.FirstOrDefault(x => x.Culture == clan.Culture).Settlement;
                }

                var source = from e in MBObjectManager.Instance.GetObjectTypeList<MBEquipmentRoster>()
                    where e.EquipmentCulture == clan.Culture
                    select e;
                if (source == null)
                {
                    return;
                }

                var roster = (from e in source
                    where e.HasEquipmentFlags(EquipmentFlags.IsMediumTemplate)
                    select e
                    into x
                    orderby MBRandom.RandomInt()
                    select x).FirstOrDefault();
                if (roster == null)
                {
                    return;
                }

                var price = GetPrice(village.Village.Bound, roster);
                if (clan.Leader.Gold >= price * 2f)
                {
                    var hero = HeroCreator.CreateSpecialHero(template, settlement, clan, null,
                        Campaign.Current.Models.AgeModel.HeroComesOfAge + 5 + MBRandom.RandomInt(27));

                    BannerKingsConfig.Instance.TitleManager.GrantKnighthood(title, hero, title.deJure);

                    EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, roster.AllEquipments.GetRandomElement());
                    var mainParty = hero.PartyBelongedTo == MobileParty.MainParty;
                    MobilePartyHelper.CreateNewClanMobileParty(hero, clan, out mainParty);
                    var component = clan.WarPartyComponents.FirstOrDefault(x => x.Leader == hero);

                    if (component != null)
                    {
                        EnterSettlementAction.ApplyForParty(component.MobileParty, settlement);
                    }
                }
            }
        }

        private float GetPrice(Settlement settlement, MBEquipmentRoster roster)
        {
            float price = 0;
            if (settlement != null)
            {
                var equip = roster.AllEquipments.GetRandomElement();
                for (var i = 0; i < 12; i++)
                {
                    var element = new EquipmentElement(equip[i].Item, equip[i].ItemModifier);
                    if (!element.IsEmpty && element.Item != null)
                    {
                        price += settlement.Town.MarketData.GetPrice(element.Item);
                    }
                }
            }

            return price * 0.5f;
        }
    }

    namespace Patches
    {
        [HarmonyPatch(typeof(ClanVariablesCampaignBehavior), "MakeClanFinancialEvaluation")]
        internal class MakeClanFinancialEvaluationPatch
        {
            private static bool Prefix(Clan clan)
            {
                if (clan.IsMinorFaction)
                {
                    return true;
                }

                if (BannerKingsConfig.Instance.TitleManager != null)
                {
                    var war = false;
                    if (clan.Kingdom != null)
                    {
                        war = FactionManager.GetEnemyKingdoms(clan.Kingdom).Count() > 0;
                    }

                    var income = Campaign.Current.Models.ClanFinanceModel.CalculateClanIncome(clan).ResultNumber *
                                 (war ? 0.5f : 0.2f);
                    if (war)
                    {
                        income += clan.Gold * 0.05f;
                    }


                    if (income > 0f)
                    {
                        var knights = 0f;
                        foreach (var partyComponent in clan.WarPartyComponents)
                        {
                            if (partyComponent.Leader != null && partyComponent.Leader != clan.Leader)
                            {
                                var title =
                                    BannerKingsConfig.Instance.TitleManager.GetHighestTitle(partyComponent.Leader);
                                if (title != null && title.fief != null)
                                {
                                    knights++;
                                    var limit = 0f;
                                    if (title.fief.IsVillage)
                                    {
                                        limit = title.fief.Village.TradeTaxAccumulated;
                                    }
                                    else if (title.fief.Town != null)
                                    {
                                        limit = Campaign.Current.Models.SettlementTaxModel
                                            .CalculateTownTax(title.fief.Town).ResultNumber;
                                    }

                                    partyComponent.MobileParty.PaymentLimit = (int) (50f + limit);
                                }
                            }
                        }

                        foreach (var partyComponent in clan.WarPartyComponents)
                        {
                            var share = income / clan.WarPartyComponents.Count - knights;
                            partyComponent.MobileParty.PaymentLimit = (int) (300f + share);
                        }

                        return false;
                    }
                }

                return true;
            }
        }


        [HarmonyPatch(typeof(ClanVariablesCampaignBehavior), "UpdateClanSettlementAutoRecruitment")]
        internal class AutoRecruitmentPatch
        {
            private static bool Prefix(Clan clan)
            {
                if (clan.MapFaction != null && clan.MapFaction.IsKingdomFaction)
                {
                    var enemies = FactionManager.GetEnemyKingdoms(clan.Kingdom);
                    foreach (var settlement in clan.Settlements)
                    {
                        if (settlement.IsFortification && settlement.Town.GarrisonParty != null)
                        {
                            if (enemies.Count() >= 0 && settlement.Town.GarrisonParty.MemberRoster.TotalManCount < 500)
                            {
                                settlement.Town.GarrisonAutoRecruitmentIsEnabled = true;
                            }

                            settlement.Town.GarrisonAutoRecruitmentIsEnabled = false;
                        }
                    }
                }

                return false;
            }
        }


        [HarmonyPatch(typeof(DefaultClanFinanceModel))]
        internal class ClanFinancesPatches
        {
            [HarmonyPrefix]
            [HarmonyPatch("AddIncomeFromKingdomBudget", MethodType.Normal)]
            private static bool KingdomBudgetPrefix(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals)
            {
                if (BannerKingsConfig.Instance.TitleManager != null)
                {
                    var title = BannerKingsConfig.Instance.TitleManager.GetHighestTitle(clan.Leader);
                    return title != null && title.contract != null &&
                           title.contract.Rights.Contains(FeudalRights.Assistance_Rights);
                }

                return true;
            }

            [HarmonyPrefix]
            [HarmonyPatch("AddIncomeFromParty", MethodType.Normal)]
            private static bool AddIncomeFromPartyPrefix(MobileParty party, Clan clan, ref ExplainedNumber goldChange,
                bool applyWithdrawals)
            {
                if (BannerKingsConfig.Instance.TitleManager != null && party.LeaderHero != null &&
                    party.LeaderHero != clan.Leader)
                {
                    return BannerKingsConfig.Instance.TitleManager.GetHighestTitle(party.LeaderHero) == null;
                }

                return true;
            }


            [HarmonyPrefix]
            [HarmonyPatch("AddExpensesFromGarrisons", MethodType.Normal)]
            private static bool GarrisonsPrefix(Clan clan, ref ExplainedNumber goldChange,
                bool applyWithdrawals = false)
            {
                if (BannerKingsConfig.Instance.TitleManager != null)
                {
                    var model = new DefaultClanFinanceModel();
                    var calculateWage = model.GetType().GetMethod("CalculatePartyWage",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    if (clan == Clan.PlayerClan)
                    {
                        Console.WriteLine();
                    }

                    foreach (var town in clan.Fiefs)
                    {
                        var garrisonParty = town.GarrisonParty;

                        if (garrisonParty != null && garrisonParty.IsActive)
                        {
                            var wage = (int) calculateWage.Invoke(model,
                                new object[] {garrisonParty, clan.Gold, applyWithdrawals});
                            if (wage > 0)
                            {
                                goldChange.Add(-wage, new TextObject("{=iPDOLbi3}Party wages {A0}"),
                                    garrisonParty.Name);
                            }
                        }
                    }

                    return false;
                }

                return true;
            }


            [HarmonyPrefix]
            [HarmonyPatch("AddExpensesFromParties", MethodType.Normal)]
            private static bool PartyExpensesPrefix(Clan clan, ref ExplainedNumber goldChange,
                bool applyWithdrawals = false)
            {
                if (BannerKingsConfig.Instance.TitleManager != null)
                {
                    var list = new List<MobileParty>();
                    foreach (var hero in clan.Lords)
                    foreach (var caravanPartyComponent in hero.OwnedCaravans)
                    {
                        list.Add(caravanPartyComponent.MobileParty);
                    }

                    foreach (var hero2 in clan.Companions)
                    foreach (var caravanPartyComponent2 in hero2.OwnedCaravans)
                    {
                        list.Add(caravanPartyComponent2.MobileParty);
                    }

                    foreach (var warPartyComponent in clan.WarPartyComponents)
                    {
                        list.Add(warPartyComponent.MobileParty);
                    }

                    var model = new DefaultClanFinanceModel();
                    var addExpense = model.GetType()
                        .GetMethod("AddPartyExpense", BindingFlags.Instance | BindingFlags.NonPublic);
                    foreach (var mobileParty in list)
                    {
                        if (mobileParty.LeaderHero != null && mobileParty.LeaderHero != clan.Leader)
                        {
                            object[] array = {mobileParty, clan, new ExplainedNumber(), applyWithdrawals};
                            addExpense.Invoke(model, array);
                            if (BannerKingsConfig.Instance.TitleManager.GetHighestTitle(mobileParty.LeaderHero) == null)
                            {
                                goldChange.Add(((ExplainedNumber) array[2]).ResultNumber,
                                    new TextObject("{=iPDOLbi3}Party wages {A0}"), mobileParty.Name);
                            }
                            else
                            {
                                var calculateWage = model.GetType().GetMethod("CalculatePartyWage",
                                    BindingFlags.Instance | BindingFlags.NonPublic);
                                var wage = (int) calculateWage.Invoke(model,
                                    new object[] {mobileParty, mobileParty.LeaderHero.Gold, applyWithdrawals});
                                if (applyWithdrawals)
                                {
                                    mobileParty.LeaderHero.Gold -= MathF.Min(mobileParty.LeaderHero.Gold, wage);
                                }
                            }
                        }
                    }

                    return false;
                }

                return true;
            }


            [HarmonyPrefix]
            [HarmonyPatch("AddVillagesIncome", MethodType.Normal)]
            private static bool VillageIncomePrefix(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals)
            {
                if (BannerKingsConfig.Instance.TitleManager != null)
                {
                    var lordships = BannerKingsConfig.Instance.TitleManager
                        .GetAllDeJure(clan)
                        .FindAll(x => x.type == TitleType.Lordship);
                    foreach (var village in clan.Villages)
                    {
                        var title = lordships.FirstOrDefault(x => x.fief.Village == village);
                        if (title == null)
                        {
                            title = BannerKingsConfig.Instance.TitleManager.GetTitle(village.Settlement);
                        }
                        else
                        {
                            lordships.Remove(title);
                        }

                        var result = CalculateVillageIncome(ref goldChange, village, clan, applyWithdrawals);

                        if (title != null)
                        {
                            var deJure = title.deJure;
                            var knightOwned = title.deJure != clan.Leader && title.deJure.Clan == clan;
                            if (knightOwned)
                            {
                                deJure.Gold += result;
                                continue;
                            }

                            if (deJure.Clan.Kingdom == clan.Kingdom)
                            {
                                continue;
                            }
                        }

                        goldChange.Add(result, new TextObject("{=!}{A0}"), village.Name);
                    }

                    foreach (var lordship in lordships)
                    {
                        var village = lordship.fief.Village;
                        var ownerClan = village.Settlement.OwnerClan;
                        if (ownerClan.Kingdom == clan.Kingdom)
                        {
                            var result = CalculateVillageIncome(ref goldChange, village, clan, applyWithdrawals);
                            var leaderOwned = lordship.deJure == clan.Leader;
                            if (!leaderOwned)
                            {
                                var deJure = lordship.deJure;
                                deJure.Gold += result;
                            }
                            else
                            {
                                goldChange.Add(result, new TextObject("{=!}{A0}"), village.Name);
                            }
                        }
                    }

                    return false;
                }

                return true;
            }

            private static int CalculateVillageIncome(ref ExplainedNumber goldChange, Village village, Clan clan,
                bool applyWithdrawals)
            {
                var total = village.VillageState == Village.VillageStates.Looted ||
                            village.VillageState == Village.VillageStates.BeingRaided
                    ? 0
                    : (int) (village.TradeTaxAccumulated / 5f);
                var num2 = total;
                if (clan.Kingdom != null && clan.Kingdom.RulingClan != clan &&
                    clan.Kingdom.ActivePolicies.Contains(DefaultPolicies.LandTax))
                {
                    total += (int) (-(float) total * 0.05f);
                }

                if (village.Bound.Town != null && village.Bound.Town.Governor != null &&
                    village.Bound.Town.Governor.GetPerkValue(DefaultPerks.Scouting.ForestKin))
                {
                    total += MathF.Round(total * DefaultPerks.Scouting.ForestKin.SecondaryBonus * 0.01f);
                }

                var bound = village.Bound;
                bool flag;
                if (bound == null)
                {
                    flag = null != null;
                }
                else
                {
                    var town = bound.Town;
                    flag = (town != null ? town.Governor : null) != null;
                }

                if (flag && village.Bound.Town.Governor.GetPerkValue(DefaultPerks.Steward.Logistician))
                {
                    total += MathF.Round(total * DefaultPerks.Steward.Logistician.SecondaryBonus * 0.01f);
                }

                if (applyWithdrawals)
                {
                    village.TradeTaxAccumulated -= num2;
                }

                if (clan.Kingdom != null && clan.Kingdom.RulingClan == clan &&
                    clan.Kingdom.ActivePolicies.Contains(DefaultPolicies.LandTax))
                {
                    if (!village.IsOwnerUnassigned && village.Settlement.OwnerClan != clan)
                    {
                        var policyTotal =
                            village.VillageState == Village.VillageStates.Looted ||
                            village.VillageState == Village.VillageStates.BeingRaided
                                ? 0
                                : (int) (village.TradeTaxAccumulated / 5f);
                        total += (int) (policyTotal * 0.05f);
                    }
                }

                return total;
            }
        }
    }
}