using System.Collections.Generic;
using Mvp.Shared;

namespace Mvp.CommanderSelect
{
    /// <summary>
    /// Source of commander definitions. Six commanders, each with an exclusive
    /// mechanic tag (frenzy / bulwark / lethality / scorch / mercenary / frost),
    /// two exclusive archetypes, and 20 exclusive trait cards.
    /// </summary>
    public static class CommanderCatalog
    {
        static readonly CommanderDefinition[] All =
        {
            BuildElena(),
            BuildCassian(),
            BuildVera(),
            BuildOlivia(),
            BuildDario(),
            BuildIvan()
        };

        public static IReadOnlyList<CommanderDefinition> GetAll()
        {
            return All;
        }

        public static CommanderDefinition GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id) return All[i];
            }
            return null;
        }

        static CommanderDefinition BuildElena()
        {
            var c = new CommanderDefinition
            {
                Id = "commander_elena",
                DisplayName = "伊莲娜",
                Title = "绯红猎犬",
                ExclusiveTag = "frenzy",
                MaxHealth = 100,
                CurrentHealth = 86,
                PortraitAssetId = "Battle/UI/CommanderPortraits/elena_portrait",
                MapPortraitAssetId = "Battle/UI/CommanderMarkers/elena_map_marker"
            };
            c.AffinityArchetypeIds.Add("archetype_frenzy_burst");
            c.AffinityArchetypeIds.Add("archetype_frenzy_sustain");
            c.Traits.Add("勇敢");
            c.Traits.Add("谨慎");
            c.Traits.Add("鼓舞");
            c.Traits.Add("坚韧");
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Infantry, 2));
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Tank, 1));
            AddIds(c,
                "trait_elena_bloodthirst", "trait_elena_never_fall", "trait_elena_blood_price",
                "trait_elena_berserk_moment", "trait_elena_battle_blood", "trait_elena_hunt_instinct",
                "trait_elena_bloodstream", "trait_elena_war_frenzy", "trait_elena_blood_rush",
                "trait_elena_blood_suppress", "trait_elena_fight_stronger", "trait_elena_deep_grudge",
                "trait_elena_break_pot", "trait_elena_blood_heat", "trait_elena_stop_loss",
                "trait_elena_blood_instinct", "trait_elena_death_throes", "trait_elena_berserk_speed",
                "trait_elena_blood_pact", "trait_elena_cornered");
            return c;
        }

        static CommanderDefinition BuildCassian()
        {
            var c = new CommanderDefinition
            {
                Id = "commander_cassian",
                DisplayName = "卡西安",
                Title = "铁壁总督",
                ExclusiveTag = "bulwark",
                MaxHealth = 120,
                CurrentHealth = 112,
                PortraitAssetId = "Battle/UI/CommanderPortraits/cassian_portrait",
                MapPortraitAssetId = "Battle/UI/CommanderMarkers/cassian_map_marker"
            };
            c.AffinityArchetypeIds.Add("archetype_bulwark_shield");
            c.AffinityArchetypeIds.Add("archetype_bulwark_thorns");
            c.Traits.Add("沉着");
            c.Traits.Add("守备");
            c.Traits.Add("纪律");
            c.Traits.Add("坚守");
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Infantry, 3));
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Tank, 1));
            AddIds(c,
                "trait_cassian_iron_wall", "trait_cassian_thorn_reflect", "trait_cassian_hold_line",
                "trait_cassian_immovable", "trait_cassian_iron_formation", "trait_cassian_shield_wall",
                "trait_cassian_counter_hammer", "trait_cassian_body_guard", "trait_cassian_trade_defense",
                "trait_cassian_heavy_crush", "trait_cassian_copper_wall", "trait_cassian_unyielding",
                "trait_cassian_iron_shell", "trait_cassian_shield_bash", "trait_cassian_shield_bracket",
                "trait_cassian_line_fortify", "trait_cassian_tight_formation",
                "trait_cassian_shoulder_reflect", "trait_cassian_rock_firm", "trait_cassian_rest_work");
            return c;
        }

        static CommanderDefinition BuildVera()
        {
            var c = new CommanderDefinition
            {
                Id = "commander_vera",
                DisplayName = "薇拉",
                Title = "风隼",
                ExclusiveTag = "lethality",
                MaxHealth = 95,
                CurrentHealth = 90,
                PortraitAssetId = "Battle/UI/CommanderPortraits/vera_portrait",
                MapPortraitAssetId = "Battle/UI/CommanderMarkers/vera_map_marker"
            };
            c.AffinityArchetypeIds.Add("archetype_lethality_crit");
            c.AffinityArchetypeIds.Add("archetype_lethality_execute");
            c.Traits.Add("迅捷");
            c.Traits.Add("疾攻");
            c.Traits.Add("迂回");
            c.Traits.Add("均衡");
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Infantry, 3));
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Tank, 1));
            AddIds(c,
                "trait_vera_lethal_blow", "trait_vera_beheading", "trait_vera_shadow_strike",
                "trait_vera_death_descend", "trait_vera_fatal_weakness", "trait_vera_falcon_dive",
                "trait_vera_ruthless_chase", "trait_vera_weakness_insight", "trait_vera_shadow_step",
                "trait_vera_poison_blade", "trait_vera_gale_combo", "trait_vera_hunt_moment",
                "trait_vera_cold_blood", "trait_vera_quick_hand", "trait_vera_light_march",
                "trait_vera_throat_cut", "trait_vera_first_strike", "trait_vera_gap_catch",
                "trait_vera_shadow_weave", "trait_vera_ambush");
            return c;
        }

        static CommanderDefinition BuildOlivia()
        {
            var c = new CommanderDefinition
            {
                Id = "commander_olivia",
                DisplayName = "奥莉薇",
                Title = "鹰眼",
                ExclusiveTag = "scorch",
                MaxHealth = 105,
                CurrentHealth = 100,
                PortraitAssetId = "Battle/UI/CommanderPortraits/olivia_portrait",
                MapPortraitAssetId = "Battle/UI/CommanderMarkers/olivia_map_marker"
            };
            c.AffinityArchetypeIds.Add("archetype_scorch_burn");
            c.AffinityArchetypeIds.Add("archetype_scorch_range");
            c.Traits.Add("坚韧");
            c.Traits.Add("厚重");
            c.Traits.Add("号令");
            c.Traits.Add("支援");
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Infantry, 2));
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Tank, 2));
            AddIds(c,
                "trait_olivia_scorched_hell", "trait_olivia_beyond_range", "trait_olivia_fire_suppress",
                "trait_olivia_doom_judgment", "trait_olivia_flame_storm", "trait_olivia_far_eye",
                "trait_olivia_sustain_burn", "trait_olivia_burn_out", "trait_olivia_sniper_standby",
                "trait_olivia_fire_arrow", "trait_olivia_barrage_suppress", "trait_olivia_arsonist",
                "trait_olivia_burn_shell", "trait_olivia_eye_aim", "trait_olivia_fire_seed",
                "trait_olivia_scorch_spread", "trait_olivia_long_shot", "trait_olivia_rocket_volley",
                "trait_olivia_ignite", "trait_olivia_fireline_suppress");
            return c;
        }

        static CommanderDefinition BuildDario()
        {
            var c = new CommanderDefinition
            {
                Id = "commander_dario",
                DisplayName = "达里奥",
                Title = "佣兵团长",
                ExclusiveTag = "mercenary",
                MaxHealth = 90,
                CurrentHealth = 85,
                PortraitAssetId = "Battle/UI/CommanderPortraits/dario_portrait",
                MapPortraitAssetId = "Battle/UI/CommanderMarkers/dario_map_marker"
            };
            c.AffinityArchetypeIds.Add("archetype_mercenary_snowball");
            c.AffinityArchetypeIds.Add("archetype_mercenary_goldpower");
            c.Traits.Add("军心");
            c.Traits.Add("均衡");
            c.Traits.Add("号令");
            c.Traits.Add("谨慎");
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Infantry, 2));
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Tank, 1));
            AddIds(c,
                "trait_dario_mercenary_band", "trait_dario_war_loot", "trait_dario_commission",
                "trait_dario_arms_trade", "trait_dario_bounty", "trait_dario_usury",
                "trait_dario_elite_mercenary", "trait_dario_field_broker", "trait_dario_insurance",
                "trait_dario_armament", "trait_dario_bounty_hunter", "trait_dario_market_sense",
                "trait_dario_small_business", "trait_dario_body_fee", "trait_dario_thrifty",
                "trait_dario_money_makes", "trait_dario_pay", "trait_dario_stockpile",
                "trait_dario_reward_hunter", "trait_dario_hire_kill");
            return c;
        }

        static CommanderDefinition BuildIvan()
        {
            var c = new CommanderDefinition
            {
                Id = "commander_ivan",
                DisplayName = "伊凡",
                Title = "夜鸦",
                ExclusiveTag = "frost",
                MaxHealth = 110,
                CurrentHealth = 105,
                PortraitAssetId = "Battle/UI/CommanderPortraits/ivan_portrait",
                MapPortraitAssetId = "Battle/UI/CommanderMarkers/ivan_map_marker"
            };
            c.AffinityArchetypeIds.Add("archetype_frost_burstcontrol");
            c.AffinityArchetypeIds.Add("archetype_frost_zone");
            c.Traits.Add("沉着");
            c.Traits.Add("深垒");
            c.Traits.Add("充沛");
            c.Traits.Add("拒止");
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Infantry, 2));
            c.StartingUnits.Add(new StartingUnitEntry(UnitType.Tank, 2));
            AddIds(c,
                "trait_ivan_frost_nova", "trait_ivan_frozen_zone", "trait_ivan_frost_pact",
                "trait_ivan_absolute_zero", "trait_ivan_frost_imprison", "trait_ivan_frozen_earth",
                "trait_ivan_frost_blade", "trait_ivan_ice_body", "trait_ivan_frost_spread",
                "trait_ivan_frozen_prey", "trait_ivan_north_wind", "trait_ivan_ice_shard",
                "trait_ivan_chill", "trait_ivan_frost_road", "trait_ivan_frostbite",
                "trait_ivan_cold_wind", "trait_ivan_ice_spike", "trait_ivan_frost_armor",
                "trait_ivan_ice_stab", "trait_ivan_night_hunt");
            return c;
        }

        static void AddIds(CommanderDefinition c, params string[] ids)
        {
            for (int i = 0; i < ids.Length; i++)
                if (!string.IsNullOrEmpty(ids[i])) c.ExclusiveTraitIds.Add(ids[i]);
        }
    }
}
