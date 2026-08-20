using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using SeraphLeveling.Config;
using SeraphLeveling.Messages;
using SeraphLeveling.Patches;
using SeraphLeveling.Tests;
using SeraphLeveling.Data;
using SeraphLeveling.Data.Traits;
using SeraphLeveling.Data.Mods;
using SeraphLeveling.Data.Attributes;
using SeraphLeveling.Data.Legacy;
using Vintagestory.API.Util;
using Microsoft.CSharp.RuntimeBinder;
using System.Text.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using Vintagestory.API.Datastructures;

namespace SeraphLeveling
{
    public class SeraphLevelingModSystem : ModSystem
    {
        public static ICoreServerAPI ServerApi { get; private set; }
        public static SeraphLevelingModSystem Instance { get; private set; }

        // Keys for mining progression system
        public const string BLOCKS_MINED_KEY = "sitBlocksMined";
        public const string MINING_STAT_CODE = "sitMiningBonus";
        // WatchedAttributes keys for client sync
        public const string WATCHED_MINING_LEVEL = "sitMiningLevel";
        public const string WATCHED_MINING_BONUS = "sitMiningBonusPercent";

        // Trait code for the mining mastery trait
        public const string MINING_TRAIT_CODE = "sitminingmastery";

        // Mining progression configuration
        // Base blocks for first 1%: 100 blocks
        // Each subsequent 1% requires +100 more blocks (100, 200, 300, etc.)
        // Switching pickaxe types resets the increment counter back to base
        public static int OreMultiplier = 5;             // Ore blocks count for 5x points

        // Keys for melee damage progression system
        public const string MELEE_DAMAGE_KEY = "sitMeleeDamage";
        public const string MELEE_STAT_CODE = "sitMeleeBonus";
        // WatchedAttributes keys for client sync (melee)
        public const string WATCHED_MELEE_LEVEL = "sitMeleeLevel";
        public const string WATCHED_MELEE_BONUS = "sitMeleeBonusPercent";

        // Trait code for the melee mastery trait (Soldier)
        public const string MELEE_TRAIT_CODE = "sitmeleemastery";

        // Melee damage progression configuration
        // Base damage for first 1%: 100 damage
        // Each subsequent 1% requires +100 more damage (100, 200, 300, etc.)
        // Switching weapon types resets the increment counter back to base
        public static int BaseDamagePerIncrement = 100;   // Base damage needed for first credit
        public static int MeleeIncrementStep = 100;       // How much more damage each subsequent credit needs
        public static int MaxMeleeDamagePercent = 50;     // 50% max bonus

        // Vanilla Soldier trait melee damage bonus (used for cap calculations)
        public const int VANILLA_SOLDIER_MELEE_BONUS = 30;

        // Keys for ranged damage progression system
        public const string RANGED_DAMAGE_KEY = "sitRangedDamage";
        public const string RANGED_DAMAGE_STAT_CODE = "sitRangedDamageBonus";
        public const string RANGED_ACCURACY_STAT_CODE = "sitRangedAccuracyBonus";
        public const string RANGED_DISTANCE_STAT_CODE = "sitRangedDistanceBonus";
        // WatchedAttributes keys for client sync (ranged)
        public const string WATCHED_RANGED_LEVEL = "sitRangedLevel";
        public const string WATCHED_RANGED_DAMAGE_BONUS = "sitRangedDamageBonusPercent";
        public const string WATCHED_RANGED_ACCURACY_BONUS = "sitRangedAccuracyBonusPercent";
        public const string WATCHED_RANGED_DISTANCE_BONUS = "sitRangedDistanceBonusPercent";

        // Trait code for the ranged mastery trait (Focused)
        public const string RANGED_TRAIT_CODE = "sitrangedmastery";

        // Ranged damage progression configuration
        // Base damage for first 1%: 100 damage
        // Each subsequent 1% requires +100 more damage (100, 200, 300, etc.)
        // Switching weapon combinations resets the increment counter back to base
        public static int BaseRangedDamagePerIncrement = 100;   // Base damage needed for first credit
        public static int RangedIncrementStep = 100;             // How much more damage each subsequent credit needs
        public static int MaxRangedDamagePercent = 50;           // 50% max bonus for damage
        public static int MaxRangedAccuracyPercent = 50;         // 50% max bonus for accuracy
        public static int MaxRangedDistancePercent = 50;         // 50% max bonus for distance

        // WatchedAttributes keys for client sync (walking)
        public const string WATCHED_WALKING_LEVEL = "sitWalkingLevel";
        public const string WATCHED_WALKING_BONUS = "sitWalkingBonusPercent";

        // Tracking last known positions for walking distance calculation (using Position2D to avoid Vec3d allocations)
        private static ConcurrentDictionary<string, Position2D> lastPlayerPositions = new ConcurrentDictionary<string, Position2D>();

        // Maximum distance per tick to count (prevents teleportation from counting)
        private const float MAX_DISTANCE_PER_TICK = 10f;

        // Cache for vanilla trait checks - populated once on player join
        private static ConcurrentDictionary<string, CachedVanillaTraits> VanillaTraitsCache = new ConcurrentDictionary<string, CachedVanillaTraits>();

        // WatchedAttributes keys for client sync (hunger)
        public const string WATCHED_HUNGER_LEVEL = "sitHungerLevel";
        public const string WATCHED_HUNGER_BONUS = "sitHungerBonusPercent";

        // Vanilla Ravenous trait hunger rate increase (used for cap calculations)
        // Blackguard has +30% hunger rate, so earning 25% brings them back to nearly normal
        public const int VANILLA_RAVENOUS_HUNGER_PENALTY = 30;
        public const string WATCHED_RAVENOUS_REMAINING = "sitRavenousRemaining";

        // WatchedAttributes keys for client sync (armor)
        public const string WATCHED_ARMOR_DURABILITY_LEVEL = "sitArmorDurabilityLevel";
        public const string WATCHED_ARMOR_DURABILITY_BONUS = "sitArmorDurabilityBonusPercent";
        public const string WATCHED_ARMOR_WALKSPEED_LEVEL = "sitArmorWalkSpeedLevel";
        public const string WATCHED_ARMOR_WALKSPEED_BONUS = "sitArmorWalkSpeedBonusPercent";

        // Armor progression configuration
        // Time-based progression: 1 VS day (48 min) base, +1 VS day increment per credit (gives -1% walk speed penalty per credit)
        public static int BaseSecondsInArmorPerIncrement = 2880;  // Base seconds (1 VS day = 48 min) for first credit
        public static int ArmorTimeIncrementStep = 2880;          // How many more seconds each subsequent credit needs (1 VS day)

        // Damage-based progression: 100 damage base, +100 increment per credit (gives +1% durability per credit)
        public static int BaseDamageBlockedPerIncrement = 100;     // Base damage blocked for first credit
        public static int ArmorDamageIncrementStep = 100;          // How much more damage each subsequent credit needs

        // Repair-based progression: 1 repair base, +1 increment per credit (gives +1% durability per credit)
        public static int BaseRepairsPerIncrement = 1;             // Base repairs for first credit
        public static int ArmorRepairIncrementStep = 1;            // How many more repairs each subsequent credit needs

        // First-equip bonuses (durability):
        // +1% for light armor and chain, +2% for brigandine, +3% for scale and plate
        // Now configurable via config file
        public static int FirstEquipLightBonus = 1;
        public static int FirstEquipChainBonus = 1;
        public static int FirstEquipBrigandineBonus = 2;
        public static int FirstEquipScaleBonus = 3;
        public static int FirstEquipPlateBonus = 3;

        // First-equip bonuses (walk speed penalty reduction):
        // Same values as durability - grants walk speed bonus on first equip
        // Now configurable via config file
        public static int FirstEquipWalkSpeedLightBonus = 1;
        public static int FirstEquipWalkSpeedChainBonus = 1;
        public static int FirstEquipWalkSpeedBrigandineBonus = 2;
        public static int FirstEquipWalkSpeedScaleBonus = 3;
        public static int FirstEquipWalkSpeedPlateBonus = 3;

        // Max bonuses
        public static int MaxArmorDurabilityPercent = 50;          // 50% max armor durability bonus
        public static int MaxArmorWalkSpeedPercent = 50;           // 50% max walk speed penalty reduction

        // Optional armor features (disabled by default)
        public static bool EnableArmorHungerReduction = false;     // If true, armor time grants hunger rate reduction
        public static int MaxArmorHungerReductionPercent = 50;     // Max hunger rate reduction from armor
        public static bool EnableArmorHealingBonus = false;        // If true, armor time grants healing effectiveness
        public static int MaxArmorHealingPercent = 25;             // Max healing effectiveness from armor


        // Vanilla Soldier trait armor bonuses (used for cap calculations)
        public const int VANILLA_SOLDIER_ARMOR_DURABILITY_BONUS = 15;
        public const int VANILLA_SOLDIER_ARMOR_WALKSPEED_BONUS = 25;

        // Tracking currently equipped armor for each player (for time tracking and equip detection)
        private static ConcurrentDictionary<string, Dictionary<string, string>> playerEquippedArmor = new ConcurrentDictionary<string, Dictionary<string, string>>();

        // =========================================================================
        // CLOTHIER TRAIT - Tracks unique clothing worn to unlock sewing kit crafting
        // =========================================================================

        // Clothier progression configuration
        public static void InitializeClothierBlacklistedItems(ICoreAPI api)
        {
            bool hasSacredLib = DetectAnySacredLib(api.ModLoader);
            api.Logger.Notification($"[SeraphLeveling] Initializing Clothier Blacklisted Items. Sacred Classes compatibility enabled: {hasSacredLib}");
            AttributeModifierDefinitions.Clothier.TokenBanList = hasSacredLib ?
            [
                // Woodsman
                "clothes-neck-acorn-amulet", "clothes-waist-sturdy-leather-belt", "clothes-shoulder-patchwork", "clothes-upperbody-survivor", "clothes-foot-high-leather-boots", "clothes-lowerbody-workmans-gown",
                // Craftsman
                "clothes-nadiya-neck-flint-amulet", "clothes-shoulder-clockmaker-apron", "clothes-upperbody-chemiseshoulderlong", "clothes-lowerbody-centurion", "clothes-foot-tigh-high-boots",
                // Witch
                "clothes-arm-fortuneteller", "clothes-foot-fortuneteller", "clothes-lowerbody-skirt", "clothes-upperbody-fortuneteller", "clothes-upperbodyover-fortuneteller","clothes-head-fortune-tellers-scarf",
                // Blacksmith
                "clothes-nadiya-face-blacksmith", "clothes-nadiya-shoulder-blacksmith", "clothes-nadiya-upperbody-blacksmith", "clothes-nadiya-lowerbody-blacksmith", "clothes-nadiya-foot-blacksmith", "sacredlib:blacksmithgloves-plain",
                // Artificer
                "clothes-face-glasses-clockmaker", "clothes-waist-clockmaker-belt", "clothes-upperbody-clockmaker-shirt", "clothes-lowerbody-clockmaker-pants", "clothes-foot-metalcap-boots",
                // Miner
                "clothes-face-miner", "clothes-upperbody-miner", "clothes-lowerbody-miner", "clothes-shoulder-miner", "clothes-upperbodyover-miner", "clothes-foot-miner",
                // Homesteader
                "clothes-nadiya-face-grain", "clothes-head-straw-hat", "clothes-neck-ruralfarmer", "clothes-arm-ruralfarmer", "clothes-upperbody-ruralfarmer", "clothes-lowerbody-raw-hide-trousers", "clothes-waist-ruralfarmer", "clothes-foot-ruralfarmer",
                // Huntsman
                "clothes-face-hunter-mask", "clothes-waist-ruralhunter", "clothes-shoulder-ruralhunter", "clothes-upperbody-hunters-green", "clothes-lowerbody-arctichunter", "clothes-foot-ruralhunter",
                // Guardsman
                "clothes-face-leather-reinforced-mask", "clothes-emblem-silver-pin", "clothes-upperbody-hunter-shirt", "clothes-upperbodyover-forgotten", "clothes-lowerbody-commoner-trousers", "clothes-hand-heavy-leather-gloves", "clothes-foot-survivor",
                // Hearthmaster
                "clothes-upperbody-commoner-shirt", "clothes-face-headbandfabric", "clothes-waist-peasantapron", "clothes-lowerbody-workmans-gown", "clothes-foot-soldier-boots",
                // Haberdasher
                "clothes-waist-merchant-belt", "clothes-upperbody-midsummer", "clothes-shoulder-woolen-scarf", "clothes-neck-pomander", "clothes-lowerbody-arcticfisher", "clothes-head-lackey-hat", "clothes-foot-hobnailboots", "clothes-hand-cuffsred", "clothes-arm-tailor-needlepuff",
                // Zealot
                "clothes-face-blindfold", "clothes-shoulder-rotwalker", "clothes-upperbody-woolen-shirt", "clothes-lowerbody-rotwalker", "clothes-foot-rusty-ankle-manacles", "clothes-arm-rusty-wrist-manacles"
            ]
            :
            [
                // Hunter
                "clothes-upperbody-hunter-shirt", "clothes-upperbodyover-hunter-coat", "clothes-shoulder-hunter-poncho",
                "clothes-lowerbody-hunter-leggings", "clothes-foot-hunter-boots", "clothes-hand-hunter-gloves",
                "clothes-head-hunter-hood", "clothes-face-hunter-mask",
                // Tailor
                "clothes-upperbody-tailor-blouse", "clothes-foot-tailor-shoes", "clothes-hand-tailor-gloves",
                "clothes-waist-tailor-belt", "clothes-shoulder-tailor-jacket",
                // Malefactor
                "clothes-shoulder-malefactor-cloak", "clothes-foot-malefactor-boots", "clothes-hand-malefactor-gloves",
                "clothes-lowerbody-malefactor-trousers", "clothes-neck-malefactor-pendant",
                // Blackguard
                "clothes-foot-blackguard-shoes", "clothes-lowerbody-blackguard-leggings",
                "clothes-upperbody-blackguard-shirt", "clothes-waist-blackguard-belt",
                // Clockmaker
                "clothes-hand-clockmaker-wristguard", "clothes-foot-clockmaker-shoes",
                "clothes-upperbody-clockmaker-shirt", "clothes-shoulder-clockmaker-apron", "clothes-upperbodyover-clockmaker-tunic",
                // Commoner
                "clothes-upperbody-commoner-shirt", "clothes-upperbodyover-commoner-coat",
                "clothes-lowerbody-commoner-trousers", "clothes-foot-commoner-boots", "clothes-hand-commoner-gloves"
            ];

        }

        // =========================================================================
        // MENDER TRAIT - Tracks sewing kit repairs for durability bonus
        // =========================================================================
        public const string WATCHED_MENDER_LEVEL = "sitMenderLevel";
        public const string WATCHED_MENDER_BONUS = "sitMenderBonusPercent";

        // Mender progression configuration
        public static int BaseMenderRepairsPerIncrement = 5;   // Base repairs for first credit
        public static int MenderIncrementStep = 1;              // Increment step per credit
        public static int MaxMenderPercent = 25;                // 25% total cap (matches vanilla Mender +25% so Tailor and non-Tailor end up equal)

        // Vanilla Mender trait bonus (used for cap calculations)
        // Vanilla Mender shows "+25% armor durability" (armorDurabilityLoss: -0.25). This is
        // used both for cap math (Tailor's earnable = MaxMenderPercent - 25, so total caps at
        // MaxMenderPercent like every other class) and for inline display Replace.
        public const int VANILLA_MENDER_ARMOR_DURABILITY_BONUS = 25;

        // Durability tracking for repair detection - key is "playerUid_slotId", value is last known durability
        private static ConcurrentDictionary<string, int> TrackedItemDurabilities = new ConcurrentDictionary<string, int>();

        // Sewing kit consumption tracking - key is playerUid, value is last known sewing kit count on mouse cursor
        private static ConcurrentDictionary<string, int> TrackedSewingKitCounts = new ConcurrentDictionary<string, int>();

        // =========================================================================
        // PILFERER TRAIT - Tracks chests/vessels for loot bonuses
        // =========================================================================
        public const string WATCHED_PILFERER_LEVEL = "sitPilfererLevel";
        // Per-stat displayed bonuses. Pilferer's three stats have different vanilla values
        // (vessel +15%, rusty gear +10%, whole vessel +12%), so a single shared bonus value
        // can't drive all three to the same cap simultaneously for Malefactor (vanilla
        // Pilferer). Tracking each stat's earned amount independently keeps every class at
        // exactly +20% per stat at maxall.
        public const string WATCHED_PILFERER_VESSEL_BONUS = "sitPilfererVesselBonus";
        public const string WATCHED_PILFERER_RUSTY_BONUS = "sitPilfererRustyBonus";
        public const string WATCHED_PILFERER_WHOLE_BONUS = "sitPilfererWholeBonus";

        // Pilferer progression configuration
        public static int BasePilfererPointsPerIncrement = 10;  // Base points for first credit
        public static int PilfererIncrementStep = 10;           // Increment step per credit
        public static int MaxPilfererPercent = 20;              // 20% max bonus for all three stats
        public const int PILFERER_VESSEL_POINTS = 2;            // Points per broken loot vessel

        // Vanilla Pilferer trait bonuses (Malefactor exclusive)
        public const int VANILLA_PILFERER_RUSTY_GEAR_BONUS = 10;
        public const int VANILLA_PILFERER_VESSEL_CONTENTS_BONUS = 15;
        public const int VANILLA_PILFERER_WHOLE_VESSEL_BONUS = 12;

        // =========================================================================
        // RESOURCEFUL TRAIT - Tracks animal harvesting for loot/speed bonuses
        // =========================================================================
        public const string WATCHED_RESOURCEFUL_LEVEL = "sitResourcefulLevel";
        public const string WATCHED_RESOURCEFUL_LOOT_BONUS = "sitResourcefulLootBonusPercent";
        public const string WATCHED_RESOURCEFUL_SPEED_BONUS = "sitResourcefulSpeedBonusPercent";

        // Resourceful progression configuration
        public static int BaseResourcefulAnimalsPerIncrement = 10;  // Base animals for first credit
        public static int ResourcefulIncrementStep = 10;            // Increment step per credit
        public static int MaxResourcefulLootPercent = 20;           // 20% max animal loot bonus
        public static int MaxResourcefulSpeedPercent = 25;          // 25% max harvesting speed bonus

        // Vanilla Resourceful trait bonuses (Hunter/Malefactor)
        public const int VANILLA_RESOURCEFUL_LOOT_BONUS = 10;
        public const int VANILLA_RESOURCEFUL_SPEED_BONUS = 25;

        // =========================================================================
        // FORAGER TRAIT - Tracks wild crop breaking for foraging loot bonuses
        // =========================================================================
        public const string FORAGER_LOOT_STAT_CODE = "sitForagerLoot";
        public const string FORAGER_WILD_CROP_STAT_CODE = "sitForagerWildCrop";
        public const string WATCHED_FORAGER_LEVEL = "sitForagerLevel";
        public const string WATCHED_FORAGER_LOOT_BONUS = "sitForagerLootBonusPercent";
        public const string WATCHED_FORAGER_WILD_CROP_BONUS = "sitForagerWildCropBonusPercent";
        public const string FORAGER_TRAIT_CODE = "sitforagermastery";

        // Forager progression configuration
        public static int BaseForagerCropsPerIncrement = 10;    // Base crops for first credit
        public static int ForagerIncrementStep = 10;            // Increment step per credit
        public static int MaxForagerLootPercent = 20;           // 20% max foraging loot bonus
        public static int MaxForagerWildCropPercent = 20;       // 20% max wild crop drop bonus

        // Vanilla Forager trait bonuses (Hunter/Malefactor)
        public const int VANILLA_FORAGER_LOOT_BONUS = 10;
        public const int VANILLA_FORAGER_WILD_CROP_BONUS = 20;

        // =========================================================================
        // FURTIVE TRAIT - Tracks sneaking blocks for animal detection range reduction
        // =========================================================================
        public const string WATCHED_FURTIVE_LEVEL = "sitFurtiveLevel";
        public const string WATCHED_FURTIVE_BONUS = "sitFurtiveBonusPercent";

        // Vanilla Furtive trait bonus (Malefactor)
        public const int VANILLA_FURTIVE_DETECTION_REDUCTION = 35;

        // Storage for furtive progress
        // Tracking last known positions for sneaking distance calculation (using Position2D to avoid Vec3d allocations)
        private static ConcurrentDictionary<string, Position2D> lastSneakingPositions = new ConcurrentDictionary<string, Position2D>();

        // =========================================================================
        // PRECISE TRAIT - Tracks damage to mechanicals for damage bonus
        // =========================================================================
        public const string PRECISE_STAT_CODE = "sitPreciseBonus";
        public const string WATCHED_PRECISE_LEVEL = "sitPreciseLevel";
        public const string WATCHED_PRECISE_BONUS = "sitPreciseBonusPercent";
        public const string PRECISE_TRAIT_CODE = "sitprecisemastery";

        // Precise progression configuration
        public static int BasePreciseDamagePerIncrement = 100;  // Base damage for first credit
        public static int PreciseIncrementStep = 100;            // Increment step per credit
        public static int MaxPrecisePercent = 30;                // 30% max damage bonus to mechanicals

        // Vanilla Precise trait bonus (Clockmaker)
        public const int VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS = 25;

        // =========================================================================
        // MERCILESS TRAIT - Unlocks shortsword/shield after armor + melee thresholds
        // =========================================================================
        public const string MERCILESS_STAT_CODE = "sitMercilessBonus";
        public const string WATCHED_MERCILESS_UNLOCKED = "sitMercilessUnlocked";

        // =========================================================================
        // NEGATIVE TRAIT CONSTANTS - Used for cancellation calculations
        // =========================================================================

        // Farsighted (Hunter): -15% melee damage
        public const int VANILLA_FARSIGHTED_MELEE_PENALTY = 15;
        public const string WATCHED_FARSIGHTED_REMAINING = "sitFarsightedRemaining";

        // Nervous (Malefactor, Clockmaker): -15% melee damage
        public const int VANILLA_NERVOUS_MELEE_PENALTY = 15;
        public const string WATCHED_NERVOUS_REMAINING = "sitNervousRemaining";

        // Nearsighted (Blackguard): -15% ranged damage
        public const int VANILLA_NEARSIGHTED_RANGED_PENALTY = 15;
        public const string WATCHED_NEARSIGHTED_REMAINING = "sitNearsightedRemaining";

        // Frail (Malefactor, Clockmaker): -2.5 HP, -25% ranged distance
        public const int VANILLA_FRAIL_DISTANCE_PENALTY = 25;

        // Civil (Tailor): -10% loot from foraging
        public const int VANILLA_CIVIL_FORAGING_PENALTY = 10;
        public const string WATCHED_CIVIL_REMAINING = "sitCivilRemaining";

        // Weak (Tailor): -2 HP, -10% mining speed
        public const int VANILLA_WEAK_MINING_PENALTY = 10;
        public const string WATCHED_WEAK_HP_REMAINING = "sitWeakHpRemaining";
        public const string WATCHED_WEAK_MINING_REMAINING = "sitWeakMiningRemaining";

        // Kind (Tailor): -10% animal loot, -25% harvesting speed
        public const int VANILLA_KIND_LOOT_PENALTY = 10;
        public const int VANILLA_KIND_SPEED_PENALTY = 25;
        public const string WATCHED_KIND_LOOT_REMAINING = "sitKindLootRemaining";
        public const string WATCHED_KIND_SPEED_REMAINING = "sitKindSpeedRemaining";

        // Heavyhanded (Blackguard): -10% vessel loot, -15% foraging, -20% wild crop
        public const int VANILLA_HEAVYHANDED_VESSEL_PENALTY = 10;
        public const int VANILLA_HEAVYHANDED_FORAGING_PENALTY = 15;
        public const int VANILLA_HEAVYHANDED_WILD_CROP_PENALTY = 20;
        public const string WATCHED_HEAVYHANDED_VESSEL_REMAINING = "sitHeavyhandedVesselRemaining";
        public const string WATCHED_HEAVYHANDED_FORAGING_REMAINING = "sitHeavyhandedForagingRemaining";
        public const string WATCHED_HEAVYHANDED_WILD_CROP_REMAINING = "sitHeavyhandedWildCropRemaining";

        // Claustrophobic (Hunter): -15% ore drop, -10% mining speed - already defined above
        public const int VANILLA_CLAUSTROPHOBIC_ORE_PENALTY = 15;
        public const int VANILLA_CLAUSTROPHOBIC_MINING_PENALTY = 10;
        public const string WATCHED_CLAUSTROPHOBIC_MINING_REMAINING = "sitClaustrophobicMiningRemaining";

        private const string CONFIG_SAVE_KEY = "sitConfig";
        private const string CONFIG_FILE_NAME = "SeraphLeveling.json";

        /// <summary>Version stamped into the config file. 1 means the world-save blob has been folded in.</summary>
        private const int CURRENT_CONFIG_VERSION = 2;

        /// <summary>ConfigVersion read from the file this run. Zero for files written before 1.19.0.</summary>
        private static int LoadedConfigVersion = 0;

        // Vanilla Hardy trait mining speed bonus (used for cap calculations)
        public const int VANILLA_HARDY_MINING_BONUS = 10;

        // Lock object for persistence operations
        public static readonly object persistLock = new object();

        // Flag to indicate pending config save
        public static volatile bool pendingConfigSave = false;

        // Auto-save configuration
        public static int AutoSaveIntervalSeconds = 300;  // Default 5 minutes
        private static long autoSaveTimerId = 0;

        // Disabled skills set for quick lookup (lowercase)
        public static HashSet<string> DisabledSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // =========================================================================
        // MULTI-MOD COMPATIBILITY
        // =========================================================================

        public static ImmutableDictionary<string, ImmutableList<(TraitDefinition Trait, int Value)>> TraitsForAttributes { get; private set; } = ImmutableDictionary<string, ImmutableList<(TraitDefinition, int)>>.Empty;
        public static List<TraitDefinition> LoadedTraits { get; internal set; } = [];   // Preserve ordering for consistent trait text formatting
        public static HashSet<ISaveableAttribute> LoadedAttributes { get; internal set; } = [];

        public static HashSet<ModDefinition> LoadedMods { get; internal set; } = [ModDefinitions.Vanilla];
        public static void DetectLoadedMods(IModLoader modLoader)
        {
            HashSet<ModDefinition> activeMods = [ModDefinitions.Vanilla];
            Instance.DetectCombatOverhaul(modLoader);
            Instance.DetectSacredLib(modLoader);
            if (IsSacredLibLoaded)
            {
                // Sacred Classes replaces the vanilla set of classes
                activeMods.Remove(ModDefinitions.Vanilla);
                activeMods.Add(ModDefinitions.SacredClasses);
            }
            LoadedMods = activeMods;
            var traits = activeMods
                    .SelectMany(mod => mod.CharacterClasses)
                    .SelectMany(charClass => charClass.Traits)
                    .DistinctBy(trait => trait.Id);
            LoadedTraits = traits.ToList();

            var flatAttributeMappings = traits
                    .SelectMany(trait => trait.Attributes, (trait, attrKvp) => new
                    {
                        attrKvp.Attribute,
                        TraitTuple = (Trait: trait, Value: attrKvp.ModifierValue)
                    });

            // 3. Extract the unique attributes
            LoadedAttributes = flatAttributeMappings
                .Select(x => x.Attribute)
                .ToHashSet();

            // 4. Group by Attribute ID and build an immutable dictionary atomicaly
            TraitsForAttributes = flatAttributeMappings
                .GroupBy(x => x.Attribute.Id)
                .ToImmutableDictionary(
                    group => group.Key,
                    group => group.Select(x => x.TraitTuple).ToImmutableList()
                );
            ServerApi.Logger.Notification("[SeraphLeveling] loaded attributes, verifying list...");
            foreach (var attribute in LoadedAttributes)
            {
                ServerApi.Logger.Notification($"[SeraphLeveling] attribute {attribute.Id} loaded.");
            }
            foreach (var (attrKey, traitList) in TraitsForAttributes)
            {
                ServerApi.Logger.Notification($"[SeraphLeveling] attribute {attrKey} linked to {traitList.Count} traits.");
            }
            foreach (var definition in LoadedAttributes)
            {
                if (AttributeConfiguration.TryGetValue(definition.Id, out var dataDict))
                {
                    definition.ReadConfigData(dataDict);
                }
            }
        }

        // =========================================================================
        // SACRED CLASSES COMPATIBILITY
        // =========================================================================

        /// <summary>Whether Sacred Classes mod is loaded.</summary>
        public static bool IsSacredLibLoaded { get; internal set; } = false;

        public static bool IsSacredLibCompatEnabled => IsSacredLibLoaded && SacredLibEnableCompat;

        public static bool SacredLibEnableCompat = true;

        public static bool DetectAnySacredLib(IModLoader modLoader)
        {
            if (modLoader == null) return false;
            return modLoader.IsModEnabled("sacredlib");
        }

        /// <summary>
        /// Detect if Sacred Classes mod is loaded and log the result.
        /// </summary>
        private void DetectSacredLib(IModLoader modLoader)
        {
            IsSacredLibLoaded = DetectAnySacredLib(modLoader);
            if (IsSacredLibLoaded)
            {
                if (SacredLibEnableCompat)
                {
                    ServerApi.Logger.Notification($"[SeraphLeveling] Sacred Classes mod detected. Compatibility enabled.");
                }
                else
                {
                    ServerApi.Logger.Notification($"[SeraphLeveling] Sacred Classes mod detected, but compatibility is disabled in config.");
                }
            }
            else
            {
                ServerApi.Logger.Notification($"[SeraphLeveling]Sacred Classes mod not detected. Compatibility disabled.");
            }
        }

        // =========================================================================
        // COMBAT OVERHAUL COMPATIBILITY
        // =========================================================================

        /// <summary>Whether Combat Overhaul mod is loaded.</summary>
        public static bool IsCombatOverhaulLoaded { get; internal set; } = false;

        /// <summary>Whether specifically the 1.22 Combat Overhaul FORK is loaded.
        /// The fork adds a separate poleaxeProficiency stat the original mod lacks,
        /// so poleaxe weapons route to that stat only when this is true (original
        /// CO keeps lumping them with halberds).</summary>
        public static bool IsCombatOverhaulForkLoaded { get; internal set; } = false;

        /// <summary>Whether CO compatibility is enabled (mod loaded AND config enabled).</summary>
        public static bool IsCOCompatEnabled => IsCombatOverhaulLoaded && COEnableCompat;

        /// <summary>
        /// Mod IDs recognized as "Combat Overhaul". The original mod is
        /// "combatoverhaul"; the 1.22 community continuation is the fork
        /// "combatoverhaulfork". The fork keeps CO's stat names and trait
        /// codes (bowsProficiency, playerHeadDamageFactor, meleeDamageTierBonus*,
        /// etc.) and ships its weapons in the game domain with the same code
        /// suffixes (blade-, bow-, spear-, ...), and our weapon matching strips
        /// the domain prefix, so all proficiency tracking and stat application
        /// works for either one once it is detected here.
        /// </summary>
        public static readonly string[] CombatOverhaulModIds = { "combatoverhaul", "combatoverhaulfork" };

        /// <summary>Returns true if Combat Overhaul or its 1.22 fork is enabled.</summary>
        public static bool DetectAnyCombatOverhaul(IModLoader modLoader)
        {
            if (modLoader == null) return false;
            foreach (string id in CombatOverhaulModIds)
            {
                if (modLoader.IsModEnabled(id)) return true;
            }
            return false;
        }
        // CO configuration values (loaded from config)
        public static bool COEnableCompat = true;
        public static int COBaseDamagePerIncrement = 100;
        public static int COIncrementStep = 100;
        public static float COBowsProficiencyMax = 0.5f;
        public static float COCrossbowsProficiencyMax = 0.5f;
        public static float COFirearmsProficiencyMax = 0.5f;
        public static float COSlingsProficiencyMax = 0.3f;
        public static float COOneHandedSwordsProficiencyMax = 0.3f;
        public static float COTwoHandedSwordsProficiencyMax = 0.3f;
        public static float COSpearsProficiencyMax = 0.3f;
        public static float COJavelinsProficiencyMax = 0.3f;
        public static float COMacesProficiencyMax = 0.3f;
        public static float COClubsProficiencyMax = 0.3f;
        public static float COHalberdsProficiencyMax = 0.3f;
        public static float COPoleaxeProficiencyMax = 0.3f;
        public static float COAxesProficiencyMax = 0.3f;
        public static float COQuarterstaffProficiencyMax = 0.3f;
        public static float COSteadyAimMax = 0.5f;

        // Debug logging (disabled by default to avoid log spam)
        public static bool DebugLoggingEnabled = false;

        // Notification settings
        public static bool EnableLevelUpMessages = true;
        public static bool EnableLevelUpSound = true;
        public static string LevelUpSoundName = "game:sounds/effect/receptionbell";
        public static float LevelUpSoundVolume = 0.25f;

        // Dispose guard to prevent OnGameWorldSave from persisting empty dictionaries after Dispose()
        private static volatile bool isDisposed = false;

        // Network channel for sending level-up sounds to clients
        private static IServerNetworkChannel serverSoundChannel;

        // Skill decay settings
        public static bool EnableSkillDecay = false;
        public static double DecayGracePeriodDays = 1.0;
        public static int DecayBasePointsPerDay = 10;
        public static int DecayMaxPointsPerDay = 100;
        public static HashSet<string> DecayExemptSkills = new HashSet<string>();
        public static Dictionary<string, double> DecayGracePeriodOverrides = new Dictionary<string, double>();
        public static Dictionary<string, int> DecayBasePointsOverrides = new Dictionary<string, int>();
        public static Dictionary<string, int> DecayMaxPointsOverrides = new Dictionary<string, int>();
        public static bool VerboseDecayLogging = false;

        // Per-player tracking for daily decay tick (maps player UID to last checked in-game day)
        public static ConcurrentDictionary<string, double> LastDecayCheckDay = new ConcurrentDictionary<string, double>();

        // Sleep buff settings
        public static bool EnableSleepBuff = false;
        public static float SleepBuffLinenBedMultiplier = 2.0f;
        public static float SleepBuffHayBedMultiplier = 1.5f;
        public static double SleepBuffDurationDays = 1.0;

        // Sleep buff tracking - maps player UID to their buff expiration time (in game days)
        public static ConcurrentDictionary<string, double> SleepBuffExpiration = new ConcurrentDictionary<string, double>();
        public static ConcurrentDictionary<string, float> SleepBuffMultiplier = new ConcurrentDictionary<string, float>();
        private const string SLEEP_BUFF_SAVE_KEY = "sitSleepBuff";
        internal static volatile bool pendingSleepBuffSave = false;

        // Dedup tracking for sleep buff messages (prevents double message from head+foot bed parts)
        internal static ConcurrentDictionary<string, long> LastSleepBuffApplyTick = new ConcurrentDictionary<string, long>();

        // Death penalty settings
        public static bool EnableDeathPenalty = false;
        public static double DeathPenaltyFraction = 0.5;
        public static HashSet<string> DeathPenaltyExemptSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Per-proficiency base and increment overrides (optional, falls back to global values)
        public static Dictionary<string, int> COProficiencyBaseOverrides = new Dictionary<string, int>();
        public static Dictionary<string, int> COProficiencyIncrementOverrides = new Dictionary<string, int>();

        // CO proficiency stat names (must match Combat Overhaul's stat names)
        public const string CO_BOWS_PROFICIENCY = "bowsProficiency";
        public const string CO_CROSSBOWS_PROFICIENCY = "crossbowsProficiency";
        public const string CO_FIREARMS_PROFICIENCY = "firearmsProficiency";
        public const string CO_SLINGS_PROFICIENCY = "slingsProficiency";
        public const string CO_ONE_HANDED_SWORDS_PROFICIENCY = "oneHandedSwordsProficiency";
        public const string CO_TWO_HANDED_SWORDS_PROFICIENCY = "twoHandedSwordsProficiency";
        public const string CO_SPEARS_PROFICIENCY = "spearsProficiency";
        public const string CO_JAVELINS_PROFICIENCY = "javelinsProficiency";
        public const string CO_MACES_PROFICIENCY = "macesProficiency";
        public const string CO_CLUBS_PROFICIENCY = "clubsProficiency";
        public const string CO_HALBERDS_PROFICIENCY = "halberdsProficiency";
        public const string CO_POLEAXE_PROFICIENCY = "poleaxeProficiency";
        public const string CO_AXES_PROFICIENCY = "axesProficiency";
        public const string CO_QUARTERSTAFF_PROFICIENCY = "quarterstaffProficiency";
        public const string CO_STEADY_AIM = "steadyAim";

        // CO negative trait penalty values
        public const float CO_TREMBLING_AIM_PENALTY = 0.3f;
        public const float CO_CLUMSY_HANDS_PENALTY = 0.3f;
        public const int CO_FEAR_OF_MELEE_TIER_PENALTY = 1;
        public const float CO_WEAK_HAND_PENALTY = 0.3f;  // -0.3 to ranged proficiencies (similar to Clumsy Hands)
        public const int CO_NERVOUS_TIER_PENALTY = 1;    // -1 damage tier for piercing melee

        // CO Damage Tier Stats (armor penetration)
        public const string CO_RANGED_TIER_SLASHING = "rangedDamageTierBonusSlashingAttack";
        public const string CO_RANGED_TIER_PIERCING = "rangedDamageTierBonusPiercingAttack";
        public const string CO_RANGED_TIER_BLUNT = "rangedDamageTierBonusBluntAttack";
        public const string CO_MELEE_TIER_SLASHING = "meleeDamageTierBonusSlashingAttack";
        public const string CO_MELEE_TIER_PIERCING = "meleeDamageTierBonusPiercingAttack";
        public const string CO_MELEE_TIER_BLUNT = "meleeDamageTierBonusBluntAttack";

        // CO Body Damage Stats
        public const string CO_HEAD_DAMAGE_FACTOR = "playerHeadDamageFactor";
        public const string CO_FACE_DAMAGE_FACTOR = "playerFaceDamageFactor";
        public const string CO_LEGS_DAMAGE_FACTOR = "playerLegsDamageFactor";
        public const string CO_FEET_DAMAGE_FACTOR = "playerFeetDamageFactor";
        public const string CO_JUMP_HEIGHT = "jumpHeightMul";

        // CO Big Head / Thick Skull / Leg Day penalties and bonuses
        public const float CO_BIG_HEAD_PENALTY = 0.5f;      // +50% head/face damage (Clockmaker)
        public const float CO_THICK_SKULL_BONUS = 0.5f;     // -50% head/face damage (earnable)
        public const float CO_LEG_DAY_PENALTY = 1.0f;       // +100% leg/feet damage (Blackguard)
        public const float CO_LEG_DAY_JUMP_BONUS = 0.25f;   // +25% jump height (Blackguard)
        public const int CO_MELEE_EXPERT_TIER_BONUS = 1;    // +1 melee slashing tier (Blackguard)
        public const int CO_FRIGHTENED_TIER_PENALTY = 1;    // -1 melee slashing tier (Clockmaker)

        // WatchedAttributes keys for CO (client sync)
        public const string WATCHED_CO_STEADY_AIM_CREDITS = "sitCOSteadyAimCredits";
        public const string WATCHED_CO_TREMBLING_AIM_REMAINING = "sitCOTremblingAimRemaining";
        public const string WATCHED_CO_HAS_TREMBLING_AIM = "sitCOHasTremblingAim";
        public const string WATCHED_CO_CLUMSY_HANDS_REMAINING = "sitCOClumsyHandsRemaining";
        public const string WATCHED_CO_FEAR_OF_MELEE_REMAINING = "sitCOFearOfMeleeRemaining";
        public const string WATCHED_CO_WEAK_HAND_REMAINING = "sitCOWeakHandRemaining";
        public const string WATCHED_CO_NERVOUS_REMAINING = "sitCONervousRemaining";

        // WatchedAttributes for Big Head / Thick Skull / Leg Day / Melee Expert
        public const string WATCHED_CO_BIG_HEAD_REMAINING = "sitCOBigHeadRemaining";
        public const string WATCHED_CO_LEG_DAY_REMAINING = "sitCOLegDayRemaining";
        public const string WATCHED_CO_FRIGHTENED_REMAINING = "sitCOFrightenedRemaining";
        public const string WATCHED_CO_MELEE_TIER_BONUS = "sitCOMeleeTierBonus";
        public const string WATCHED_CO_RANGED_TIER_BONUS = "sitCORangedTierBonus";

        // CO stat codes (prefixed to avoid collisions)
        public const string CO_STAT_PREFIX = "sitCO";

        /// <summary>
        /// How much the player's traits have already contributed to one stat.
        ///
        /// The game's CharacterSystem.applyTraitAttributes writes every trait attribute
        /// into the player's stats under the single stat code "trait", and EntityStats
        /// blends a category by summing all of its codes. Anything we write under our own
        /// stat code therefore lands on top of the trait's value rather than replacing it.
        /// Subtract this from the total we want the player to end up with, or the trait
        /// gets counted twice.
        ///
        /// This reads the value the game actually applied instead of assuming one from
        /// the character class, so it stays right whichever mod granted the trait, and it
        /// returns zero when no trait touches the stat. CharacterSystem is part of the
        /// survival mod, which loads before us, so its PlayerJoin handler has already run
        /// by the time ours does and the value is there to read.
        /// </summary>
        private static float TraitStatValue(EntityPlayer entity, string statCategory)
        {
            if (entity?.Stats == null || string.IsNullOrEmpty(statCategory)) return 0f;

            foreach (KeyValuePair<string, EntityFloatStats> stat in entity.Stats)
            {
                if (stat.Key != statCategory) continue;
                if (stat.Value?.ValuesByKey == null) return 0f;
                return stat.Value.ValuesByKey.TryGetValue("trait", out EntityStat<float> applied) ? applied.Value : 0f;
            }

            return 0f;
        }

        /// <summary>
        /// The penalty part of a trait's contribution to a stat, as a negative number, or
        /// zero if the trait helps rather than hurts.
        ///
        /// Used for the stats where our own value is an additive bonus rather than a total:
        /// steadyAim and the weapon proficiencies. A class that grants one of those as a
        /// perk should keep it and have our earned bonus stack on top, but a class that is
        /// penalised there, by Trembling Aim or Clumsy Hands, should be able to buy the
        /// penalty back off.
        /// </summary>
        private static float TraitStatPenalty(EntityPlayer entity, string statCategory)
        {
            return Math.Min(0f, TraitStatValue(entity, statCategory));
        }

        // CO persistence
        // Storage for CO progress - keyed by player UID
        public static ConcurrentDictionary<string, COPlayerProgressData> COProgress = new ConcurrentDictionary<string, COPlayerProgressData>();

        // Flag to indicate pending CO progress save
        public static volatile bool pendingCOProgressSave = false;

        /// <summary>
        /// All CO proficiency stat names for iteration.
        /// </summary>
        public static readonly string[] AllCOProficiencies = new[]
        {
            CO_BOWS_PROFICIENCY, CO_CROSSBOWS_PROFICIENCY, CO_FIREARMS_PROFICIENCY, CO_SLINGS_PROFICIENCY,
            CO_ONE_HANDED_SWORDS_PROFICIENCY, CO_TWO_HANDED_SWORDS_PROFICIENCY, CO_SPEARS_PROFICIENCY,
            CO_JAVELINS_PROFICIENCY, CO_MACES_PROFICIENCY, CO_CLUBS_PROFICIENCY, CO_HALBERDS_PROFICIENCY,
            CO_POLEAXE_PROFICIENCY, CO_AXES_PROFICIENCY, CO_QUARTERSTAFF_PROFICIENCY
        };

        /// <summary>
        /// Ranged proficiencies that also contribute to Steady Aim.
        /// </summary>
        public static readonly string[] CORangedProficiencies = new[]
        {
            CO_BOWS_PROFICIENCY, CO_CROSSBOWS_PROFICIENCY, CO_FIREARMS_PROFICIENCY, CO_SLINGS_PROFICIENCY
        };

        /// <summary>
        /// Check if a skill is disabled in the config.
        /// </summary>
        public static bool IsSkillDisabled(string skillName)
        {
            return DisabledSkills.Contains(skillName);
        }

        public static bool IsAttributeModifierDisabled(ISaveableAttribute attribute)
        {
            return DisabledSkills.Contains(attribute.Id);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            ServerApi = api;
            Instance = this;
            isDisposed = false;

            // Register network channel for level-up sound
            serverSoundChannel = api.Network.RegisterChannel("seraphleveling")
                .RegisterMessageType<LevelUpSoundMessage>();

            // Load config file (sets defaults for new worlds)
            LoadConfigFile(api);

            // Detect loaded mods.
            DetectLoadedMods(api.ModLoader);

            // Register /trait command with subcommands
            IChatCommand command = null;
            command = api.ChatCommands.Create("trait")
                .WithDescription("Manage and view trait progression")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith((args) =>
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("**Available subcommands for /trait:**");

                    // Loop over the command's own registered subcommands automatically
                    foreach (IChatCommand sub in args.Command.Subcommands)
                    {
                        // Format example:  • /trait add [name] - Gives a trait to the player
                        sb.AppendLine($"• **/trait {sub.Name}** - {sub.Description}");
                    }

                    return TextCommandResult.Success(sb.ToString());
                });
            foreach (var definition in LoadedAttributes)
            {
                definition.RegisterCommands(api, command);
            }
            command
                .BeginSubCommand("testwalkspeed")
                    .WithDescription("Apply a test walk speed modifier (admin only, use 0 to clear)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTestWalkSpeedCommand)
                .EndSubCommand()
                // Reset all traits
                .BeginSubCommand("reset")
                    .WithDescription("Reset all trait progression to 0 (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitResetCommand)
                .EndSubCommand()
                // Export full progression to a JSON file for cross-world transfer (admin only)
                .BeginSubCommand("export")
                    .WithDescription("Export your (or a named player's) full progression to a JSON file you can carry to another world. Admin only.")
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("playername"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitExportCommand)
                .EndSubCommand()
                // Import a progression JSON file onto a player, replacing current progress (admin only)
                .BeginSubCommand("import")
                    .WithDescription("Import a progression JSON file onto yourself (or a named player), replacing current progress. Admin only.")
                    .WithArgs(api.ChatCommands.Parsers.Word("filename"), api.ChatCommands.Parsers.OptionalWord("playername"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitImportCommand)
                .EndSubCommand()
                // Re-read ModConfig/SeraphLeveling.json without a restart
                .BeginSubCommand("reloadconfig")
                    .WithDescription("Re-read ModConfig/SeraphLeveling.json and apply it immediately (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitReloadConfigCommand)
                .EndSubCommand()
                // Diagnostic: show what every stat this mod touches is actually made of
                .BeginSubCommand("verify")
                    .WithDescription("Show the blended value and each contributing stat code for the stats this mod writes (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitVerifyCommand)
                .EndSubCommand()
                // Max all traits for testing
                .BeginSubCommand("maxall")
                    .WithDescription("Set all trait progression to maximum for testing (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMaxAllCommand)
                .EndSubCommand()
                // Test suite command
                .BeginSubCommand("testsuite")
                    .WithDescription("Run automated tests for trait calculations")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("category"))
                    .HandleWith(OnTraitTestSuiteCommand)
                .EndSubCommand()
                // Visual format probe: every progression at exactly 1 credit so every dynamic
                // trait line renders, but with values low enough that negative traits aren't
                // fully cancelled (so reduction-style displays also show).
                .BeginSubCommand("testsuite1")
                    .WithDescription("Set every progression skill to 1 credit to visually inspect dynamic trait formatting (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTestSuite1Command)
                .EndSubCommand();
            if (IsCombatOverhaulLoaded)
            {
                // Combat Overhaul proficiency commands
                command.BeginSubCommand("coproficiency")
                    .WithDescription("View all Combat Overhaul proficiency progression (requires CO mod)")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitCOProficiencyCommand)
                .EndSubCommand()
                .BeginSubCommand("colevel")
                    .WithDescription("Set Combat Overhaul proficiency credits (admin only). Usage: /trait colevel &lt;proficiency&gt; &lt;credits&gt; [toolname]")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.Word("proficiency"), api.ChatCommands.Parsers.Int("credits"), api.ChatCommands.Parsers.OptionalWord("toolname"))
                    .HandleWith(OnTraitCOLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("coreset")
                    .WithDescription("Reset all Combat Overhaul progression to 0 (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitCOResetCommand)
                .EndSubCommand()
                .BeginSubCommand("comaxall")
                    .WithDescription("Set all Combat Overhaul proficiencies to max for testing (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitCOMaxAllCommand)
                .EndSubCommand()
                // Per-proficiency CO commands
                .BeginSubCommand("bows")
                    .WithDescription("View or configure Bows proficiency. Usage: /trait bows [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_BOWS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("crossbows")
                    .WithDescription("View or configure Crossbows proficiency. Usage: /trait crossbows [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_CROSSBOWS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("firearms")
                    .WithDescription("View or configure Firearms proficiency. Usage: /trait firearms [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_FIREARMS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("slings")
                    .WithDescription("View or configure Slings proficiency. Usage: /trait slings [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_SLINGS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("onehandedswords")
                    .WithDescription("View or configure One-Handed Swords proficiency. Usage: /trait onehandedswords [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_ONE_HANDED_SWORDS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("twohandedswords")
                    .WithDescription("View or configure Two-Handed Swords proficiency. Usage: /trait twohandedswords [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_TWO_HANDED_SWORDS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("cospears")
                    .WithDescription("View or configure Spears proficiency (CO). Usage: /trait cospears [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_SPEARS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("javelins")
                    .WithDescription("View or configure Javelins proficiency. Usage: /trait javelins [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_JAVELINS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("maces")
                    .WithDescription("View or configure Maces proficiency. Usage: /trait maces [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_MACES_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("clubs")
                    .WithDescription("View or configure Clubs proficiency. Usage: /trait clubs [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_CLUBS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("halberds")
                    .WithDescription("View or configure Halberds proficiency. Usage: /trait halberds [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_HALBERDS_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("poleaxe")
                    .WithDescription("View or configure Poleaxe proficiency (Combat Overhaul fork). Usage: /trait poleaxe [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_POLEAXE_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("coaxes")
                    .WithDescription("View or configure Axes proficiency (CO). Usage: /trait coaxes [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_AXES_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("quarterstaff")
                    .WithDescription("View or configure Quarterstaff proficiency. Usage: /trait quarterstaff [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_QUARTERSTAFF_PROFICIENCY))
                .EndSubCommand()
                .BeginSubCommand("steadyaim")
                    .WithDescription("View or configure Steady Aim. Usage: /trait steadyaim [base|increment|level|max] [value]")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.OptionalWord("action"), api.ChatCommands.Parsers.OptionalInt("value"))
                    .HandleWith(args => OnTraitCOProficiencyConfigCommand(args, CO_STEADY_AIM))
                .EndSubCommand();
            }
            command
            // Sleep buff and decay status commands
            .BeginSubCommand("sleepbuff")
                .WithDescription("View your current sleep buff status")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(OnTraitSleepBuffCommand)
            .EndSubCommand()
            .BeginSubCommand("decay")
                .WithDescription("View your current skill decay status")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(OnTraitDecayCommand)
            .EndSubCommand()
            .BeginSubCommand("all")
                .WithDescription("View all trait progression at once")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(OnTraitAllCommand)
            .EndSubCommand()
            .BeginSubCommand("list")
                .WithDescription("View all supported traits for the currently loaded mod set")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(OnTraitListCommand)
            .EndSubCommand()
            .BeginSubCommand("soundvolume")
                .WithDescription("Get or set the level-up ding volume, from 0.0 (silent) to 1.0 (full). Default 0.25. Scale is exponential, so 0.5 is close to 1.0 (admin)")
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("volume"))
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnTraitSoundVolumeCommand)
            .EndSubCommand()
            .BeginSubCommand("testsound")
                .WithDescription("Play the level-up ding once at a specified volume (0.0-1.0) for testing. Defaults to the current config volume (admin)")
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("volume"))
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .HandleWith(OnTraitTestSoundCommand)
            .EndSubCommand()
            .BeginSubCommand("setplayer")
                .WithDescription("Set a trait level for another player. Usage: /trait setplayer &lt;playername&gt; &lt;trait&gt; &lt;level&gt; [toolname]")
                .WithArgs(api.ChatCommands.Parsers.Word("playername"), api.ChatCommands.Parsers.Word("trait"), api.ChatCommands.Parsers.Int("level"), api.ChatCommands.Parsers.OptionalWord("toolname"))
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnTraitSetPlayerCommand)
            .EndSubCommand()
            .BeginSubCommand("max")
                .WithDescription("View or set the max bonus percent for a trait. (admin only) Usage: /trait max &lt;trait&gt; [level]")
                .WithArgs(api.ChatCommands.Parsers.Word("trait"), api.ChatCommands.Parsers.OptionalInt("level"))
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnTraitSetMaxCommand)
            .EndSubCommand()
            .BeginSubCommand("increment")
                .WithDescription("View or set the increment step per credit for a trait. (admin only) Usage: /trait increment &lt;trait&gt; [step]")
                .WithArgs(api.ChatCommands.Parsers.Word("trait"), api.ChatCommands.Parsers.OptionalInt("step"))
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnTraitSetIncrementCommand)
            .EndSubCommand()
            .BeginSubCommand("base")
                .WithDescription("View or set the base step per credit for a trait. (admin only) Usage: /trait base &lt;trait&gt; [step]")
                .WithArgs(api.ChatCommands.Parsers.Word("trait"), api.ChatCommands.Parsers.OptionalInt("step"))
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnTraitSetBaseCommand)
            .EndSubCommand()
            .BeginSubCommand("level")
                .WithDescription("View or set your level for a trait. (admin only) Usage: /trait level &lt;trait&gt; [level]")
                .WithArgs(api.ChatCommands.Parsers.Word("trait"), api.ChatCommands.Parsers.OptionalInt("step"), api.ChatCommands.Parsers.OptionalWord("tool"))
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnTraitLevelCommand)
            .EndSubCommand()
            .BeginSubCommand("unlock")
                .WithDescription("Manually lock or unlock an unlockable trait. (admin only) Usage: /trait unlock &lt;trait&gt; &lt;unlock&gt;")
                .WithArgs(api.ChatCommands.Parsers.Word("trait"), api.ChatCommands.Parsers.Bool("unlock", "unlocked"))
                .RequiresPrivilege(Privilege.controlserver)
                .HandleWith(OnTraitUnlockCommand)
            .EndSubCommand()

                ;

            // Hook into block breaking for mining progression
            api.Event.DidBreakBlock += OnBlockBroken;

            // Apply Harmony patches for melee damage tracking
            ApplyServerHarmonyPatches(api);

            // Hook into player join to apply saved bonuses
            api.Event.PlayerJoin += OnPlayerJoin;

            // Hook into world save event to persist progress
            api.Event.GameWorldSave += OnGameWorldSave;

            // Load config and progress data after save game is loaded
            api.Event.SaveGameLoaded += LoadConfig;
            api.Event.SaveGameLoaded += LoadAllProgress;
            api.Event.SaveGameLoaded += LoadCOProgress;
            api.Event.SaveGameLoaded += LoadSleepBuffData;

            // Register game tick listener for walking distance tracking (every 500ms)
            api.Event.RegisterGameTickListener(OnWalkingTick, 500);

            // Register game tick listener for hunger tracking (every 1000ms / 1 second)
            api.Event.RegisterGameTickListener(OnHungerTick, 1000);

            // Register game tick listener for armor time tracking (every 1000ms / 1 second)
            api.Event.RegisterGameTickListener(OnArmorTick, 1000);

            // Register game tick listener for clothing tracking (every 1000ms / 1 second)
            api.Event.RegisterGameTickListener(OnClothingTick, 1000);

            // Register game tick listener for Mender repair tracking (every 500ms for responsive detection)
            api.Event.RegisterGameTickListener(OnMenderRepairTick, 500);

            // Register game tick listener for sneaking distance tracking (every 500ms for Furtive)
            api.Event.RegisterGameTickListener(OnSneakingTick, 500);

            // Register decay tick (every 10 seconds, checks for daily decay while online)
            api.Event.RegisterGameTickListener(OnDecayTick, 10000);

            // Register auto-save timer if enabled
            if (AutoSaveIntervalSeconds > 0)
            {
                autoSaveTimerId = api.Event.RegisterGameTickListener(OnAutoSaveTick, AutoSaveIntervalSeconds * 1000);
                api.Logger.Notification($"[SeraphLeveling] Auto-save enabled every {AutoSaveIntervalSeconds} seconds");
            }

            // Hook into player disconnect to clean up position tracking and save data
            api.Event.PlayerDisconnect += OnPlayerDisconnect;

            api.Logger.Notification("[SeraphLeveling] Mod loaded");
        }


        /// <summary>
        /// Handler for /trait soundvolume command.
        /// Gets or sets the level-up ding volume (0.0 to 1.0) and persists it to the config file.
        /// </summary>
        private TextCommandResult OnTraitSoundVolumeCommand(TextCommandCallingArgs args)
        {
            string raw = args[0] as string;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return TextCommandResult.Success(
                    $"Current level-up sound volume: {LevelUpSoundVolume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} (0.0 silent, 1.0 full, default 0.25). The scale is exponential, so 0.5 is close to 1.0 and 0.05 is about half as loud as 0.25. Set it with /trait soundvolume 0.25");
            }

            if (!float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float newVolume))
            {
                return TextCommandResult.Error("Volume must be a number between 0.0 and 1.0. For example: /trait soundvolume 0.25");
            }

            if (newVolume < 0f || newVolume > 1f)
            {
                return TextCommandResult.Error("Volume must be between 0.0 and 1.0. The scale is exponential: 0.25 is a comfortable medium, 0.05 is quiet, 0.5 sounds close to 1.0.");
            }

            LevelUpSoundVolume = newVolume;
            SaveLevelUpSoundVolumeToConfig();

            return TextCommandResult.Success(
                $"Level-up sound volume set to {LevelUpSoundVolume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}. This has been saved to the config file.");
        }

        /// <summary>
        /// Writes the current level-up sound volume back to the JSON config file so it survives a restart.
        /// </summary>
        private void SaveLevelUpSoundVolumeToConfig()
        {
            SaveConfigFile();
        }

        /// <summary>
        /// Handler for /trait testsound command.
        /// Sends a single level-up sound packet to the calling player at the requested volume
        /// (or the currently configured volume if no value is given). Marks the packet as a test
        /// so the client logs and prints what it actually received, isolating whether the volume
        /// reached the audio engine intact.
        /// </summary>
        private TextCommandResult OnTraitTestSoundCommand(TextCommandCallingArgs args)
        {
            string raw = args[0] as string;
            float volume = LevelUpSoundVolume;
            bool overridden = false;

            if (!string.IsNullOrWhiteSpace(raw))
            {
                if (!float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out volume))
                {
                    return TextCommandResult.Error("Volume must be a number between 0.0 and 1.0. For example: /trait testsound 0.1");
                }
                if (volume < 0f || volume > 1f)
                {
                    return TextCommandResult.Error("Volume must be between 0.0 and 1.0. Use 0.0 for silent, 0.5 for half, 1.0 for full.");
                }
                overridden = true;
            }

            var caller = args.Caller.Player as IServerPlayer;
            if (caller == null)
            {
                return TextCommandResult.Error("This command must be run by a player.");
            }

            if (serverSoundChannel == null)
            {
                return TextCommandResult.Error("Sound network channel not initialized. Cannot send test sound.");
            }

            string soundName = LevelUpSoundName;
            try
            {
                serverSoundChannel.SendPacket(new LevelUpSoundMessage
                {
                    SoundName = soundName,
                    Volume = volume,
                    IsTest = true
                }, caller);
            }
            catch (Exception ex)
            {
                return TextCommandResult.Error($"Failed to send test sound: {ex.Message}");
            }

            string volStr = volume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            string source = overridden ? "supplied" : "current config";
            ServerApi?.Logger.Notification($"[SeraphLeveling] /trait testsound by {caller.PlayerName}: sound={soundName}, volume={volStr} ({source})");
            return TextCommandResult.Success(
                $"Sent test sound '{soundName}' at volume {volStr} ({source}). Run /trait testsound 0.05 then /trait testsound 1.0 back to back to A/B test, and watch your chat for the volume the client reports receiving.");
        }

        /// <summary>
        /// Handler for /trait all command. Shows all trait progression in a single message.
        /// </summary>
        private TextCommandResult OnTraitAllCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var sb = new StringBuilder();
            sb.AppendLine("=== All Trait Progression ===");
            foreach (var definition in LoadedAttributes)
            {
                definition.GetTraitAllCommandLine(player, sb);
            }

            // Unlock traits
            sb.AppendLine("\n--- Unlock Traits ---");
            foreach (var definition in LoadedAttributes)
            {
                definition.GetTraitUnlockableCommandLine(player, sb);
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        private TextCommandResult OnTraitListCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            var sb = new StringBuilder();
            sb.AppendLine("--- Leveled Traits ---");
            LoadedAttributes.Where(attr => attr is ILeveledAttributeModifierDefinition).OrderBy(attr => attr.SkillKey).Foreach(attr => sb.AppendLine(attr.SkillKey + ": " + attr.LongDescription));
            sb.AppendLine("\n--- Unlocked Traits ---");
            LoadedAttributes.Where(attr => attr is IUnlockedAttributeModifierDefinition).OrderBy(attr => attr.SkillKey).Foreach(attr => sb.AppendLine(attr.SkillKey + ": " + attr.LongDescription));
            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Resolves a player by name (case-insensitive partial match).
        /// Returns null if not found online.
        /// </summary>
        private IServerPlayer ResolvePlayerByName(string playerName)
        {
            if (ServerApi?.World?.AllOnlinePlayers == null) return null;

            // Try exact match first (case-insensitive)
            foreach (var onlinePlayer in ServerApi.World.AllOnlinePlayers)
            {
                var sp = onlinePlayer as IServerPlayer;
                if (sp != null && string.Equals(sp.PlayerName, playerName, StringComparison.OrdinalIgnoreCase))
                    return sp;
            }

            // Try partial match
            IServerPlayer match = null;
            int matches = 0;
            foreach (var onlinePlayer in ServerApi.World.AllOnlinePlayers)
            {
                var sp = onlinePlayer as IServerPlayer;
                if (sp != null && sp.PlayerName.IndexOf(playerName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    match = sp;
                    matches++;
                }
            }
            return matches == 1 ? match : null;
        }

        /// <summary>
        /// Handler for /trait max command. Sets the max earnable credits for a trait.
        /// Usage: /trait max <trait> [level]
        /// </summary>
        private TextCommandResult OnTraitSetMaxCommand(TextCommandCallingArgs args)
        {
            string traitName = ((string)args[0]).ToLowerInvariant();
            foreach (var definition in LoadedAttributes)
            {
                if (definition.SkillKey == traitName)
                {
                    return definition.HandleMaxCommand(args, 1);
                }
            }
            return TextCommandResult.Error($"No '{traitName}' trait found.");
        }

        /// <summary>
        /// Handler for /trait increment command. Sets the increment units per step for a trait.
        /// Usage: /trait increment <trait> [units]
        /// </summary>
        private TextCommandResult OnTraitSetIncrementCommand(TextCommandCallingArgs args)
        {
            string traitName = ((string)args[0]).ToLowerInvariant();
            foreach (var definition in LoadedAttributes)
            {
                if (definition.SkillKey == traitName)
                {
                    return definition.HandleIncrementCommand(args, 1);
                }
            }
            return TextCommandResult.Error($"No '{traitName}' trait found.");
        }

        /// <summary>
        /// Handler for /trait base command. Sets the base units per step for a trait.
        /// Usage: /trait base <trait> [units]
        /// </summary>
        private TextCommandResult OnTraitSetBaseCommand(TextCommandCallingArgs args)
        {
            string traitName = ((string)args[0]).ToLowerInvariant();
            foreach (var definition in LoadedAttributes)
            {
                if (definition.SkillKey == traitName)
                {
                    return definition.HandleBaseCommand(args, 1);
                }
            }
            return TextCommandResult.Error($"No '{traitName}' trait found.");
        }

        /// <summary>
        /// Handler for /trait level command. Sets the user's level for a trait.
        /// Usage: /trait level <trait> [level] [tool]
        /// </summary>
        private TextCommandResult OnTraitLevelCommand(TextCommandCallingArgs args)
        {
            string traitName = ((string)args[0]).ToLowerInvariant();
            foreach (var definition in LoadedAttributes)
            {
                if (definition.SkillKey == traitName)
                {
                    return definition.HandleLevelCommand(args, 1);
                }
            }
            return TextCommandResult.Error($"No '{traitName}' trait found.");
        }

        /// <summary>
        /// Handler for /trait max command. Sets the max earnable credits for a trait.
        /// Usage: /trait max <trait> [level]
        /// </summary>
        private TextCommandResult OnTraitUnlockCommand(TextCommandCallingArgs args)
        {
            string traitName = ((string)args[0]).ToLowerInvariant();
            foreach (var definition in LoadedAttributes)
            {
                if (definition.SkillKey == traitName)
                {
                    return definition.HandleUnlockCommand(args, 1);
                }
            }
            return TextCommandResult.Error($"No '{traitName}' trait found.");
        }



        /// <summary>
        /// Handler for /trait setplayer command. Sets a trait level for a target player.
        /// Usage: /trait setplayer <PlayerName> <trait> <level>
        /// </summary>
        private TextCommandResult OnTraitSetPlayerCommand(TextCommandCallingArgs args)
        {
            string playerName = (string)args[0];
            string traitName = ((string)args[1]).ToLowerInvariant();
            int level = (int)args[2];
            string toolName = (string)args[3];

            if (level < 0)
                return TextCommandResult.Error("Level cannot be negative.");

            var targetPlayer = ResolvePlayerByName(playerName);
            if (targetPlayer == null)
                return TextCommandResult.Error($"Could not find online player matching '{playerName}'.");

            string targetUid = targetPlayer.PlayerUID;
            foreach (var definition in LoadedAttributes)
            {
                if (definition.SkillKey == traitName)
                {
                    Type[] getParams = [typeof(string)];
                    dynamic progress = definition.GetType().GetMethod("GetForPlayer", BindingFlags.Public | BindingFlags.Instance, null, getParams, null).Invoke(definition, [(object)targetUid]);
                    if (progress != null)
                    {
                        try
                        {
                            return (TextCommandResult)progress.SetLevel(targetPlayer, level, toolName);
                        }
                        catch (RuntimeBinderException)
                        {
                            if (toolName != null)
                            {
                                return TextCommandResult.Error($"The '{traitName}' trait does not support per-tool level setting.");
                            }
                            try
                            {
                                return (TextCommandResult)((dynamic)definition).SetLevel(targetPlayer, level);
                            }
                            catch (RuntimeBinderException)
                            {
                                return TextCommandResult.Error($"The '{traitName}' trait does not support level setting.");
                            }
                        }
                    }
                }
            }

            // Traits without per-tool support — reject toolName if provided
            if (toolName != null)
                return TextCommandResult.Error($"The '{traitName}' trait does not support per-tool level setting.");
            return TextCommandResult.Error($"Unknown trait '{traitName}'.");
        }

        /// <summary>
        /// Calculates credits earned by a tool from its CurrentIncrementSize.
        /// Credits = (currentIncrementSize - baseIncrement) / incrementStep
        /// </summary>
        public static int CalculateToolCredits(int currentIncrementSize, int baseIncrement, int incrementStep)
        {
            if (incrementStep <= 0) return 0;
            int credits = (currentIncrementSize - baseIncrement) / incrementStep;
            return Math.Max(0, credits);
        }

        /// <summary>
        /// Recalculates TotalCredits by summing credits from all per-tool entries.
        /// </summary>
        public static int RecalculateTotalCreditsFromTools<T>(
            Dictionary<string, T> toolDict,
            System.Func<T, int> getIncrementSize,
            int baseIncrement, int incrementStep)
        {
            if (incrementStep <= 0) return 0;
            int total = 0;
            foreach (var kvp in toolDict)
            {
                int toolCredits = (getIncrementSize(kvp.Value) - baseIncrement) / incrementStep;
                if (toolCredits > 0) total += toolCredits;
            }
            return total;
        }

        /// <summary>
        /// Sets per-tool credits for a Combat Overhaul proficiency.
        /// </summary>
        private TextCommandResult SetCOLevelForPlayer(IServerPlayer player, string proficiencyStat, int credits, string toolName)
        {
            string playerUid = player.PlayerUID;
            int maxCredits = proficiencyStat == CO_STEADY_AIM
                ? GetCOSteadyAimMaxCreditsForPlayer(playerUid)
                : GetCOProficiencyMaxCreditsForPlayer(playerUid, proficiencyStat);

            if (credits < 0 || credits > maxCredits)
                return TextCommandResult.Error($"Credits must be between 0 and {maxCredits} for {GetCOProficiencyDisplayName(proficiencyStat)}.");

            var playerProgress = COProgress.GetOrAdd(playerUid, _ => new COPlayerProgressData());

            if (proficiencyStat == CO_STEADY_AIM)
            {
                if (toolName != null)
                    return TextCommandResult.Error("Steady Aim does not support per-tool level setting.");

                playerProgress.SteadyAimCredits = credits;
                ApplyCOSteadyAimBonus(player, credits);
            }
            else
            {
                int profBase = GetCOProficiencyBase(proficiencyStat);
                int profIncrement = GetCOProficiencyIncrement(proficiencyStat);
                var profProgress = playerProgress.GetProficiencyProgress(proficiencyStat);

                if (toolName != null)
                {
                    int oldToolCredits = 0;
                    if (profProgress.WeaponProgress.TryGetValue(toolName, out var existingTool))
                        oldToolCredits = CalculateToolCredits(existingTool.CurrentIncrementSize, profBase, profIncrement);

                    int projectedTotal = profProgress.TotalCredits - oldToolCredits + credits;
                    if (projectedTotal > maxCredits)
                        return TextCommandResult.Error($"Setting {credits} credits on {toolName} would result in {projectedTotal} total credits, exceeding max ({maxCredits}).");

                    if (credits == 0)
                    {
                        profProgress.WeaponProgress.Remove(toolName);
                    }
                    else
                    {
                        var weaponProgress = profProgress.GetWeaponProgress(toolName, profBase);
                        weaponProgress.CurrentIncrementSize = profBase + (credits * profIncrement);
                        weaponProgress.DamageInIncrement = 0;
                    }

                    profProgress.TotalCredits = RecalculateTotalCreditsFromTools(
                        profProgress.WeaponProgress, w => w.CurrentIncrementSize,
                        profBase, profIncrement);

                    ApplyCOProficiencyBonusWithCancellation(player, proficiencyStat, profProgress.TotalCredits);
                }
                else
                {
                    profProgress.TotalCredits = credits;
                    profProgress.WeaponProgress.Clear();
                    ApplyCOProficiencyBonusWithCancellation(player, proficiencyStat, credits);
                }
            }

            pendingCOProgressSave = true;
            UpdateSkillActivityDay(playerUid, "coproficiency");

            int finalCredits = proficiencyStat == CO_STEADY_AIM
                ? playerProgress.SteadyAimCredits
                : playerProgress.GetProficiencyProgress(proficiencyStat).TotalCredits;
            float bonus = CalculateCOProficiencyBonus(finalCredits, GetCOProficiencyMax(proficiencyStat));
            string toolSuffix = toolName != null ? $" on {toolName}. Total: {finalCredits}" : $" to {credits}";
            return TextCommandResult.Success($"Set {GetCOProficiencyDisplayName(proficiencyStat)}{toolSuffix} credits (+{bonus * 100:F0}%).");
        }

        /// <summary>
        /// Gets the pickaxe code from the player's held item, or null if not holding a pickaxe.
        /// </summary>
        private string GetHeldPickaxeCode(IServerPlayer player) => GetHeldToolCodeInner(player, EnumTool.Pickaxe);
        private string GetHeldAxeCode(IServerPlayer player) => GetHeldToolCodeInner(player, EnumTool.Axe);
        private string GetHeldShovelCode(IServerPlayer player) => GetHeldToolCodeInner(player, EnumTool.Shovel);
        private string GetHeldShearsCode(IServerPlayer player) => GetHeldToolCodeInner(player, EnumTool.Shears);

        private string GetHeldToolCodeInner(IServerPlayer player, EnumTool toolType)
        {
            if (player?.Entity == null) return null;

            var heldItem = player.Entity.RightHandItemSlot?.Itemstack?.Collectible;
            if (heldItem == null) return null;

            // Check if it's the right type of tool
            if (heldItem.Tool != toolType) return null;

            // Return the item code as the pickaxe identifier
            return heldItem.Code?.ToString();
        }

        private string GetBlockCode(int blockId)
        {
            if (ServerApi == null) return "";

            var block = ServerApi.World.GetBlock(blockId);
            if (block == null) return "";

            string blockCode = block.Code?.ToString() ?? "";

            // Remove "game:" prefix if present for consistent matching
            return blockCode.StartsWith("game:") ? blockCode.Substring(5) : blockCode;
        }
        private int GetWoodLogPoints(int blockId)
        {
            string codeToCheck = GetBlockCode(blockId);
            ServerApi.Logger.Debug($"[SeraphLeveling] Checking wood log points for block code {codeToCheck}.");
            if (codeToCheck.StartsWith("log-grown-"))
            {
                return 5;
            }
            return 0;
        }

        private int GetDirtPoints(int blockId)
        {
            string codeToCheck = GetBlockCode(blockId);
            ServerApi.Logger.Debug($"[SeraphLeveling] Checking dirt points for block code {codeToCheck}.");
            if (codeToCheck.StartsWith("rawclay-"))
            {
                // Clay soil
                return 5;
            }
            else if (codeToCheck.StartsWith("peat-"))
            {
                // Peat soil
                return 5;
            }
            else if (codeToCheck.StartsWith("soil-") || codeToCheck.StartsWith("forestfloor-") || codeToCheck.StartsWith("farmland-"))
            {
                // Ordinary dirt
                return 1;
            }
            return 0;
        }

        private int GetLeavesPoints(int blockId)
        {
            string codeToCheck = GetBlockCode(blockId);
            ServerApi.Logger.Debug($"[SeraphLeveling] Checking leaves points for block code {codeToCheck}.");
            if (codeToCheck.StartsWith("leavesbranchy-grown"))
            {
                return 2;
            }
            else if (codeToCheck.StartsWith("leaves-grown") || codeToCheck.StartsWith("leavesnarrow-grown"))
            {
                return 1;
            }
            return 0;
        }

        /// <summary>
        /// Determines the point value for a broken block.
        /// Returns OreMultiplier (default 5) for ore blocks, 1 for stone blocks, 0 for other blocks.
        ///
        /// Stone block patterns (1 point each):
        /// - rock-{type} (e.g., rock-granite, rock-limestone)
        /// - crackedrock-{type} (e.g., crackedrock-granite)
        ///
        /// Ore block patterns (OreMultiplier points):
        /// - Contains "ore-" (e.g., ore-copper-granite, ore-lignite-chalk)
        /// </summary>
        private int GetStoneBlockPoints(int blockId)
        {
            string codeToCheck = GetBlockCode(blockId);
            // Ore blocks: code contains "ore-" (e.g., "ore-lignite-chalk", "ore-copper-granite")
            if (codeToCheck.Contains("ore-"))
            {
                return OreMultiplier;
            }

            // Meteoric iron blocks (treat same as ore - high value)
            if (codeToCheck.StartsWith("meteorite") ||
                codeToCheck.Contains("meteoriciron"))
            {
                return OreMultiplier;
            }

            // Stone/rock blocks that should count for mining XP
            if (codeToCheck.StartsWith("rock-") ||           // Regular rock (rock-granite)
                codeToCheck.StartsWith("crackedrock-"))      // Cracked rock (crackedrock-granite)
            {
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Handler for /trait testwalkspeed command.
        /// Applies a test armor walk speed penalty reduction (positive = less penalty, 0 = clear).
        /// </summary>
        private TextCommandResult OnTraitTestWalkSpeedCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Player entity not found");
            }

            int? percent = (int?)args[0];

            if (!percent.HasValue)
            {
                return TextCommandResult.Success("Usage: /trait testwalkspeed [percent]\nExample: /trait testwalkspeed 99 (reduces armor penalty by 99%)\nUse 0 to clear the test modifier.");
            }

            if (percent.Value == 0)
            {
                player.Entity.Stats["armorWalkSpeedAffectedness"].Remove("sitTestPenalty");

                // Force WearableStats to recalculate
                var clearInv = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
                if (clearInv != null)
                {
                    foreach (var slot in clearInv)
                    {
                        if (slot?.Itemstack != null)
                        {
                            slot.MarkDirty();
                            break;
                        }
                    }
                }

                return TextCommandResult.Success("Test armor walk speed penalty modifier cleared.");
            }

            // armorWalkSpeedAffectedness: negative values reduce the penalty
            float reduction = -(percent.Value * 0.01f);
            player.Entity.Stats["armorWalkSpeedAffectedness"].Set("sitTestPenalty", reduction);

            // Debug: check blended value
            float blendedValue = player.Entity.Stats.GetBlended("armorWalkSpeedAffectedness");
            ServerApi.Logger.Debug($"[SeraphLeveling] Test command: set armorWalkSpeedAffectedness modifier to {reduction:F2}, blended value is now {blendedValue:F2}");

            // Force WearableStats to recalculate by triggering a slot change on character inventory
            var charInv = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (charInv != null)
            {
                // Trigger slot modified on first slot to force WearableStats recalculation
                foreach (var slot in charInv)
                {
                    if (slot?.Itemstack != null)
                    {
                        slot.MarkDirty();
                        break;
                    }
                }
                ServerApi.Logger.Debug($"[SeraphLeveling] Triggered character inventory refresh to recalculate wearable stats");
            }

            return TextCommandResult.Success($"Applied {percent.Value}% armor walk speed penalty reduction (stat value: {reduction:F2}, blended: {blendedValue:F2}). Use '/trait testwalkspeed 0' to clear.");
        }

        /// <summary>
        /// Calculate the melee damage bonus as an integer percentage (0 to 150).
        /// Each credit gives 1% bonus, capped at MaxMeleeDamagePercent.
        /// </summary>
        public static int CalculateMeleeBonusPercent(int credits)
        {
            return Math.Min(credits, MaxMeleeDamagePercent);
        }

        /// <summary>
        /// Get the maximum melee credits a player can earn based on their traits.
        /// Players with Farsighted or Nervous traits can earn extra credits
        /// to compensate for the penalty before gaining positive bonuses.
        /// </summary>
        public static int GetMaxMeleeCredits(EntityPlayer entity)
        {
            if (entity == null) return MaxMeleeDamagePercent;

            bool hasFarsighted = PlayerHasVanillaFarsighted(entity);
            bool hasNervous = PlayerHasVanillaNervous(entity);

            // Farsighted penalty is 15% melee damage, need 15 extra levels to cancel it
            if (hasFarsighted)
            {
                return MaxMeleeDamagePercent + VANILLA_FARSIGHTED_MELEE_PENALTY;
            }

            // Nervous penalty is 15% melee damage, need 15 extra levels to cancel it
            if (hasNervous)
            {
                return MaxMeleeDamagePercent + VANILLA_NERVOUS_MELEE_PENALTY;
            }

            return MaxMeleeDamagePercent;
        }

        /// <summary>
        /// Reliable check for whether the player has a vanilla trait. Reads from
        /// `characterTraits` and `characterClass` watched attributes (both reliably synced by
        /// vanilla VS) instead of our own `sitHasVanillaX` bools, which depend on a MarkPathDirty
        /// call that doesn't always cover sibling attributes and can leave the client reading
        /// the default `false` even when the player's class genuinely has the trait.
        /// </summary>
        public static bool PlayerHasTrait(EntityPlayer entity, TraitDefinition traitDefinition)
        {
            if (entity == null || traitDefinition == null)
            {
                return false;
            }

            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", []);
            foreach (string trait in classTraits)
            {
                if (trait.Equals(traitDefinition.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Fallback: check known classes from loaded mods that have the given trait
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return LoadedMods
                    .SelectMany(modDef => modDef.CharacterClasses)
                    .Where(charClassDef => charClassDef.Traits.Contains(traitDefinition))
                    .Select(charClassDef => charClassDef.Id)
                    .Any(id => characterClass.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Soldier trait.
        /// </summary>
        private static bool PlayerHasVanillaSoldierForArmor(EntityPlayer entity)
        {
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);

            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("soldier", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // Fallback: check known classes that have Soldier (Blackguard)
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("blackguard", StringComparison.OrdinalIgnoreCase);
        }

        // =========================================================================
        // NEGATIVE TRAIT DETECTION METHODS
        // =========================================================================

        /// <summary>
        /// Checks if the player's class has the vanilla Farsighted trait (Hunter).
        /// </summary>
        public static bool PlayerHasVanillaFarsighted(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("farsighted", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("hunter", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Nervous trait (Malefactor, Clockmaker).
        /// </summary>
        public static bool PlayerHasVanillaNervous(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("nervous", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("malefactor", StringComparison.OrdinalIgnoreCase) ||
                   characterClass.Equals("clockmaker", StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// Calculate the remaining penalty for a negative trait after applying progression bonus.
        /// Returns the remaining penalty (0 or positive), or 0 if fully cancelled.
        /// </summary>
        public static int CalculateRemainingPenalty(int basePenalty, int progressionBonus)
        {
            return Math.Max(0, basePenalty - progressionBonus);
        }

        /// <summary>
        /// Calculate armor durability bonus as an integer percentage.
        /// Accounts for vanilla Soldier trait (+15% armor durability).
        /// </summary>
        public static int CalculateArmorDurabilityBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasSoldier = entity != null && PlayerHasVanillaSoldierForArmor(entity);
            int vanillaBonus = hasSoldier ? VANILLA_SOLDIER_ARMOR_DURABILITY_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxArmorDurabilityPercent - vanillaBonus);
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Calculate armor walk speed penalty reduction bonus as an integer percentage.
        /// Accounts for vanilla Soldier trait (+25% armor walk speed penalty reduction).
        /// </summary>
        public static int CalculateArmorWalkSpeedBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasSoldier = entity != null && PlayerHasVanillaSoldierForArmor(entity);
            int vanillaBonus = hasSoldier ? VANILLA_SOLDIER_ARMOR_WALKSPEED_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxArmorWalkSpeedPercent - vanillaBonus);
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Determines the armor type from an item code.
        /// Returns: "light" (leather, gambeson), "chain", "brigandine", "scale", "plate", or null if not armor.
        /// </summary>
        public static string GetArmorType(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return null;

            string codeToCheck = itemCode.StartsWith("game:") ? itemCode.Substring(5) : itemCode;

            // Check if it's armor (starts with "armor-")
            if (!codeToCheck.StartsWith("armor-")) return null;

            // Determine armor type from code
            if (codeToCheck.Contains("-plate-")) return "plate";
            if (codeToCheck.Contains("-scale-")) return "scale";
            if (codeToCheck.Contains("-brigandine-")) return "brigandine";
            if (codeToCheck.Contains("-chain-")) return "chain";
            if (codeToCheck.Contains("-lamellar-")) return "chain"; // Treat lamellar as chain for first-equip
            if (codeToCheck.Contains("-leather-") || codeToCheck.Contains("-gambeson-") ||
                codeToCheck.Contains("-jerkin-") || codeToCheck.Contains("-improvised-"))
                return "light";

            // Default to light if unrecognized armor type
            return "light";
        }

        /// <summary>
        /// Gets the first-equip durability bonus for an armor type.
        /// Values are now configurable via config file.
        /// </summary>
        public static int GetFirstEquipBonus(string armorType)
        {
            switch (armorType?.ToLowerInvariant())
            {
                case "plate": return FirstEquipPlateBonus;
                case "scale": return FirstEquipScaleBonus;
                case "brigandine": return FirstEquipBrigandineBonus;
                case "chain": return FirstEquipChainBonus;
                case "light":
                default: return FirstEquipLightBonus;
            }
        }

        /// <summary>
        /// Gets the first-equip walk speed penalty reduction bonus for an armor type.
        /// Values are now configurable via config file.
        /// </summary>
        public static int GetFirstEquipWalkSpeedBonus(string armorType)
        {
            switch (armorType?.ToLowerInvariant())
            {
                case "plate": return FirstEquipWalkSpeedPlateBonus;
                case "scale": return FirstEquipWalkSpeedScaleBonus;
                case "brigandine": return FirstEquipWalkSpeedBrigandineBonus;
                case "chain": return FirstEquipWalkSpeedChainBonus;
                case "light":
                default: return FirstEquipWalkSpeedLightBonus;
            }
        }

        /// <summary>
        /// Initialize armor tracking for a player by checking their currently equipped armor.
        /// </summary>
        private void InitializePlayerArmorTracking(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;

            // Get the player's currently equipped armor
            var equippedArmor = new Dictionary<string, string>();

            // Check armor slots (head, body, legs) using character inventory
            var characterInventory = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (characterInventory != null)
            {
                // Armor slots are typically: 12 = head, 13 = body, 14 = legs (may vary)
                foreach (var slot in characterInventory)
                {
                    if (slot?.Itemstack?.Collectible != null)
                    {
                        string itemCode = slot.Itemstack.Collectible.Code?.ToString();
                        string armorType = GetArmorType(itemCode);
                        if (armorType != null)
                        {
                            string slotId = slot.Inventory?.InventoryID + "_" + slot.Inventory?.GetSlotId(slot);
                            equippedArmor[slotId] = itemCode;

                            // Check for first-time equip bonus
                            AttributeModifierDefinitions.ArmorDurability.GetForPlayer(playerUid).ApplyFirstTimeBonus(player, itemCode, GetFirstEquipBonus(armorType));
                            AttributeModifierDefinitions.ArmorWalkSpeed.GetForPlayer(playerUid).ApplyFirstTimeBonus(player, itemCode, GetFirstEquipBonus(armorType));
                        }
                    }
                }
            }

            playerEquippedArmor[playerUid] = equippedArmor;
        }

        /// <summary>
        /// Game tick handler for armor time tracking.
        /// Checks each player's equipped armor and accumulates time credits.
        /// Also detects armor equip/unequip for first-equip bonus.
        /// </summary>
        private void OnArmorTick(float dt)
        {
            if (ServerApi == null) return;

            foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;
                if (!player.Entity.Alive) continue;

                string playerUid = player.PlayerUID;
                var currentArmor = new Dictionary<string, string>();

                // Get the player's currently equipped armor using character inventory
                var characterInventory = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
                if (characterInventory != null)
                {
                    foreach (var slot in characterInventory)
                    {
                        if (slot?.Itemstack?.Collectible != null)
                        {
                            string itemCode = slot.Itemstack.Collectible.Code?.ToString();
                            string armorType = GetArmorType(itemCode);
                            if (armorType != null)
                            {
                                string slotId = slot.Inventory?.InventoryID + "_" + slot.Inventory?.GetSlotId(slot);
                                currentArmor[slotId] = itemCode;
                            }
                        }
                    }
                }

                // Get previous armor state
                var previousArmor = playerEquippedArmor.GetOrAdd(playerUid, _ => new Dictionary<string, string>());

                // Check for newly equipped armor (first-equip bonus) and track time worn
                foreach (var kvp in currentArmor)
                {
                    string slotId = kvp.Key;
                    string itemCode = kvp.Value;

                    // Check if this is new armor in this slot
                    if (!previousArmor.TryGetValue(slotId, out string prevArmor) || prevArmor != itemCode)
                    {
                        AttributeModifierDefinitions.ArmorDurability.GetForPlayer(playerUid).ApplyFirstTimeBonus(player, itemCode, GetFirstEquipBonus(GetArmorType(itemCode)));
                        AttributeModifierDefinitions.ArmorWalkSpeed.GetForPlayer(playerUid).ApplyFirstTimeBonus(player, itemCode, GetFirstEquipBonus(GetArmorType(itemCode)));
                    }
                    AttributeModifierDefinitions.ArmorWalkSpeed.GetForPlayer(playerUid).DoEvent(player, itemCode, 1f);
                    AttributeModifierDefinitions.ArmorHealing.GetForPlayer(playerUid).DoEvent(player, itemCode, 1f);
                    AttributeModifierDefinitions.ArmorHungerRate.GetForPlayer(playerUid).DoEvent(player, itemCode, 1f);
                }
                // Update the equipped armor tracking
                playerEquippedArmor[playerUid] = currentArmor;
            }
        }

        /// <summary>
        /// Process armor damage blocked. Called from Harmony patch when player takes damage.
        /// </summary>
        public static void ProcessArmorDamageBlocked(IServerPlayer player, float damageBlocked, string armorCode)
        {
            if (player?.Entity == null || string.IsNullOrEmpty(armorCode)) return;

            string playerUid = player.PlayerUID;


            AttributeModifierDefinitions.ArmorDurability.GetForPlayer(playerUid).DoEvent(player, armorCode, damageBlocked, ArmorDurabilityProgressTypes.DamageBlocked);
        }

        /// <summary>
        /// Process armor repair. Called from Harmony patch when armor is repaired.
        /// </summary>
        public static void ProcessArmorRepair(IServerPlayer player, string armorCode)
        {
            if (player?.Entity == null || string.IsNullOrEmpty(armorCode)) return;

            string playerUid = player.PlayerUID;
            AttributeModifierDefinitions.ArmorDurability.GetForPlayer(playerUid).DoEvent(player, armorCode, 1, ArmorDurabilityProgressTypes.RepairProgress);
        }

        /// <summary>
        /// Called when a player breaks a block. Updates mining progress based on new mechanics:
        /// - Only counts blocks broken with pickaxes
        /// - Only counts stone (1 point) and ore (5 points) blocks
        /// - Each pickaxe type tracks its own increment progress independently
        /// </summary>
        private void OnBlockBroken(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            if (byPlayer?.Entity == null) return;

            // Check for Forager progression (wild crops on dirt, not farmland)
            if (IsWildCropBlock(oldblockId, blockSel?.Position))
            {
                ProcessWildCropBroken(byPlayer);
            }

            // Check for Pilferer progression (cracked vessels only - they can't be re-placed)
            if (IsCrackedVesselBlock(oldblockId))
            {
                ProcessVesselBreak(byPlayer);
            }

            // Check for Pitmaster progression (ready-to-harvest pit charcoal)
            if (IsCharcoalPile(oldblockId, out int charcoalPoints))
            {
                ProcessCharcoalBreak(byPlayer, charcoalPoints);
            }

            string playerUid = byPlayer.PlayerUID;

            // Check if player is using a tool for progression
            string pickaxeCode = GetHeldPickaxeCode(byPlayer);
            string axeCode = GetHeldAxeCode(byPlayer);
            string shovelCode = GetHeldShovelCode(byPlayer);
            string shearsCode = GetHeldShearsCode(byPlayer);

            // Handle pickaxe specific attributes
            if (pickaxeCode != null)
            {
                // Check block type and get points
                int points = GetStoneBlockPoints(oldblockId);
                if (points > 0)
                {
                    AttributeModifierDefinitions.MiningSpeed.GetForPlayer(playerUid).DoEvent(byPlayer, pickaxeCode, points);
                    AttributeModifierDefinitions.OreDropRate.GetForPlayer(playerUid).DoEvent(byPlayer, pickaxeCode, points);
                    AttributeModifierDefinitions.StoneDropRate.GetForPlayer(playerUid).DoEvent(byPlayer, pickaxeCode, points);
                }
            }

            // Handle axe specific attributes
            if (axeCode != null)
            {
                // Check block type and get points
                int woodPoints = GetWoodLogPoints(oldblockId);
                if (woodPoints > 0)
                {
                    AttributeModifierDefinitions.TreeChoppingSpeed.GetForPlayer(playerUid).DoEvent(byPlayer, axeCode, woodPoints);
                    AttributeModifierDefinitions.AxeDamage.GetForPlayer(playerUid).DoEvent(byPlayer, axeCode, woodPoints);
                }
            }

            // Handle shovel specific attributes
            if (shovelCode != null)
            {
                // Check block type and get points
                int dirtPoints = GetDirtPoints(oldblockId);
                if (dirtPoints > 0)
                {
                    AttributeModifierDefinitions.ClayDropRate.GetForPlayer(playerUid).DoEvent(byPlayer, shovelCode, dirtPoints);
                    AttributeModifierDefinitions.ClayformSpeed.GetForPlayer(playerUid).DoEvent(byPlayer, shovelCode, dirtPoints);
                    AttributeModifierDefinitions.PeatDropRate.GetForPlayer(playerUid).DoEvent(byPlayer, shovelCode, dirtPoints);
                }
            }

            // Handle attributes satisfied by an axe or shears
            if (shearsCode != null || axeCode != null)
            {
                // Check block type and get points
                string toolCode = shearsCode ?? axeCode;
                int leavesPoints = GetLeavesPoints(oldblockId);
                if (leavesPoints > 0)
                {
                    AttributeModifierDefinitions.WoodDropRate.GetForPlayer(playerUid).DoEvent(byPlayer, toolCode, leavesPoints);
                    AttributeModifierDefinitions.SeedDropRate.GetForPlayer(playerUid).DoEvent(byPlayer, toolCode, leavesPoints);
                    AttributeModifierDefinitions.StickDropRate.GetForPlayer(playerUid).DoEvent(byPlayer, toolCode, leavesPoints);
                }
            }
        }

        /// <summary>
        /// Called every 500ms to track walking distance for all online players.
        /// Calculates 2D horizontal distance moved (ignoring Y-axis for climbing/falling).
        /// </summary>
        private void OnWalkingTick(float dt)
        {
            foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;

                string playerUid = player.PlayerUID;
                double currentX = player.Entity.Pos.X;
                double currentZ = player.Entity.Pos.Z;

                // Get or initialize last position (using Position2D struct to avoid Vec3d allocations)
                if (!lastPlayerPositions.TryGetValue(playerUid, out Position2D lastPos))
                {
                    lastPlayerPositions[playerUid] = new Position2D(currentX, currentZ);
                    continue;
                }

                // Calculate 2D horizontal distance (ignore Y axis to avoid counting climbing/falling)
                double dx = currentX - lastPos.X;
                double dz = currentZ - lastPos.Z;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz);

                // Update last position (no allocation - struct assignment)
                lastPlayerPositions[playerUid] = new Position2D(currentX, currentZ);

                // Skip if no movement or teleportation (too far)
                if (distance < 0.01f || distance > MAX_DISTANCE_PER_TICK) continue;

                // Get or create player progress data
                AttributeModifierDefinitions.WalkingSpeed.GetForPlayer(playerUid).DoEvent(player, distance);

                if (IsStandingOnPath(player.Entity))
                {
                    AttributeModifierDefinitions.Townie.GetForPlayer(playerUid).DoEvent(player, distance);
                }

            }
        }

        private bool IsStandingOnPath(EntityPlayer entity)
        {
            BlockPos feetPos = new BlockPos(
                (int)Math.Floor(entity.Pos.X),
                (int)Math.Floor(entity.Pos.Y - 0.05),
                (int)Math.Floor(entity.Pos.Z)
            );

            BlockPos belowPos = feetPos.DownCopy();

            return IsPathBlock(feetPos) || IsPathBlock(belowPos);
        }

        private bool IsPathBlock(BlockPos pos)
        {
            Block block = ServerApi.World.BlockAccessor.GetBlock(pos);
            if (block?.Code == null) return false;

            string code = block.Code.ToString().ToLowerInvariant();

            return code.Contains("path");
        }

        /// <summary>
        /// Called every 1000ms (1 second) to track time spent at full saturation for all online players.
        /// Players at maximum saturation (1500/1500) accumulate time toward hunger rate reduction.
        /// </summary>
        private void OnHungerTick(float dt)
        {
            foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;

                string playerUid = player.PlayerUID;

                // Get the player's hunger data from WatchedAttributes
                var hungerTree = player.Entity.WatchedAttributes.GetTreeAttribute("hunger");
                if (hungerTree == null) continue;

                // Check if player is at full saturation (1500/1500)
                float currentSaturation = hungerTree.GetFloat("currentsaturation", 0);
                float maxSaturation = hungerTree.GetFloat("maxsaturation", 1500);

                // Only count time when at exactly max saturation
                if (currentSaturation < maxSaturation) continue;

                var playerProgress = AttributeModifierDefinitions.HungerRate.GetForPlayer(playerUid);
                playerProgress.DoEvent(player, 1f);
            }
        }

        /// <summary>
        /// Called when a player disconnects. Cleans up their position, armor tracking, and cached data.
        /// Also triggers a save of all progress data to prevent data loss.
        /// </summary>
        private void OnPlayerDisconnect(IServerPlayer byPlayer)
        {
            if (byPlayer == null) return;
            string playerUid = byPlayer.PlayerUID;
            lastPlayerPositions.TryRemove(playerUid, out _);
            lastSneakingPositions.TryRemove(playerUid, out _);
            playerEquippedArmor.TryRemove(playerUid, out _);
            VanillaTraitsCache.TryRemove(playerUid, out _);
            LastDecayCheckDay.TryRemove(playerUid, out _);

            // Save all pending progress data to prevent data loss on disconnect
            SaveAllPendingProgress();
        }

        /// <summary>
        /// Called periodically by auto-save timer to persist all pending progress.
        /// Only saves when players are online to avoid waking up idle dedicated servers.
        /// </summary>
        private void OnAutoSaveTick(float dt)
        {
            // Don't save if no players are online - this prevents waking up idle dedicated servers
            if (ServerApi?.World?.AllOnlinePlayers == null || ServerApi.World.AllOnlinePlayers.Length == 0)
            {
                return;
            }

            SaveAllPendingProgress();
        }

        /// <summary>
        /// Saves all pending progress data. Called on player disconnect and auto-save tick.
        /// </summary>
        private void SaveAllPendingProgress()
        {
            // Reuse the same logic as OnGameWorldSave
            OnGameWorldSave();
        }

        /// <summary>
        /// Populates the vanilla traits cache for a player.
        /// This reads the characterTraits array once and caches all trait booleans.
        /// </summary>
        private static void PopulateVanillaTraitsCache(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var entity = player.Entity;

            // Get character traits once
            string[] characterTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null) ?? Array.Empty<string>();
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "")?.ToLowerInvariant() ?? "";

            // Debug logging for trait detection
            ServerApi?.Logger?.Debug($"[SeraphLeveling] PopulateVanillaTraitsCache for {player.PlayerName}: class='{characterClass}', traits=[{string.Join(", ", characterTraits)}]");

            // Create a HashSet for O(1) lookups
            var traitSet = new HashSet<string>(characterTraits, StringComparer.OrdinalIgnoreCase);

            var cache = new CachedVanillaTraits
            {
                HasHardy = traitSet.Contains("hardy") || characterClass == "blackguard",
                HasSoldier = traitSet.Contains("soldier") || characterClass == "blackguard",
                HasFocused = traitSet.Contains("focused") || characterClass == "hunter",
                HasFleetfooted = traitSet.Contains("fleetfooted") || characterClass == "hunter" || characterClass == "clockmaker",
                HasRavenous = traitSet.Contains("ravenous") || characterClass == "blackguard",
                HasFarsighted = traitSet.Contains("farsighted") || characterClass == "hunter",
                HasNervous = traitSet.Contains("nervous") || characterClass == "malefactor" || characterClass == "clockmaker",
                HasNearsighted = traitSet.Contains("nearsighted") || characterClass == "blackguard",
                HasFrail = traitSet.Contains("frail") || characterClass == "malefactor" || characterClass == "clockmaker",
                HasCivil = traitSet.Contains("civil") || characterClass == "tailor",
                HasWeak = traitSet.Contains("weak") || characterClass == "tailor",
                HasKind = traitSet.Contains("kind") || characterClass == "tailor",
                HasHeavyhanded = traitSet.Contains("heavyhanded") || characterClass == "blackguard",
                HasClaustrophobic = traitSet.Contains("claustrophobic") || characterClass == "hunter",
                HasFurtive = traitSet.Contains("furtive") || characterClass == "malefactor",
                HasPrecise = traitSet.Contains("precise") || characterClass == "clockmaker",
                HasMender = traitSet.Contains("mender") || characterClass == "tailor",
                HasPilferer = traitSet.Contains("pilferer") || characterClass == "malefactor",
                HasResourceful = traitSet.Contains("resourceful") || characterClass == "hunter" || characterClass == "malefactor",
                HasForager = traitSet.Contains("forager") || characterClass == "hunter" || characterClass == "malefactor",

                // Combat Overhaul traits — gated on IsCombatOverhaulLoaded. CO traits don't
                // exist in the game world unless CO itself is loaded, so the class fallbacks
                // (e.g. "Blackguard always has Trembling Aim") should only fire when CO is
                // actually installed. Otherwise the cache reports phantom CO traits and downstream
                // Apply* functions write penalty values into watched attrs that the postfix then
                // renders as ghost debuffs (the "Trembling Aim -30%" symptom on Blackguard with
                // CO uninstalled).
                HasCOTremblingAim = IsCombatOverhaulLoaded && (traitSet.Contains("tremblingaim") || traitSet.Contains("trembling aim") || characterClass == "blackguard"),
                HasCOClumsyHands = IsCombatOverhaulLoaded && (traitSet.Contains("clumsyhands") || traitSet.Contains("clumsy hands")),
                HasCOFearOfMelee = IsCombatOverhaulLoaded && (traitSet.Contains("fearofmelee") || traitSet.Contains("fear of melee") || traitSet.Contains("frightenedofmelee") || traitSet.Contains("frightened of melee") || characterClass == "clockmaker"),
                HasCOWeakHand = IsCombatOverhaulLoaded && (traitSet.Contains("weakhand") || traitSet.Contains("weak hand")),
                HasCONervous = IsCombatOverhaulLoaded && (traitSet.Contains("nervous") || characterClass == "malefactor" || characterClass == "clockmaker"),

                // Combat Overhaul mixed/positive traits (Big Head, Thick Skull, Leg Day, Melee Expert, Self Defence)
                HasCOBigHead = IsCombatOverhaulLoaded && (traitSet.Contains("bighead") || traitSet.Contains("big head") || characterClass == "clockmaker"),
                HasCOThickSkull = IsCombatOverhaulLoaded && (traitSet.Contains("thickskull") || traitSet.Contains("thick skull") || characterClass == "malefactor"),
                HasCOLegDay = IsCombatOverhaulLoaded && (traitSet.Contains("legday") || traitSet.Contains("leg day") || characterClass == "blackguard"),
                HasCOMeleeExpert = IsCombatOverhaulLoaded && (traitSet.Contains("meleeexpert") || traitSet.Contains("melee expert") || traitSet.Contains("expert in melee") || characterClass == "blackguard"),
                HasCOSelfDefence = IsCombatOverhaulLoaded && (traitSet.Contains("selfdefence") || traitSet.Contains("self defence") || characterClass == "tailor")
            };

            VanillaTraitsCache[playerUid] = cache;

            ServerApi?.Logger?.Debug($"[SeraphLeveling] Cached traits: HasClaustrophobic={cache.HasClaustrophobic}, HasFocused={cache.HasFocused}, HasFleetfooted={cache.HasFleetfooted}, HasFarsighted={cache.HasFarsighted}");
        }

        /// <summary>
        /// Gets the cached vanilla traits for a player. Returns null if not cached.
        /// </summary>
        public static CachedVanillaTraits GetCachedTraits(string playerUid)
        {
            VanillaTraitsCache.TryGetValue(playerUid, out var cache);
            return cache;
        }

        /// <summary>
        /// Called when a player joins. Applies their saved bonuses (mining, melee, ranged, walking, and hunger).
        /// </summary>
        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            if (byPlayer?.Entity == null) return;

            string playerUid = byPlayer.PlayerUID;

            // Populate vanilla traits cache first (before applying any bonuses)
            PopulateVanillaTraitsCache(byPlayer);

            // Initialize decay check day for this player (online-only decay)
            if (EnableSkillDecay)
            {
                double currentDay = ServerApi.World.Calendar.TotalDays;
                LastDecayCheckDay[playerUid] = currentDay;
            }

            foreach (var definition in LoadedAttributes)
            {
                definition.HandleLogin(byPlayer);
            }

            // Apply Combat Overhaul proficiency bonuses (if CO is loaded)
            if (IsCOCompatEnabled)
            {
                ApplyAllCOBonuses(byPlayer);
                if (COProgress.TryGetValue(playerUid, out var coProgress))
                {
                    int totalCOCredits = coProgress.SteadyAimCredits;
                    foreach (var prof in coProgress.Proficiencies)
                    {
                        totalCOCredits += prof.Value.TotalCredits;
                    }
                    if (totalCOCredits > 0)
                    {
                        ServerApi.Logger.Debug($"[SeraphLeveling] Applied CO bonuses ({totalCOCredits} total credits) to player {byPlayer.PlayerName}");
                    }
                }
            }

            // Initialize equipped armor tracking for this player
            InitializePlayerArmorTracking(byPlayer);
        }

        /// <summary>
        /// Apply the mining speed bonus to a player based on their level.
        /// Sends a level-up notification to the player (chat message and/or sound),
        /// respecting the EnableLevelUpMessages and EnableLevelUpSound config options.
        /// </summary>
        public static void NotifyLevelUp(IServerPlayer player, string message)
        {
            if (EnableLevelUpMessages)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, message, EnumChatType.Notification);
            }

            if (EnableLevelUpSound && serverSoundChannel != null)
            {
                try
                {
                    serverSoundChannel.SendPacket(new LevelUpSoundMessage { SoundName = LevelUpSoundName, Volume = LevelUpSoundVolume }, player);
                }
                catch (Exception ex)
                {
                    ServerApi?.Logger.Warning($"[SeraphLeveling] Failed to send level-up sound to {player.PlayerName}: {ex.Message}");
                }
            }
        }

        // Server-side Harmony instance for melee damage tracking
        private Harmony serverHarmony;

        /// <summary>
        /// Apply Harmony patches for server-side melee damage tracking.
        /// </summary>
        private void ApplyServerHarmonyPatches(ICoreServerAPI api)
        {
            const string HARMONY_SERVER_ID = "seraphleveling.server";
            serverHarmony = new Harmony(HARMONY_SERVER_ID);

            try
            {
                if (!Harmony.HasAnyPatches(HARMONY_SERVER_ID))
                {
                    // Find Entity.ReceiveDamage method
                    var entityType = typeof(Entity);
                    api.Logger.Debug($"[SeraphLeveling] Looking for Entity.ReceiveDamage method in {entityType.FullName}");

                    var receiveDamageMethod = entityType.GetMethod("ReceiveDamage",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    if (receiveDamageMethod == null)
                    {
                        // Try to list available methods for debugging
                        var methods = entityType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        var damageMethodNames = methods.Where(m => m.Name.Contains("Damage")).Select(m => m.Name).ToArray();
                        api.Logger.Warning($"[SeraphLeveling] Could not find Entity.ReceiveDamage method. Available damage methods: {string.Join(", ", damageMethodNames)}");
                        return;
                    }

                    api.Logger.Debug($"[SeraphLeveling] Found Entity.ReceiveDamage: {receiveDamageMethod}");

                    // Get our postfix method
                    var postfixMethod = AccessTools.Method(typeof(EntityDamagePatches),
                        nameof(EntityDamagePatches.ReceiveDamage_Postfix));

                    if (postfixMethod == null)
                    {
                        api.Logger.Error("[SeraphLeveling] Could not find ReceiveDamage_Postfix method!");
                        return;
                    }

                    api.Logger.Debug($"[SeraphLeveling] Found postfix method: {postfixMethod}");

                    serverHarmony.Patch(receiveDamageMethod, postfix: new HarmonyMethod(postfixMethod));
                    api.Logger.Notification("[SeraphLeveling] Successfully patched Entity.ReceiveDamage for damage tracking");

                    // Patch EntityBehaviorHarvestable.SetHarvested for Resourceful trait (animal harvesting)
                    PatchAnimalHarvesting(api);

                    // Patch CollectibleObject.OnHeldInteractStep for Mender trait (sewing kit repairs)
                    PatchSewingKitRepairs(api);

                    // Patch BlockEntityStaticTranslocator.DoRepair for Technical trait (translocator repairs)
                    PatchTranslocatorRepairs(api);

                    // Patch BEBehaviorBed.DidSleep for sleep buff system
                    PatchBedSleeping(api);

                    // Patch Entity.Die for death penalty system
                    PatchEntityDeath(api);

                    // Patch crafting methods for recipe crafting detection
                    PatchGridCrafting(api);

                    // Patch ItemPoultice.OnHeldInteractStop for Medic trait (poultice/bandage healing)
                    PatchPoulticeHealing(api);
                }
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[SeraphLeveling] Failed to apply server Harmony patches: {ex.Message}");
            }
        }

        private void PatchGridCrafting(ICoreServerAPI api)
        {
            try
            {
                var gridRecipeType = typeof(GridRecipe);
                if (gridRecipeType == null)
                {
                    api.Logger.Debug("[SeraphLeveling] Could not find GridRecipe type for crafting hooks");
                    return;
                }

                var consumeInputMethod = AccessTools.Method(gridRecipeType, "ConsumeInput");
                if (consumeInputMethod == null)
                {
                    api.Logger.Debug("[SeraphLeveling] Could not find GridRecipe.ConsumeInput method for crafting hooks");
                    return;
                }

                // Get our postfix method
                var postfixMethod = AccessTools.Method(typeof(CraftingPatches), nameof(CraftingPatches.GridRecipeConsumeInput_Postfix));
                if (postfixMethod == null)
                {
                    api.Logger.Error("[SeraphLeveling] Could not find GridRecipeConsumeInput_Postfix method!");
                    return;
                }

                serverHarmony.Patch(consumeInputMethod, postfix: new HarmonyMethod(postfixMethod));
                api.Logger.Notification("[SeraphLeveling] Successfully patched GridRecipe.ConsumeInput for crafting hooks");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch GridRecipe.ConsumeInput: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch BlockEntityBed.DidUnmount to apply sleep buff when players wake up.
        /// </summary>
        private void PatchBedSleeping(ICoreServerAPI api)
        {
            if (!EnableSleepBuff) return; // Only patch if sleep buff is enabled

            try
            {
                // BlockEntityBed is in Vintagestory.GameContent (already imported)
                var bedBehaviorType = typeof(BlockEntityBed);
                if (bedBehaviorType == null)
                {
                    api.Logger.Debug("[SeraphLeveling] Could not find BlockEntityBed type for sleep buff");
                    return;
                }

                // Find the DidUnmount method (called when player gets out of bed)
                var didUnmountMethod = AccessTools.Method(bedBehaviorType, "DidUnmount");
                if (didUnmountMethod == null)
                {
                    api.Logger.Debug("[SeraphLeveling] Could not find BlockEntityBed.DidUnmount method for sleep buff");
                    return;
                }

                // Get our postfix method
                var postfixMethod = AccessTools.Method(typeof(BedSleepPatches), nameof(BedSleepPatches.DidUnmount_Postfix));
                if (postfixMethod == null)
                {
                    api.Logger.Error("[SeraphLeveling] Could not find DidUnmount_Postfix method!");
                    return;
                }

                serverHarmony.Patch(didUnmountMethod, postfix: new HarmonyMethod(postfixMethod));
                api.Logger.Notification("[SeraphLeveling] Successfully patched BlockEntityBed.DidUnmount for sleep buff");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch BlockEntityBed.DidUnmount: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch Entity.Die to apply death penalty when players die.
        /// </summary>
        private void PatchEntityDeath(ICoreServerAPI api)
        {
            if (!EnableDeathPenalty) return;

            try
            {
                var entityType = typeof(Entity);
                var dieMethod = entityType.GetMethod("Die",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (dieMethod == null)
                {
                    api.Logger.Warning("[SeraphLeveling] Could not find Entity.Die method for death penalty");
                    return;
                }

                var postfixMethod = AccessTools.Method(typeof(EntityDeathPatches),
                    nameof(EntityDeathPatches.Die_Postfix));
                if (postfixMethod == null)
                {
                    api.Logger.Error("[SeraphLeveling] Could not find Die_Postfix method!");
                    return;
                }

                serverHarmony.Patch(dieMethod, postfix: new HarmonyMethod(postfixMethod));
                api.Logger.Notification("[SeraphLeveling] Successfully patched Entity.Die for death penalty");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch Entity.Die: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch EntityBehaviorHarvestable.SetHarvested to track animal harvesting for Resourceful trait.
        /// </summary>
        private void PatchAnimalHarvesting(ICoreServerAPI api)
        {
            try
            {
                // Find the EntityBehaviorHarvestable type in VSSurvivalMod
                var harvestableType = AccessTools.TypeByName("Vintagestory.GameContent.EntityBehaviorHarvestable");
                if (harvestableType == null)
                {
                    api.Logger.Warning("[SeraphLeveling] Could not find EntityBehaviorHarvestable type");
                    return;
                }

                // Find the SetHarvested method
                var setHarvestedMethod = AccessTools.Method(harvestableType, "SetHarvested");
                if (setHarvestedMethod == null)
                {
                    // Try alternative method name
                    setHarvestedMethod = AccessTools.Method(harvestableType, "SetHarvestedBy");
                }
                if (setHarvestedMethod == null)
                {
                    api.Logger.Warning("[SeraphLeveling] Could not find SetHarvested or SetHarvestedBy method in EntityBehaviorHarvestable");
                    return;
                }

                // Get our postfix method
                var postfixMethod = AccessTools.Method(typeof(HarvestingPatches),
                    nameof(HarvestingPatches.SetHarvested_Postfix));

                serverHarmony.Patch(setHarvestedMethod, postfix: new HarmonyMethod(postfixMethod));
                api.Logger.Notification("[SeraphLeveling] Successfully patched EntityBehaviorHarvestable.SetHarvested for Resourceful trait");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch EntityBehaviorHarvestable: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch methods to track sewing kit repairs for Mender trait.
        /// Tries multiple approaches since sewing kit repairs can happen in different ways.
        /// </summary>
        private void PatchSewingKitRepairs(ICoreServerAPI api)
        {
            bool anyPatchSucceeded = false;

            // Approach 1: Try to patch ItemSewingKit directly if it exists
            try
            {
                var sewingKitType = AccessTools.TypeByName("Vintagestory.GameContent.ItemSewingKit");
                if (sewingKitType != null)
                {
                    // Try to find repair-related methods
                    var onHeldInteractStopMethod = AccessTools.Method(sewingKitType, "OnHeldInteractStop");
                    if (onHeldInteractStopMethod != null)
                    {
                        var postfixMethod = AccessTools.Method(typeof(SewingKitPatches),
                            nameof(SewingKitPatches.OnHeldInteractStop_Postfix));
                        serverHarmony.Patch(onHeldInteractStopMethod, postfix: new HarmonyMethod(postfixMethod));
                        api.Logger.Notification("[SeraphLeveling] Successfully patched ItemSewingKit.OnHeldInteractStop for Mender trait");
                        anyPatchSucceeded = true;
                    }
                }
            }
            catch (Exception ex)
            {
                api.Logger.Debug($"[SeraphLeveling] ItemSewingKit patch attempt: {ex.Message}");
            }

            // Approach 2: Patch CollectibleObject.OnModifiedInInventorySlot to detect durability restoration
            try
            {
                var collectibleType = typeof(CollectibleObject);
                var onModifiedMethod = AccessTools.Method(collectibleType, "OnModifiedInInventorySlot");
                if (onModifiedMethod != null)
                {
                    var postfixMethod = AccessTools.Method(typeof(SewingKitPatches),
                        nameof(SewingKitPatches.OnModifiedInInventorySlot_Postfix));
                    serverHarmony.Patch(onModifiedMethod, postfix: new HarmonyMethod(postfixMethod));
                    api.Logger.Notification("[SeraphLeveling] Successfully patched CollectibleObject.OnModifiedInInventorySlot for Mender trait");
                    anyPatchSucceeded = true;
                }
            }
            catch (Exception ex)
            {
                api.Logger.Debug($"[SeraphLeveling] OnModifiedInInventorySlot patch attempt: {ex.Message}");
            }

            // Approach 3: Patch OnHeldInteractStep as fallback
            try
            {
                var collectibleType = typeof(CollectibleObject);
                var onHeldInteractStepMethod = AccessTools.Method(collectibleType, "OnHeldInteractStep");
                if (onHeldInteractStepMethod != null)
                {
                    var postfixMethod = AccessTools.Method(typeof(SewingKitPatches),
                        nameof(SewingKitPatches.OnHeldInteractStep_Postfix));
                    serverHarmony.Patch(onHeldInteractStepMethod, postfix: new HarmonyMethod(postfixMethod));
                    api.Logger.Notification("[SeraphLeveling] Successfully patched CollectibleObject.OnHeldInteractStep for Mender trait");
                    anyPatchSucceeded = true;
                }
            }
            catch (Exception ex)
            {
                api.Logger.Debug($"[SeraphLeveling] OnHeldInteractStep patch attempt: {ex.Message}");
            }

            if (!anyPatchSucceeded)
            {
                api.Logger.Warning("[SeraphLeveling] Could not patch any method for Mender trait (sewing kit repairs)");
            }
        }

        /// <summary>
        /// Patch methods to track poultice healing for the Medic trait.
        /// Tries multiple approaches since poultice healing can happen in different ways.
        /// </summary>
        private void PatchPoulticeHealing(ICoreServerAPI api)
        {
            bool anyPatchSucceeded = false;

            // Approach 1: Try to patch ItemPoultice directly if it exists
            try
            {
                var poulticeType = AccessTools.TypeByName("Vintagestory.GameContent.ItemPoultice");
                if (poulticeType != null)
                {
                    // Try to find healing-related methods
                    var onHeldInteractStopMethod = AccessTools.Method(poulticeType, "OnHeldInteractStop");
                    if (onHeldInteractStopMethod != null)
                    {
                        var postfixMethod = AccessTools.Method(typeof(PoulticePatches),
                            nameof(PoulticePatches.OnHeldInteractStop_Postfix));
                        serverHarmony.Patch(onHeldInteractStopMethod, postfix: new HarmonyMethod(postfixMethod));
                        api.Logger.Notification("[SeraphLeveling] Successfully patched ItemPoultice.OnHeldInteractStop for Medic trait");
                        anyPatchSucceeded = true;
                    }
                }
            }
            catch (Exception ex)
            {
                api.Logger.Debug($"[SeraphLeveling] ItemPoultice patch attempt: {ex.Message}");
            }

            // Approach 2: Patch OnHeldInteractStep as fallback
            try
            {
                var collectibleType = typeof(CollectibleObject);
                var onHeldInteractStepMethod = AccessTools.Method(collectibleType, "OnHeldInteractStep");
                if (onHeldInteractStepMethod != null)
                {
                    var postfixMethod = AccessTools.Method(typeof(PoulticePatches),
                        nameof(PoulticePatches.OnHeldInteractStep_Postfix));
                    serverHarmony.Patch(onHeldInteractStepMethod, postfix: new HarmonyMethod(postfixMethod));
                    api.Logger.Notification("[SeraphLeveling] Successfully patched CollectibleObject.OnHeldInteractStep for Medic trait");
                    anyPatchSucceeded = true;
                }
            }
            catch (Exception ex)
            {
                api.Logger.Debug($"[SeraphLeveling] CollectibleObject.OnHeldInteractStep patch attempt: {ex.Message}");
            }

            if (!anyPatchSucceeded)
            {
                api.Logger.Warning("[SeraphLeveling] Could not patch any method for Medic trait (poultice healing)");
            }
        }

        /// <summary>
        /// Patch BlockEntityStaticTranslocator.DoRepair to track translocator repairs for Technical trait.
        /// </summary>
        private void PatchTranslocatorRepairs(ICoreServerAPI api)
        {
            try
            {
                // Find the BlockEntityStaticTranslocator type
                var translocatorType = AccessTools.TypeByName("Vintagestory.GameContent.BlockEntityStaticTranslocator");
                if (translocatorType == null)
                {
                    api.Logger.Warning("[SeraphLeveling] Could not find BlockEntityStaticTranslocator type");
                    return;
                }

                // Find the DoRepair method
                var doRepairMethod = AccessTools.Method(translocatorType, "DoRepair");
                if (doRepairMethod == null)
                {
                    api.Logger.Warning("[SeraphLeveling] Could not find DoRepair method in BlockEntityStaticTranslocator");
                    return;
                }

                // Get our postfix method
                var postfixMethod = AccessTools.Method(typeof(TranslocatorPatches),
                    nameof(TranslocatorPatches.DoRepair_Postfix));

                serverHarmony.Patch(doRepairMethod, postfix: new HarmonyMethod(postfixMethod));
                api.Logger.Notification("[SeraphLeveling] Successfully patched BlockEntityStaticTranslocator.DoRepair for Technical trait");
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"[SeraphLeveling] Failed to patch BlockEntityStaticTranslocator.DoRepair: {ex.Message}");
            }
        }

        /// <summary>
        /// Process melee damage dealt by a player. Called from Harmony patch.
        /// </summary>
        public static void ProcessMeleeDamage(IServerPlayer attackerPlayer, string weaponType, float damage)
        {
            if (attackerPlayer?.Entity == null || string.IsNullOrEmpty(weaponType)) return;

            string playerUid = attackerPlayer.PlayerUID;

            var damageProgress = AttributeModifierDefinitions.MeleeDamage.GetForPlayer(playerUid);
            damageProgress.DoEvent(attackerPlayer, weaponType, damage);
        }

        /// <summary>
        /// Static version of UpdateExtraTrait for use from Harmony patches.
        /// </summary>
        public static void UpdateExtraTraitStatic(EntityPlayer entity, string traitCode, bool shouldHave)
        {
            string[] currentTraits = entity.WatchedAttributes.GetStringArray("extraTraits", null) ?? Array.Empty<string>();
            bool hasTrait = currentTraits.Contains(traitCode);

            if (shouldHave && !hasTrait)
            {
                var newTraits = currentTraits.Append(traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("extraTraits", newTraits);
                entity.WatchedAttributes.MarkPathDirty("extraTraits");
            }
            else if (!shouldHave && hasTrait)
            {
                var newTraits = currentTraits.Where(t => t != traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("extraTraits", newTraits);
                entity.WatchedAttributes.MarkPathDirty("extraTraits");
            }
        }

        /// <summary>
        /// Gets the weapon code from a held item if it's a qualifying melee weapon, or null otherwise.
        /// Returns the full item code (e.g., "game:sword-copper") to track each weapon type individually.
        /// Static version for use from Harmony patches.
        /// </summary>
        public static string GetWeaponTypeFromCode(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return null;

            // Remove namespace prefix for pattern matching
            string codeToCheck = itemCode;
            if (itemCode.Contains(":"))
            {
                codeToCheck = itemCode.Substring(itemCode.IndexOf(':') + 1);
            }
            string lowerCode = codeToCheck.ToLowerInvariant();

            // =================================================================
            // SWORDS (one-handed and two-handed)
            // =================================================================

            // Two-handed swords
            if (lowerCode.StartsWith("greatsword-") || lowerCode.StartsWith("zweihander-") ||
                lowerCode.StartsWith("claymore-") || lowerCode.StartsWith("flamberge-") ||
                lowerCode.StartsWith("montante-") || lowerCode.StartsWith("nodachi-") ||
                lowerCode.StartsWith("2hsword-") || lowerCode.StartsWith("2h-sword-") ||
                lowerCode.StartsWith("twohandedsword-") || lowerCode.StartsWith("twohanded-sword-") ||
                lowerCode.StartsWith("sword-great-") || lowerCode.StartsWith("sword-long-") ||
                lowerCode.StartsWith("longsword-") ||
                (lowerCode.Contains("twohanded") && lowerCode.Contains("sword")) ||
                (lowerCode.Contains("2h") && lowerCode.Contains("sword")))
            {
                return itemCode;
            }

            // One-handed swords, blades, and similar
            if (lowerCode.StartsWith("sword-") || lowerCode.StartsWith("blade-") ||
                lowerCode.StartsWith("shortsword-") || lowerCode.StartsWith("sword-short-") ||
                lowerCode.StartsWith("sword-arming-") ||
                lowerCode.StartsWith("saber-") || lowerCode.StartsWith("sabre-") ||
                lowerCode.StartsWith("rapier-") || lowerCode.StartsWith("scimitar-") ||
                lowerCode.StartsWith("cutlass-") || lowerCode.StartsWith("falx-") ||
                lowerCode.StartsWith("falchion-") || lowerCode.StartsWith("kopis-") ||
                lowerCode.StartsWith("gladius-") || lowerCode.StartsWith("messer-"))
            {
                return itemCode;
            }

            // Daggers and knives (combat)
            if (lowerCode.StartsWith("dagger-") || lowerCode.StartsWith("knife-") ||
                lowerCode.StartsWith("stiletto-") || lowerCode.StartsWith("khanjar-") ||
                lowerCode.StartsWith("baselard-") || lowerCode.StartsWith("dirk-") ||
                lowerCode.StartsWith("tanto-") || lowerCode.StartsWith("kukri-"))
            {
                return itemCode;
            }

            // =================================================================
            // POLEARMS
            // =================================================================

            // Spears (melee)
            if (lowerCode.StartsWith("spear-") || lowerCode.StartsWith("pike-") ||
                lowerCode.StartsWith("lance-") || lowerCode.StartsWith("trident-") ||
                lowerCode.StartsWith("pilum-") || lowerCode.StartsWith("sarissa-"))
            {
                return itemCode;
            }

            // Javelins (can be used in melee too)
            if (lowerCode.StartsWith("javelin-") || lowerCode.StartsWith("throwing-spear-") ||
                lowerCode.StartsWith("thrown-spear-") || lowerCode.StartsWith("dart-") ||
                lowerCode.StartsWith("plumbata-") ||
                lowerCode.Contains("javelin") || lowerCode.Contains("throwingspear") ||
                lowerCode.Contains("thrownspear"))
            {
                return itemCode;
            }

            // Halberds and poleaxes
            if (lowerCode.StartsWith("halberd-") || lowerCode.StartsWith("poleaxe-") ||
                lowerCode.StartsWith("glaive-") || lowerCode.StartsWith("bardiche-") ||
                lowerCode.StartsWith("voulge-") || lowerCode.StartsWith("guisarme-") ||
                lowerCode.StartsWith("billhook-") || lowerCode.StartsWith("partisan-") ||
                lowerCode.StartsWith("naginata-") || lowerCode.StartsWith("sovnya-"))
            {
                return itemCode;
            }

            // Quarterstaves
            if (lowerCode.StartsWith("quarterstaff-") || lowerCode.StartsWith("staff-") ||
                lowerCode.StartsWith("bo-") || lowerCode.Contains("bo-staff"))
            {
                return itemCode;
            }

            // =================================================================
            // BLUNT WEAPONS
            // =================================================================

            // Maces and warhammers
            if (lowerCode.StartsWith("mace-") || lowerCode.StartsWith("morningstar-") ||
                lowerCode.StartsWith("flail-") || lowerCode.StartsWith("warhammer-") ||
                lowerCode.StartsWith("maul-") || lowerCode.StartsWith("hammer-") ||
                lowerCode.StartsWith("flangedmace-") || lowerCode.StartsWith("spikedmace-"))
            {
                return itemCode;
            }

            // Clubs
            if (lowerCode.StartsWith("club-") || lowerCode.StartsWith("cudgel-") ||
                lowerCode.StartsWith("baton-") || lowerCode.StartsWith("truncheon-") ||
                lowerCode.StartsWith("shillelagh-") || lowerCode.StartsWith("blackjack-"))
            {
                return itemCode;
            }

            // =================================================================
            // AXES (combat, not tools)
            // =================================================================
            if (lowerCode.StartsWith("battleaxe-") || lowerCode.StartsWith("waraxe-") ||
                lowerCode.StartsWith("handaxe-") || lowerCode.StartsWith("hatchet-") ||
                lowerCode.StartsWith("tomahawk-") || lowerCode.StartsWith("francisca-") ||
                lowerCode.StartsWith("dane-axe-") || lowerCode.StartsWith("daneaxe-") ||
                lowerCode.StartsWith("broadaxe-") || lowerCode.StartsWith("labrys-") ||
                (lowerCode.StartsWith("axe-") && !lowerCode.Contains("pickaxe")))
            {
                return itemCode;
            }

            // =================================================================
            // ANCIENT ARMORY MOD WEAPONS
            // =================================================================

            // aa-blade: gladius, arming, claymore, sabre, longsword, falchion (swords)
            if (lowerCode.StartsWith("aa-blade-"))
            {
                return itemCode;
            }

            // aa-axe: bearded, battle, bardiche (battle axes)
            if (lowerCode.StartsWith("aa-axe-"))
            {
                return itemCode;
            }

            // aa-club: flanged, morningstar, spiked, warhammer (maces)
            if (lowerCode.StartsWith("aa-club-"))
            {
                return itemCode;
            }

            // aa-knife: dagger, stiletto, khanjar, baselard (combat knives)
            if (lowerCode.StartsWith("aa-knife-"))
            {
                return itemCode;
            }

            // aa-spear: boar, voulge, fork, ranseur (polearms)
            if (lowerCode.StartsWith("aa-spear-"))
            {
                return itemCode;
            }

            return null;
        }

        /// <summary>
        /// Process ranged damage dealt by a player. Called from Harmony patch.
        /// </summary>
        public static void ProcessRangedDamage(IServerPlayer attackerPlayer, string weaponCombo, float damage)
        {
            if (attackerPlayer?.Entity == null || string.IsNullOrEmpty(weaponCombo)) return;

            string playerUid = attackerPlayer.PlayerUID;

            // Track unlock-trait progression FIRST, independent of the ranged credit cap.
            // Bowyer and Improviser are separate unlocks that should still progress for players
            // who have already maxed their ranged credits.
            if (IsSimpleBowOrLongbow(weaponCombo))
            {
                TrackBowyerBowDamage(attackerPlayer, damage);
            }
            if (IsThrownRock(weaponCombo))
            {
                TrackImproviserRockDamage(attackerPlayer, damage);
            }

            var damageProgress = AttributeModifierDefinitions.RangedDamage.GetForPlayer(playerUid);
            damageProgress.DoEvent(attackerPlayer, weaponCombo, damage);
            var accuracyProgress = AttributeModifierDefinitions.RangedAccuracy.GetForPlayer(playerUid);
            accuracyProgress.DoEvent(attackerPlayer, weaponCombo, damage);
            var distanceProgress = AttributeModifierDefinitions.RangedDistance.GetForPlayer(playerUid);
            distanceProgress.DoEvent(attackerPlayer, weaponCombo, damage);
        }

        /// <summary>
        /// Check if the weapon combo represents a simple bow or longbow.
        /// </summary>
        private static bool IsSimpleBowOrLongbow(string weaponCombo)
        {
            if (string.IsNullOrEmpty(weaponCombo)) return false;
            string lower = weaponCombo.ToLowerInvariant();
            return lower.Contains("bow-simple") || lower.Contains("bow-long") ||
                   lower.StartsWith("simple") || lower.StartsWith("long");
        }

        /// <summary>
        /// Check if the weapon combo represents a thrown rock.
        /// </summary>
        private static bool IsThrownRock(string weaponCombo)
        {
            if (string.IsNullOrEmpty(weaponCombo)) return false;
            string lower = weaponCombo.ToLowerInvariant();
            return lower.Contains("stone-") || lower.Contains("sling+stone") ||
                   lower.StartsWith("stone") || lower.Contains("thrownstone") ||
                   (lower.Contains("stone") && !lower.Contains("whetstone"));
        }

        /// <summary>
        /// Track bow damage for Bowyer unlock.
        /// </summary>
        private static void TrackBowyerBowDamage(IServerPlayer player, float damage)
        {
            if (player?.Entity == null || damage <= 0) return;

            // Apply sleep buff multiplier if active
            AttributeModifierDefinitions.Bowyer.AddCredits(player, ApplyXPMultiplier(player.PlayerUID, damage));
        }

        /// <summary>
        /// Track thrown rock damage for Improviser unlock.
        /// </summary>
        private static void TrackImproviserRockDamage(IServerPlayer player, float damage)
        {
            if (player?.Entity == null || damage <= 0) return;

            // Apply sleep buff multiplier if active
            AttributeModifierDefinitions.Improviser.AddCredits(player, ApplyXPMultiplier(player.PlayerUID, damage));
        }

        /// <summary>
        /// Gets a weapon combination code from a projectile and the shooter's held weapon.
        /// For bows+arrows, returns "bowCode+arrowCode" (e.g., "bow-long+arrow-copper").
        /// For slings+stones, returns "sling+stone".
        /// Returns null if not a qualifying ranged weapon.
        /// </summary>
        public static string GetRangedWeaponCombo(Entity projectile, EntityPlayer shooter)
        {
            if (projectile == null || shooter == null) return null;

            string projectileCode = projectile.Code?.ToString() ?? "";
            string heldItemCode = shooter.RightHandItemSlot?.Itemstack?.Collectible?.Code?.ToString() ?? "";

            // Remove any mod prefix (e.g., "game:", "combatoverhaul:") for checking
            string projCheck = projectileCode.Contains(":") ? projectileCode.Substring(projectileCode.IndexOf(':') + 1) : projectileCode;
            string heldCheck = heldItemCode.Contains(":") ? heldItemCode.Substring(heldItemCode.IndexOf(':') + 1) : heldItemCode;

            // Check for arrow projectiles (bows)
            if (projCheck.StartsWith("arrow-") || projCheck == "arrow" || projCheck.Contains("arrow"))
            {
                // Get bow type from held item (if still holding a bow)
                string bowCode = "unknown-bow";
                if (heldCheck.StartsWith("bow-") || heldCheck == "bow" ||
                    heldCheck.StartsWith("longbow") || heldCheck.StartsWith("recurvebow") ||
                    heldCheck.StartsWith("crudebow") || heldCheck.StartsWith("simplebow") ||
                    heldCheck.Contains("bow"))
                {
                    bowCode = heldCheck;
                }
                return $"{bowCode}+{projCheck}";
            }

            // Check for crossbow bolts/quarrels
            if (projCheck.StartsWith("bolt-") || projCheck == "bolt" || projCheck.Contains("bolt") ||
                projCheck.StartsWith("quarrel-") || projCheck == "quarrel" || projCheck.Contains("quarrel"))
            {
                // Get crossbow type from held item
                string crossbowCode = "unknown-crossbow";
                if (heldCheck.StartsWith("crossbow") || heldCheck.Contains("crossbow"))
                {
                    crossbowCode = heldCheck;
                }
                return $"{crossbowCode}+{projCheck}";
            }

            // Check for firearm projectiles (bullets, musket balls, etc.)
            if (projCheck.StartsWith("bullet-") || projCheck == "bullet" || projCheck.Contains("bullet") ||
                projCheck.StartsWith("musketball") || projCheck.Contains("musketball") ||
                projCheck.StartsWith("shot-") || projCheck.Contains("shot"))
            {
                // Get firearm type from held item
                string firearmCode = "unknown-firearm";
                if (heldCheck.StartsWith("musket") || heldCheck.StartsWith("pistol") ||
                    heldCheck.StartsWith("rifle") || heldCheck.StartsWith("blunderbuss") ||
                    heldCheck.Contains("gun") || heldCheck.Contains("firearm"))
                {
                    firearmCode = heldCheck;
                }
                return $"{firearmCode}+{projCheck}";
            }

            // Check for sling stones (slung from sling — entity is thrownstone-{rock})
            if (projCheck.StartsWith("stone-") || projCheck == "stone" || projCheck.StartsWith("thrownstone") ||
                projCheck.Contains("slingstone") || projCheck.Contains("sling-stone"))
            {
                // Check if holding a sling
                string slingCode = "thrown";
                if (heldCheck.StartsWith("sling") || heldCheck.Contains("sling"))
                {
                    slingCode = heldCheck;
                }
                return $"{slingCode}+{projCheck}";
            }

            // Generic thrown items (CollectibleBehaviorThrowable). VS 1.22 spawns hand-thrown
            // stones, bones, etc. as a single shared `game:thrownitem` projectile entity — the
            // actual item is in ProjectileStack. Peek there to figure out what was thrown so
            // Improviser etc. can recognize it as a stone.
            if (projCheck == "thrownitem" || projCheck.StartsWith("thrownitem-") || projCheck.StartsWith("thrownitem+"))
            {
                string stackCode = "";
                if (projectile is IProjectile proj && proj.ProjectileStack?.Collectible?.Code != null)
                {
                    stackCode = proj.ProjectileStack.Collectible.Code.ToString();
                }
                string stackCheck = !string.IsNullOrEmpty(stackCode) && stackCode.Contains(":")
                    ? stackCode.Substring(stackCode.IndexOf(':') + 1)
                    : stackCode;
                if (!string.IsNullOrEmpty(stackCheck))
                {
                    return $"thrown+{stackCheck}";
                }
                return $"thrown+{projCheck}";
            }

            // Check for spear/javelin throws (thrown spears deal ranged damage)
            if (projCheck.StartsWith("spear-") || projCheck.StartsWith("thrownspear") ||
                projCheck.StartsWith("javelin-") || projCheck.Contains("javelin") ||
                projCheck.StartsWith("pilum-") || projCheck.Contains("throwingspear"))
            {
                return $"thrown+{projCheck}";
            }

            // Check for Atlatl darts (Return of the Atlatl mod)
            // Projectile entities are "atlatl:apdart-{material}", projCheck will be "apdart-{material}"
            if (projCheck.StartsWith("apdart") || projectileCode.Contains("atlatl:apdart"))
            {
                string launcherCode = "unknown-atlatl";
                if (heldCheck.StartsWith("aplauncher") || heldCheck.Contains("aplauncher") ||
                    heldItemCode.Contains("atlatl:aplauncher"))
                {
                    launcherCode = heldCheck;
                }
                return $"{launcherCode}+{projCheck}";
            }

            return null;
        }

        /// <summary>
        /// Checks if a damage source is from a ranged attack (projectile).
        /// </summary>
        public static bool IsRangedDamage(DamageSource damageSource)
        {
            // Debug logging at start to diagnose CO issues (disabled by default)
            if (DebugLoggingEnabled)
            {
                ServerApi?.Logger.Debug($"[SeraphLeveling] IsRangedDamage called: SourceEntity={damageSource?.SourceEntity?.Code}, CauseEntity={damageSource?.CauseEntity?.Code}, Type={damageSource?.Type}, Same={damageSource?.SourceEntity == damageSource?.CauseEntity}");
            }

            // CauseEntity is non-null for projectile damage (it's the shooter)
            // SourceEntity is the projectile itself
            if (damageSource?.CauseEntity == null) return false;

            // For melee attacks, SourceEntity equals CauseEntity (both are the attacker).
            // For ranged attacks, SourceEntity is the projectile, CauseEntity is the shooter.
            // Combat Overhaul may set CauseEntity for melee attacks, so we check if they're
            // the same entity to distinguish melee from ranged.
            if (damageSource.SourceEntity == damageSource.CauseEntity) return false;

            // Additional check: the damage should be from a projectile type
            // PiercingAttack is typically used for arrows in vanilla
            // SlashingAttack is used by Combat Overhaul for arrows
            // BluntAttack is used for thrown stones
            return damageSource.Type == EnumDamageType.PiercingAttack ||
                   damageSource.Type == EnumDamageType.SlashingAttack ||
                   damageSource.Type == EnumDamageType.BluntAttack;
        }

        public override void Dispose()
        {
            // Persist any pending progress before shutdown
            if (ServerApi != null)
            {
                foreach (var def in LoadedAttributes)
                {
                    if (def.HasUnsavedProgress())
                    {
                        def.PersistProgress(ServerApi);
                    }
                }

                // These two are newer than the rest and were never added to the shutdown
                // flush. OnGameWorldSave persists them, so they only went missing when
                // Dispose ran without a world save first.
                if (pendingCOProgressSave || !COProgress.IsEmpty)
                {
                    PersistCOProgress();
                }

                if (pendingSleepBuffSave || !SleepBuffExpiration.IsEmpty)
                {
                    PersistSleepBuffData();
                }

                ServerApi.Event.DidBreakBlock -= OnBlockBroken;
                ServerApi.Event.PlayerJoin -= OnPlayerJoin;
                ServerApi.Event.PlayerDisconnect -= OnPlayerDisconnect;
                ServerApi.Event.GameWorldSave -= OnGameWorldSave;
                ServerApi.Event.SaveGameLoaded -= LoadConfig;
                ServerApi.Event.SaveGameLoaded -= LoadAllProgress;
                ServerApi.Event.SaveGameLoaded -= LoadSleepBuffData;
            }

            // Mark as disposed BEFORE clearing dictionaries to prevent OnGameWorldSave
            // from persisting empty data if it fires during shutdown after Clear()
            isDisposed = true;

            // Unpatch server-side Harmony patches
            serverHarmony?.UnpatchAll("seraphleveling.server");

            foreach (var def in LoadedAttributes)
            {
                def.ResetProgress();
                def.PendingSave = false;
            }

            lastPlayerPositions.Clear();
            lastSneakingPositions.Clear();
            VanillaTraitsCache.Clear();
            LastDecayCheckDay.Clear();
            SleepBuffExpiration.Clear();
            SleepBuffMultiplier.Clear();
            LastSleepBuffApplyTick.Clear();
            pendingSleepBuffSave = false;
            base.Dispose();
        }

        /// <summary>
        /// Called when the world is saved. Persist all progress and config to world save data.
        /// </summary>
        private void OnGameWorldSave()
        {
            // Guard against persisting empty data after Dispose() has cleared dictionaries
            if (isDisposed) return;
            PersistModList();

            foreach (var def in LoadedAttributes)
            {
                if (def.HasUnsavedProgress())
                {
                    def.PersistProgress(ServerApi);
                    def.PendingSave = false;
                }
            }

            // Combat Overhaul compatibility persistence
            if (pendingCOProgressSave || !COProgress.IsEmpty)
            {
                PersistCOProgress();
                pendingCOProgressSave = false;
            }

            if (pendingSleepBuffSave || !SleepBuffExpiration.IsEmpty)
            {
                PersistSleepBuffData();
                pendingSleepBuffSave = false;
            }

            if (pendingConfigSave)
            {
                PersistConfig();
                pendingConfigSave = false;
            }
        }

        public static void PersistProgress<T>() where T : ProgressData<T>, IProgressDataContract<T>
        {
            if (ServerApi == null) return;
            var progress = T.ProgressDictionary();

            lock (persistLock)
            {
                if (progress.IsEmpty)
                {
                    return;
                }

                try
                {
                    var snapshot = progress.ToArray();

                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            ProgressData<T>.WriteHeader(writer);

                            // Write number of players
                            writer.Write(snapshot.Length);

                            foreach (var playerKvp in snapshot)
                            {
                                writer.Write(playerKvp.Key);   // Player UID
                                var p = playerKvp.Value;
                                p.WriteOut(writer);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(T.SAVE_KEY, data);
                    var description = T.Description;
                    ServerApi.Logger.Debug($"[SeraphLeveling] Persisted {description} progress for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    var description = T.Description;
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist {description} progress: {ex.Message}");
                }
            }
        }

        public static void LogSpammyData(byte[] data, string description, string location)
        {
            var stringyData = string.Concat(data.Select(b => b >= 32 && b <= 122 ? ((char)b).ToString() : $"[0x{b:X2}]"));
            ServerApi.Logger.Debug($"[SeraphLeveling] {description} data found: {stringyData} in {location}");

        }

        const string MOD_LIST_SAVE_KEY = "sitModListData";
        const string MOD_LIST_HEADER = "SML";
        private static bool savedModList = false;
        private void LoadModList()
        {
            byte[] data = ServerApi.WorldManager.SaveGame.GetData(MOD_LIST_SAVE_KEY);
            if (data == null || data.Length == 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] No mod list data found in world save; either a fresh world or a port of the old Seraph Leveling.");
                var legacyRangedDamage = new DamageAttributeModifierDefinition
                {
                    Id = AttributeModifierDefinitions.RangedDamage.Id,
                    Name = "Ranged",
                    PersistenceHeader = "SIR",
                    SkillKey = "rangedlegacy",
                    GlobalMaxCredits = AttributeModifierDefinitions.RangedDamage.GlobalMaxCredits,
                    Stat = AttributeModifierDefinitions.RangedDamage.Stat,
                    IncrementData = AttributeModifierDefinitions.RangedDamage.IncrementData,
                    StatName = AttributeModifierDefinitions.RangedDamage.StatName,
                    Tool = AttributeModifierDefinitions.RangedDamage.Tool
                };
                Conversion.PortData<DamageAttributeModifierDefinition, DamageAttributeModifierProgressData>(legacyRangedDamage, AttributeModifierDefinitions.RangedDamage, ServerApi);
                Conversion.PortData<DamageAttributeModifierDefinition, DamageAttributeModifierProgressData>(legacyRangedDamage, AttributeModifierDefinitions.RangedAccuracy, ServerApi);
                Conversion.PortData<DamageAttributeModifierDefinition, DamageAttributeModifierProgressData>(legacyRangedDamage, AttributeModifierDefinitions.RangedDistance, ServerApi);
                var legacyForager = new GenericLeveledAttributeModifierDefinition
                {
                    Id = AttributeModifierDefinitions.ForageLootingBonus.Id,
                    Name = "Forager",
                    PersistenceHeader = "FRG",
                    SkillKey = "foragerlegacy",
                    SaveKey = "sitForagerProgress",
                    GlobalMaxCredits = AttributeModifierDefinitions.ForageLootingBonus.GlobalMaxCredits,
                    Stat = AttributeModifierDefinitions.ForageLootingBonus.Stat,
                    StatName = AttributeModifierDefinitions.ForageLootingBonus.StatName,
                    BaseIncrement = AttributeModifierDefinitions.ForageLootingBonus.BaseIncrement,
                    IncrementStep = AttributeModifierDefinitions.ForageLootingBonus.IncrementStep,
                    IncrementUnits = AttributeModifierDefinitions.ForageLootingBonus.IncrementUnits,
                };
                Conversion.PortData<LeveledPartialAttributeModifierDefinition, LeveledPartialAttributeModifierProgressData>(legacyForager, AttributeModifierDefinitions.ForageLootingBonus, ServerApi);
                Conversion.PortData<LeveledPartialAttributeModifierDefinition, LeveledPartialAttributeModifierProgressData>(legacyForager, AttributeModifierDefinitions.WildCropDropRate, ServerApi);
                var legacyResourceful = new GenericLeveledAttributeModifierDefinition
                {
                    Id = AttributeModifierDefinitions.AnimalDropRate.Id,
                    Name = "Resourceful",
                    PersistenceHeader = "RSF",
                    SkillKey = "resourcefullegacy",
                    SaveKey = "sitResourcefulProgress",
                    GlobalMaxCredits = AttributeModifierDefinitions.AnimalDropRate.GlobalMaxCredits,
                    Stat = AttributeModifierDefinitions.AnimalDropRate.Stat,
                    StatName = AttributeModifierDefinitions.AnimalDropRate.StatName,
                    BaseIncrement = AttributeModifierDefinitions.AnimalDropRate.BaseIncrement,
                    IncrementStep = AttributeModifierDefinitions.AnimalDropRate.IncrementStep,
                    IncrementUnits = AttributeModifierDefinitions.AnimalDropRate.IncrementUnits,
                };
                Conversion.PortData<LeveledPartialAttributeModifierDefinition, LeveledPartialAttributeModifierProgressData>(legacyResourceful, AttributeModifierDefinitions.AnimalHarvestRate, ServerApi);
                LoadProgress<PilfererProgressData>();
                if (!PilfererProgressData.progressDict.IsEmpty)
                {
                    var snapshot = PilfererProgressData.progressDict.ToArray();
                    ServerApi.Logger.Debug($"[SeraphLeveling] Porting legacy pilferer data for {snapshot.Length} players.");
                    AttributeModifierDefinitions.GearDropRate.LoadProgress(ServerApi);
                    if (AttributeModifierDefinitions.GearDropRate.ProgressDictionary.IsEmpty)
                    {
                        foreach (var kvp in snapshot)
                        {
                            var pd = AttributeModifierDefinitions.GearDropRate.CreateProgressData();
                            pd.TotalCredits = kvp.Value.TotalCredits;
                            pd.PartialCredit = kvp.Value.PointsInIncrement;
                            pd.CurrentIncrementSize = kvp.Value.CurrentIncrementSize;
                            pd.LastActivityDay = kvp.Value.LastActivityDay;
                            AttributeModifierDefinitions.GearDropRate.ProgressDictionary.TryAdd(kvp.Key, pd);
                            AttributeModifierDefinitions.GearDropRate.PersistProgress(ServerApi);
                        }
                    }
                    if (AttributeModifierDefinitions.VesselDropRate.ProgressDictionary.IsEmpty)
                    {
                        foreach (var kvp in snapshot)
                        {
                            var pd = AttributeModifierDefinitions.VesselDropRate.CreateProgressData();
                            pd.TotalCredits = kvp.Value.TotalCredits;
                            pd.PartialCredit = kvp.Value.PointsInIncrement;
                            pd.CurrentIncrementSize = kvp.Value.CurrentIncrementSize;
                            pd.LastActivityDay = kvp.Value.LastActivityDay;
                            AttributeModifierDefinitions.VesselDropRate.ProgressDictionary.TryAdd(kvp.Key, pd);
                            AttributeModifierDefinitions.VesselDropRate.PersistProgress(ServerApi);
                        }
                    }
                    if (AttributeModifierDefinitions.WholeVesselRate.ProgressDictionary.IsEmpty)
                    {
                        foreach (var kvp in snapshot)
                        {
                            var pd = AttributeModifierDefinitions.WholeVesselRate.CreateProgressData();
                            pd.TotalCredits = kvp.Value.TotalCredits;
                            pd.PartialCredit = kvp.Value.PointsInIncrement;
                            pd.CurrentIncrementSize = kvp.Value.CurrentIncrementSize;
                            pd.LastActivityDay = kvp.Value.LastActivityDay;
                            AttributeModifierDefinitions.WholeVesselRate.ProgressDictionary.TryAdd(kvp.Key, pd);
                            AttributeModifierDefinitions.WholeVesselRate.PersistProgress(ServerApi);
                        }
                    }
                }

                LoadProgress<ArmorProgressData>();
                if (!ArmorProgressData.progressDict.IsEmpty)
                {
                    var snapshot = ArmorProgressData.progressDict.ToArray();
                    ServerApi.Logger.Debug($"[SeraphLeveling] Porting legacy armor data for {snapshot.Length} players.");
                    AttributeModifierDefinitions.ArmorDurability.LoadProgress(ServerApi);
                    if (AttributeModifierDefinitions.ArmorDurability.ProgressDictionary.IsEmpty)
                    {
                        foreach (var kvp in snapshot)
                        {
                            var pd = AttributeModifierDefinitions.ArmorDurability.CreateProgressData();
                            pd.TotalCredits = kvp.Value.TotalDurabilityCredits;
                            var armorSnapshot = kvp.Value.ArmorProgress.ToArray();
                            foreach (var ikvp in armorSnapshot)
                            {
                                var tp = pd.GetToolProgress(kvp.Key);
                                tp.HasBeenUsed = ikvp.Value.HasBeenEquipped;
                                tp.PartialCredit[ArmorDurabilityProgressTypes.DamageBlocked] = new()
                                {
                                    Amount = ikvp.Value.DamageBlockedInIncrement,
                                    IncrementSize = ikvp.Value.CurrentDamageIncrementSize,
                                };
                                tp.PartialCredit[ArmorDurabilityProgressTypes.RepairProgress] = new()
                                {
                                    Amount = ikvp.Value.RepairsInIncrement,
                                    IncrementSize = ikvp.Value.CurrentRepairIncrementSize,
                                };
                            }
                            pd.LastActivityDay = kvp.Value.LastActivityDay;
                            AttributeModifierDefinitions.ArmorDurability.ProgressDictionary.TryAdd(kvp.Key, pd);
                            AttributeModifierDefinitions.ArmorDurability.PersistProgress(ServerApi);
                        }
                    }
                    AttributeModifierDefinitions.ArmorWalkSpeed.LoadProgress(ServerApi);
                    if (AttributeModifierDefinitions.ArmorWalkSpeed.ProgressDictionary.IsEmpty)
                    {
                        foreach (var kvp in snapshot)
                        {
                            var pd = AttributeModifierDefinitions.ArmorWalkSpeed.CreateProgressData();
                            pd.TotalCredits = kvp.Value.TotalDurabilityCredits;
                            var armorSnapshot = kvp.Value.ArmorProgress.ToArray();
                            foreach (var ikvp in armorSnapshot)
                            {
                                var tp = pd.GetToolProgress(kvp.Key);
                                tp.HasBeenUsed = ikvp.Value.HasBeenEquipped;
                                tp.PartialCredit[default] = new()
                                {
                                    Amount = ikvp.Value.SecondsWornInIncrement,
                                    IncrementSize = ikvp.Value.CurrentTimeIncrementSize,
                                };
                            }
                            pd.LastActivityDay = kvp.Value.LastActivityDay;
                            AttributeModifierDefinitions.ArmorWalkSpeed.ProgressDictionary.TryAdd(kvp.Key, pd);
                            AttributeModifierDefinitions.ArmorWalkSpeed.PersistProgress(ServerApi);
                        }
                    }
                    AttributeModifierDefinitions.ArmorHealing.LoadProgress(ServerApi);
                    if (AttributeModifierDefinitions.ArmorHealing.ProgressDictionary.IsEmpty)
                    {
                        foreach (var kvp in snapshot)
                        {
                            var pd = AttributeModifierDefinitions.ArmorHealing.CreateProgressData();
                            pd.TotalCredits = kvp.Value.TotalDurabilityCredits;
                            var armorSnapshot = kvp.Value.ArmorProgress.ToArray();
                            foreach (var ikvp in armorSnapshot)
                            {
                                var tp = pd.GetToolProgress(kvp.Key);
                                tp.HasBeenUsed = ikvp.Value.HasBeenEquipped;
                                tp.PartialCredit[default] = new()
                                {
                                    Amount = ikvp.Value.SecondsWornInIncrement,
                                    IncrementSize = ikvp.Value.CurrentTimeIncrementSize,
                                };
                            }
                            pd.LastActivityDay = kvp.Value.LastActivityDay;
                            AttributeModifierDefinitions.ArmorHealing.ProgressDictionary.TryAdd(kvp.Key, pd);
                            AttributeModifierDefinitions.ArmorHealing.PersistProgress(ServerApi);
                        }
                    }
                    AttributeModifierDefinitions.ArmorHungerRate.LoadProgress(ServerApi);
                    if (AttributeModifierDefinitions.ArmorHungerRate.ProgressDictionary.IsEmpty)
                    {
                        foreach (var kvp in snapshot)
                        {
                            var pd = AttributeModifierDefinitions.ArmorHungerRate.CreateProgressData();
                            pd.TotalCredits = kvp.Value.TotalDurabilityCredits;
                            var armorSnapshot = kvp.Value.ArmorProgress.ToArray();
                            foreach (var ikvp in armorSnapshot)
                            {
                                var tp = pd.GetToolProgress(kvp.Key);
                                tp.HasBeenUsed = ikvp.Value.HasBeenEquipped;
                                tp.PartialCredit[default] = new()
                                {
                                    Amount = ikvp.Value.SecondsWornInIncrement,
                                    IncrementSize = ikvp.Value.CurrentTimeIncrementSize,
                                };
                            }
                            pd.LastActivityDay = kvp.Value.LastActivityDay;
                            AttributeModifierDefinitions.ArmorHungerRate.ProgressDictionary.TryAdd(kvp.Key, pd);
                            AttributeModifierDefinitions.ArmorHungerRate.PersistProgress(ServerApi);
                        }
                    }
                }

                PersistModList();
            }
            else
            {
                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        var bytes = Encoding.ASCII.GetBytes(MOD_LIST_HEADER);
                        bool hasProblem = false;
                        foreach (var b in bytes)
                        {
                            byte bin = reader.ReadByte();
                            hasProblem |= (bin != b);
                        }
                        if (hasProblem)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Invalid mod list data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        if (version != 0x1)
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown mod list data version");
                        }
                        // TODO: actually care about the contents, lol.
                    }
                }
            }
        }

        private void PersistModList()
        {
            if (savedModList) return;
            var snapshot = LoadedMods.ToArray();
            byte[] data;
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    var bytes = Encoding.ASCII.GetBytes(MOD_LIST_HEADER);
                    foreach (var b in bytes)
                    {
                        writer.Write(b);
                    }
                    writer.Write((byte)0x1); // version.
                    writer.Write(snapshot.Length);
                    foreach (var mod in snapshot)
                    {
                        writer.Write(mod.ModId);
                    }
                }
                data = ms.ToArray();
            }

            ServerApi.WorldManager.SaveGame.StoreData(MOD_LIST_SAVE_KEY, data);
            savedModList = true;
            ServerApi.Logger.Debug($"[SeraphLeveling] Persisted mod list.");
        }

        private void LoadAllProgress()
        {
            LoadModList();
            foreach (var definition in LoadedAttributes)
            {
                definition.LoadProgress(ServerApi);
            }
        }
        private void LoadProgress<T>() where T : ProgressData<T>, IProgressDataContract<T>
        {
            var progress = T.ProgressDictionary();
            if (ServerApi == null) return;

            progress.Clear();
            var description = T.Description;

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(T.SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug($"[SeraphLeveling] No {description} progress data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        if (!ProgressData<T>.ReadHeader(reader))
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Invalid {description} progress data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        int playerCount = reader.ReadInt32();
                        for (int i = 0; i < playerCount; i++)
                        {
                            try
                            {
                                string playerUid = reader.ReadString();
                                progress[playerUid] = T.ReadVersion(version, reader);
                            }
                            catch (Exception innerEx)
                            {
                                ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i + 1}/{playerCount} in {description} data: {innerEx.Message}");
                                break;
                            }
                        }
                        if (version != T.GetVersion())
                        {
                            T.MarkForSave();
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Loaded {description} progress for {progress.Count} players");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load {description} progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Load configuration from ModConfig/SeraphLeveling.json.
        /// If the file doesn't exist, creates one with default values.
        /// These values are used as defaults for new worlds.
        /// </summary>
        private static Dictionary<string, Dictionary<string, int>> AttributeConfiguration = [];
        private void LoadConfigFile(ICoreServerAPI api)
        {
            try
            {
                SeraphLevelingConfig config = api.LoadModConfig<SeraphLevelingConfig>(CONFIG_FILE_NAME);
                if (config == null)
                {
                    // A brand new install has no old world settings to fold in, so
                    // stamp it as already migrated.
                    InitializeClothierBlacklistedItems(api);
                    config = new SeraphLevelingConfig
                    {
                        ConfigVersion = CURRENT_CONFIG_VERSION,
                        ClothierBlacklistedItems = [.. AttributeModifierDefinitions.Clothier.TokenBanList]
                    };
                    api.StoreModConfig(config, CONFIG_FILE_NAME);
                    api.Logger.Notification("[SeraphLeveling] Created default config file: ModConfig/" + CONFIG_FILE_NAME);
                    pendingConfigSave = true; // Let's actually save the full thing, shall we?
                }

                LoadedConfigVersion = config.ConfigVersion;

                foreach (var definition in LoadedAttributes)
                {
                    if (config.AttributeConfiguration.TryGetValue(definition.Id, out var dataDict))
                    {
                        definition.ReadConfigData(dataDict);
                    }
                }
                AttributeConfiguration = config.AttributeConfiguration;

                // Apply config values to static variables
                OreMultiplier = config.MiningOreMultiplier;

                // First-equip bonus configuration
                FirstEquipLightBonus = config.ArmorFirstEquipLightDurability;
                FirstEquipChainBonus = config.ArmorFirstEquipChainDurability;
                FirstEquipBrigandineBonus = config.ArmorFirstEquipBrigandineDurability;
                FirstEquipScaleBonus = config.ArmorFirstEquipScaleDurability;
                FirstEquipPlateBonus = config.ArmorFirstEquipPlateDurability;

                FirstEquipWalkSpeedLightBonus = config.ArmorFirstEquipLightWalkSpeed;
                FirstEquipWalkSpeedChainBonus = config.ArmorFirstEquipChainWalkSpeed;
                FirstEquipWalkSpeedBrigandineBonus = config.ArmorFirstEquipBrigandineWalkSpeed;
                FirstEquipWalkSpeedScaleBonus = config.ArmorFirstEquipScaleWalkSpeed;
                FirstEquipWalkSpeedPlateBonus = config.ArmorFirstEquipPlateWalkSpeed;

                // Optional armor features
                EnableArmorHungerReduction = config.EnableArmorHungerReduction;
                EnableArmorHealingBonus = config.EnableArmorHealingBonus;

                AttributeModifierDefinitions.Clothier.RequiredCollectionSize = config.ClothierRequiredUniqueClothes;
                if (config.ClothierBlacklistedItems != null)
                {
                    AttributeModifierDefinitions.Clothier.TokenBanList = [.. config.ClothierBlacklistedItems];
                }

                // Auto-save configuration
                AutoSaveIntervalSeconds = config.AutoSaveIntervalSeconds;

                // Load disabled skills into HashSet for O(1) lookups
                DisabledSkills.Clear();
                if (config.DisabledSkills != null && config.DisabledSkills.Length > 0)
                {
                    foreach (var skill in config.DisabledSkills)
                    {
                        if (!string.IsNullOrWhiteSpace(skill))
                        {
                            DisabledSkills.Add(skill.Trim().ToLowerInvariant());
                        }
                    }
                    if (DisabledSkills.Count > 0)
                    {
                        api.Logger.Notification($"[SeraphLeveling] Disabled skills: {string.Join(", ", DisabledSkills)}");
                    }
                }

                // Combat Overhaul compatibility configuration
                COEnableCompat = config.EnableCombatOverhaulCompat;
                COBaseDamagePerIncrement = config.COProficiencyBaseDamagePerIncrement;
                COIncrementStep = config.COProficiencyIncrementStep;
                COProficiencyBaseOverrides = config.COProficiencyBaseOverrides ?? new Dictionary<string, int>();
                COProficiencyIncrementOverrides = config.COProficiencyIncrementOverrides ?? new Dictionary<string, int>();
                COBowsProficiencyMax = config.COBowsProficiencyMax;
                COCrossbowsProficiencyMax = config.COCrossbowsProficiencyMax;
                COFirearmsProficiencyMax = config.COFirearmsProficiencyMax;
                COSlingsProficiencyMax = config.COSlingsProficiencyMax;
                COOneHandedSwordsProficiencyMax = config.COOneHandedSwordsProficiencyMax;
                COTwoHandedSwordsProficiencyMax = config.COTwoHandedSwordsProficiencyMax;
                COSpearsProficiencyMax = config.COSpearsProficiencyMax;
                COJavelinsProficiencyMax = config.COJavelinsProficiencyMax;
                COMacesProficiencyMax = config.COMacesProficiencyMax;
                COClubsProficiencyMax = config.COClubsProficiencyMax;
                COHalberdsProficiencyMax = config.COHalberdsProficiencyMax;
                COPoleaxeProficiencyMax = config.COPoleaxeProficiencyMax;
                COAxesProficiencyMax = config.COAxesProficiencyMax;
                COQuarterstaffProficiencyMax = config.COQuarterstaffProficiencyMax;
                COSteadyAimMax = config.COSteadyAimMax;

                // Skill decay settings
                EnableSkillDecay = config.EnableSkillDecay;
                DecayGracePeriodDays = config.DecayGracePeriodDays;
                DecayBasePointsPerDay = config.DecayBasePointsPerDay;
                DecayMaxPointsPerDay = config.DecayMaxPointsPerDay;
                DecayExemptSkills.Clear();
                if (config.DecayExemptSkills != null)
                {
                    foreach (var skill in config.DecayExemptSkills)
                    {
                        if (!string.IsNullOrWhiteSpace(skill))
                        {
                            DecayExemptSkills.Add(skill.Trim().ToLowerInvariant());
                        }
                    }
                }

                // Load per-skill decay override dictionaries
                DecayGracePeriodOverrides = config.DecayGracePeriodOverrides ?? new Dictionary<string, double>();
                DecayBasePointsOverrides = config.DecayBasePointsOverrides ?? new Dictionary<string, int>();
                DecayMaxPointsOverrides = config.DecayMaxPointsOverrides ?? new Dictionary<string, int>();

                // Sleep buff settings
                EnableSleepBuff = config.EnableSleepBuff;
                SleepBuffLinenBedMultiplier = config.SleepBuffLinenBedMultiplier;
                SleepBuffHayBedMultiplier = config.SleepBuffHayBedMultiplier;
                SleepBuffDurationDays = config.SleepBuffDurationDays;

                // Death penalty settings
                EnableDeathPenalty = config.EnableDeathPenalty;
                DeathPenaltyFraction = config.DeathPenaltyFraction;
                DeathPenaltyExemptSkills.Clear();
                if (config.DeathPenaltyExemptSkills != null)
                {
                    foreach (var skill in config.DeathPenaltyExemptSkills)
                    {
                        if (!string.IsNullOrWhiteSpace(skill))
                        {
                            DeathPenaltyExemptSkills.Add(skill.Trim().ToLowerInvariant());
                        }
                    }
                }

                // Notification settings
                EnableLevelUpMessages = config.EnableLevelUpMessages;
                EnableLevelUpSound = config.EnableLevelUpSound;
                LevelUpSoundName = config.LevelUpSoundName;
                LevelUpSoundVolume = Math.Clamp(config.LevelUpSoundVolume, 0f, 1f);

                // Debug settings
                DebugLoggingEnabled = config.EnableDebugLogging;
                VerboseDecayLogging = config.VerboseDecayLogging;
                if (DebugLoggingEnabled)
                {
                    api.Logger.Warning("[SeraphLeveling] Debug logging is ENABLED - this can spam server logs!");
                }

                if (EnableSkillDecay)
                {
                    api.Logger.Notification($"[SeraphLeveling] Skill decay ENABLED: {DecayGracePeriodDays} day grace, {DecayBasePointsPerDay} base decay/day, max {DecayMaxPointsPerDay}/day (online-only, per-skill overrides active)");
                }

                if (EnableSleepBuff)
                {
                    api.Logger.Notification($"[SeraphLeveling] Sleep buff ENABLED: linen bed {SleepBuffLinenBedMultiplier}x, hay bed {SleepBuffHayBedMultiplier}x, duration {SleepBuffDurationDays} days");
                }

                if (EnableDeathPenalty)
                {
                    api.Logger.Notification($"[SeraphLeveling] Death penalty ENABLED: fraction={DeathPenaltyFraction}, exempt skills: {(DeathPenaltyExemptSkills.Count > 0 ? string.Join(", ", DeathPenaltyExemptSkills) : "none")}");
                }

                api.Logger.Notification("[SeraphLeveling] Config loaded from ModConfig/" + CONFIG_FILE_NAME);
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[SeraphLeveling] Failed to load config file: {ex.Message}. Using default values.");
            }
        }

        /// <summary>
        /// Writes the current settings back out to ModConfig/SeraphLeveling.json.
        /// The file is the single source of truth: LoadConfigFile reads it at
        /// startup, and every /trait command that changes a value ends up here on
        /// the next world save. That way an edit made in the file and an edit made
        /// in game can never disagree with each other.
        ///
        /// The existing file is loaded first rather than starting from a fresh
        /// SeraphLevelingConfig, so any setting this method forgets to write keeps
        /// the admin's value instead of quietly snapping back to its default.
        /// This is the exact inverse of LoadConfigFile; the two must stay in step.
        /// </summary>
        private void SaveConfigFile()
        {
            if (ServerApi == null) return;

            try
            {
                SeraphLevelingConfig config =
                    ServerApi.LoadModConfig<SeraphLevelingConfig>(CONFIG_FILE_NAME) ?? new SeraphLevelingConfig();

                config.ConfigVersion = CURRENT_CONFIG_VERSION;
                LoadedConfigVersion = CURRENT_CONFIG_VERSION;

                foreach (var attribute in LoadedAttributes)
                {
                    config.AttributeConfiguration[attribute.Id] = attribute.GetConfigData();
                }

                config.MiningOreMultiplier = OreMultiplier;

                config.ArmorFirstEquipLightDurability = FirstEquipLightBonus;
                config.ArmorFirstEquipChainDurability = FirstEquipChainBonus;
                config.ArmorFirstEquipBrigandineDurability = FirstEquipBrigandineBonus;
                config.ArmorFirstEquipScaleDurability = FirstEquipScaleBonus;
                config.ArmorFirstEquipPlateDurability = FirstEquipPlateBonus;

                config.ArmorFirstEquipLightWalkSpeed = FirstEquipWalkSpeedLightBonus;
                config.ArmorFirstEquipChainWalkSpeed = FirstEquipWalkSpeedChainBonus;
                config.ArmorFirstEquipBrigandineWalkSpeed = FirstEquipWalkSpeedBrigandineBonus;
                config.ArmorFirstEquipScaleWalkSpeed = FirstEquipWalkSpeedScaleBonus;
                config.ArmorFirstEquipPlateWalkSpeed = FirstEquipWalkSpeedPlateBonus;

                config.EnableArmorHungerReduction = EnableArmorHungerReduction;
                config.EnableArmorHealingBonus = EnableArmorHealingBonus;

                config.ClothierRequiredUniqueClothes = AttributeModifierDefinitions.Clothier.RequiredCollectionSize;
                config.ClothierBlacklistedItems = AttributeModifierDefinitions.Clothier.TokenBanList.ToArray();

                config.AutoSaveIntervalSeconds = AutoSaveIntervalSeconds;

                config.DisabledSkills = DisabledSkills.ToArray();

                config.EnableCombatOverhaulCompat = COEnableCompat;
                config.COProficiencyBaseDamagePerIncrement = COBaseDamagePerIncrement;
                config.COProficiencyIncrementStep = COIncrementStep;
                config.COProficiencyBaseOverrides = new Dictionary<string, int>(COProficiencyBaseOverrides);
                config.COProficiencyIncrementOverrides = new Dictionary<string, int>(COProficiencyIncrementOverrides);
                config.COBowsProficiencyMax = COBowsProficiencyMax;
                config.COCrossbowsProficiencyMax = COCrossbowsProficiencyMax;
                config.COFirearmsProficiencyMax = COFirearmsProficiencyMax;
                config.COSlingsProficiencyMax = COSlingsProficiencyMax;
                config.COOneHandedSwordsProficiencyMax = COOneHandedSwordsProficiencyMax;
                config.COTwoHandedSwordsProficiencyMax = COTwoHandedSwordsProficiencyMax;
                config.COSpearsProficiencyMax = COSpearsProficiencyMax;
                config.COJavelinsProficiencyMax = COJavelinsProficiencyMax;
                config.COMacesProficiencyMax = COMacesProficiencyMax;
                config.COClubsProficiencyMax = COClubsProficiencyMax;
                config.COHalberdsProficiencyMax = COHalberdsProficiencyMax;
                config.COPoleaxeProficiencyMax = COPoleaxeProficiencyMax;
                config.COAxesProficiencyMax = COAxesProficiencyMax;
                config.COQuarterstaffProficiencyMax = COQuarterstaffProficiencyMax;
                config.COSteadyAimMax = COSteadyAimMax;

                config.EnableSkillDecay = EnableSkillDecay;
                config.DecayGracePeriodDays = DecayGracePeriodDays;
                config.DecayBasePointsPerDay = DecayBasePointsPerDay;
                config.DecayMaxPointsPerDay = DecayMaxPointsPerDay;
                config.DecayExemptSkills = DecayExemptSkills.ToArray();
                config.DecayGracePeriodOverrides = new Dictionary<string, double>(DecayGracePeriodOverrides);
                config.DecayBasePointsOverrides = new Dictionary<string, int>(DecayBasePointsOverrides);
                config.DecayMaxPointsOverrides = new Dictionary<string, int>(DecayMaxPointsOverrides);

                config.EnableSleepBuff = EnableSleepBuff;
                config.SleepBuffLinenBedMultiplier = SleepBuffLinenBedMultiplier;
                config.SleepBuffHayBedMultiplier = SleepBuffHayBedMultiplier;
                config.SleepBuffDurationDays = SleepBuffDurationDays;

                config.EnableDeathPenalty = EnableDeathPenalty;
                config.DeathPenaltyFraction = DeathPenaltyFraction;
                config.DeathPenaltyExemptSkills = DeathPenaltyExemptSkills.ToArray();

                config.EnableLevelUpMessages = EnableLevelUpMessages;
                config.EnableLevelUpSound = EnableLevelUpSound;
                config.LevelUpSoundName = LevelUpSoundName;
                config.LevelUpSoundVolume = LevelUpSoundVolume;

                config.EnableDebugLogging = DebugLoggingEnabled;
                config.VerboseDecayLogging = VerboseDecayLogging;

                ServerApi.StoreModConfig(config, CONFIG_FILE_NAME);
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to write config file: {ex.Message}");
            }
        }

        // =========================================================================
        // SKILL DECAY SYSTEM
        // =========================================================================

        /// <summary>
        /// Get per-skill decay parameters, falling back to global defaults.
        /// Returns (gracePeriodDays, basePointsPerDay, maxPointsPerDay).
        /// </summary>
        public static (double grace, int basePoints, int maxPoints) GetDecayParams(string skillKey)
        {
            double grace = DecayGracePeriodOverrides.TryGetValue(skillKey, out var g) ? g : DecayGracePeriodDays;
            int basePoints = DecayBasePointsOverrides.TryGetValue(skillKey, out var b) ? b : DecayBasePointsPerDay;
            int maxPoints = DecayMaxPointsOverrides.TryGetValue(skillKey, out var m) ? m : DecayMaxPointsPerDay;
            return (grace, basePoints, maxPoints);
        }

        /// <summary>
        /// Calculate decay points for one day of inactivity based on days since last activity.
        /// Uses triangular formula: consecutive inactive days multiply the base rate.
        /// Only calculates decay for a single day tick (not cumulative).
        /// </summary>
        public static int CalculateDecayPoints(double lastActivityDay, double currentDay,
            double gracePeriodDays, int basePointsPerDay, int maxPointsPerDay)
        {
            if (lastActivityDay <= 0) return 0; // No activity recorded yet, no decay
            if (currentDay <= lastActivityDay) return 0; // No time passed

            double daysSinceActivity = currentDay - lastActivityDay;
            double daysAfterGrace = daysSinceActivity - gracePeriodDays;

            if (daysAfterGrace <= 0) return 0; // Still in grace period

            // Triangular: the Nth day past grace loses N * base points
            int dayNumber = (int)Math.Ceiling(daysAfterGrace);
            int decayThisDay = dayNumber * basePointsPerDay;

            // Cap at maximum per day
            return Math.Min(decayThisDay, maxPointsPerDay);
        }

        /// <summary>
        /// Decay tick handler. Runs every 10 seconds, checks if a full in-game day has passed
        /// for each online player, and applies decay if so.
        /// </summary>
        private void OnDecayTick(float dt)
        {
            if (!EnableSkillDecay) return;
            if (ServerApi?.World?.AllOnlinePlayers == null) return;

            double currentDay = ServerApi.World.Calendar.TotalDays;

            foreach (var onlinePlayer in ServerApi.World.AllOnlinePlayers)
            {
                var player = onlinePlayer as IServerPlayer;
                if (player?.Entity == null) continue;

                string playerUid = player.PlayerUID;

                if (!LastDecayCheckDay.TryGetValue(playerUid, out double lastCheck))
                    continue; // Not initialized yet (hasn't joined)

                // Process one day at a time to handle multi-day skips incrementally
                while (currentDay - lastCheck >= 1.0)
                {
                    lastCheck += 1.0;
                    ApplyDailyDecay(player, lastCheck);
                }

                LastDecayCheckDay[playerUid] = lastCheck;
            }
        }

        /// <summary>
        /// Apply one day of decay for a single player across all 13 progression skills.
        /// Called from OnDecayTick when a full in-game day has elapsed.
        /// </summary>
        private void ApplyDailyDecay(IServerPlayer player, double currentDay)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var sb = new StringBuilder();
            int totalDecayApplied = 0;
            StringBuilder verboseSb = VerboseDecayLogging ? new StringBuilder() : null;

            // --- Per-tool dictionary skills ---
            foreach (var definition in LoadedAttributes)
            {
                totalDecayApplied += definition.ApplyDecay(player, currentDay, sb, verboseSb);
            }

            // CO Proficiency (per-proficiency absolute-position drain + SteadyAim direct)
            if (!DecayExemptSkills.Contains("coproficiency") && !DisabledSkills.Contains("coproficiency") && IsCOCompatEnabled)
            {
                if (COProgress.TryGetValue(playerUid, out var coProg))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("coproficiency");
                    int decay = CalculateDecayPoints(coProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decay > 0)
                    {
                        int coDecayTotal = 0;
                        var coSb = new StringBuilder();
                        foreach (var profKvp in coProg.Proficiencies)
                        {
                            if (profKvp.Value.TotalCredits > 0 || profKvp.Value.WeaponProgress.Count > 0)
                            {
                                int oldProfCredits = profKvp.Value.TotalCredits;
                                var toolEntries = profKvp.Value.WeaponProgress.Select(kvp =>
                                    (kvp.Key, (double)kvp.Value.DamageInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                                if (toolEntries.Count > 0)
                                {
                                    double rawPenalty = (double)decay;

                                    var (newCr, lost) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                                        COBaseDamagePerIncrement, COIncrementStep, oldProfCredits,
                                        (k, a, s) =>
                                        {
                                            if (profKvp.Value.WeaponProgress.TryGetValue(k, out var p))
                                            {
                                                p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s;
                                            }
                                        },
                                        k => profKvp.Value.WeaponProgress.Remove(k), verboseSb, $"CO:{profKvp.Key}");
                                    profKvp.Value.TotalCredits = newCr;
                                    coSb.AppendLine($"    {profKvp.Key}: {oldProfCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts)");
                                    coDecayTotal += lost;
                                }
                                else
                                {
                                    int lost = Math.Min(decay, oldProfCredits);
                                    profKvp.Value.TotalCredits -= lost;
                                    if (lost > 0) coSb.AppendLine($"    {profKvp.Key}: {oldProfCredits} \u2192 {profKvp.Value.TotalCredits} (-{lost} credits)");
                                    coDecayTotal += lost;
                                }
                            }
                        }
                        if (coProg.SteadyAimCredits > 0)
                        {
                            int old = coProg.SteadyAimCredits;
                            coProg.SteadyAimCredits = Math.Max(0, coProg.SteadyAimCredits - decay);
                            int steadyLost = old - coProg.SteadyAimCredits;
                            if (steadyLost > 0) coSb.AppendLine($"    SteadyAim: {old} \u2192 {coProg.SteadyAimCredits} (-{steadyLost} credits)");
                            coDecayTotal += steadyLost;
                        }
                        if (coDecayTotal > 0) totalDecayApplied += coDecayTotal;
                        if (coSb.Length > 0)
                        {
                            pendingCOProgressSave = true;
                            sb.AppendLine($"  CO Proficiency: -{coDecayTotal} credits");
                            sb.Append(coSb);
                        }
                    }
                }
            }

            // If any decay occurred, re-apply all bonuses and notify
            if (totalDecayApplied > 0 || sb.Length > 0)
            {
                if (totalDecayApplied > 0) ReapplyAllBonuses(player);

                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    $"Skills decayed due to inactivity{(totalDecayApplied > 0 ? $" (-{totalDecayApplied} total credits)" : "")}:\n{sb}Use them to regain your progress!",
                    EnumChatType.Notification);

                ServerApi.Logger.Debug($"[SeraphLeveling] Daily decay applied to {player.PlayerName}: {totalDecayApplied} total credits lost");
            }

            if (VerboseDecayLogging && verboseSb != null && verboseSb.Length > 0)
                player.SendMessage(GlobalConstants.GeneralChatGroup, $"[Decay Detail]\n{verboseSb}", EnumChatType.Notification);
        }

        /// <summary>
        /// Re-apply all bonuses for a player after decay has reduced credits.
        /// </summary>
        private void ReapplyAllBonuses(IServerPlayer player)
        {
            string playerUid = player.PlayerUID;
            foreach (var definition in LoadedAttributes)
            {
                definition.ApplyBonusIfExists(player);
            }
            if (IsCOCompatEnabled && COProgress.TryGetValue(playerUid, out var coProg))
                ApplyAllCOBonuses(player);
        }

        /// <summary>
        /// Update the LastActivityDay for a skill when it's used.
        /// </summary>
        private static void UpdateSkillActivityDay(string playerUid, string skillType)
        {
            if (!EnableSkillDecay) return;
            if (ServerApi == null) return;

            double currentDay = ServerApi.World.Calendar.TotalDays;

            switch (skillType)
            {
                case "coproficiency":
                    if (COProgress.TryGetValue(playerUid, out var coProg))
                        coProg.LastActivityDay = currentDay;
                    break;
            }
        }

        // =========================================================================
        // SLEEP BUFF SYSTEM
        // =========================================================================

        /// <summary>
        /// Get the current XP multiplier for a player.
        /// Returns the sleep buff multiplier if active, otherwise 1.0.
        /// </summary>
        public static float GetXPMultiplier(string playerUid)
        {
            if (!EnableSleepBuff) return 1.0f;
            if (string.IsNullOrEmpty(playerUid)) return 1.0f;
            if (ServerApi == null) return 1.0f;

            // Check if player has an active sleep buff
            if (!SleepBuffExpiration.TryGetValue(playerUid, out double expiration)) return 1.0f;
            if (!SleepBuffMultiplier.TryGetValue(playerUid, out float multiplier)) return 1.0f;

            double currentDay = ServerApi.World?.Calendar?.TotalDays ?? 0;

            // Check if buff has expired
            if (currentDay >= expiration)
            {
                // Remove expired buff
                SleepBuffExpiration.TryRemove(playerUid, out _);
                SleepBuffMultiplier.TryRemove(playerUid, out _);
                pendingSleepBuffSave = true;
                return 1.0f;
            }

            return multiplier;
        }

        /// <summary>
        /// Apply the XP multiplier to a value (for progress tracking).
        /// </summary>
        public static float ApplyXPMultiplier(string playerUid, float baseValue)
        {
            float multiplier = GetXPMultiplier(playerUid);
            return baseValue * multiplier;
        }

        /// <summary>
        /// Apply the XP multiplier to an integer value (for progress tracking).
        /// </summary>
        public static int ApplyXPMultiplier(string playerUid, int baseValue)
        {
            float multiplier = GetXPMultiplier(playerUid);
            return (int)(baseValue * multiplier);
        }

        // =========================================================================
        // DEATH PENALTY SYSTEM
        // =========================================================================

        /// <summary>
        /// Drains 'penalty' raw points from a list of per-tool accumulators using water-leveling.
        /// Brings the highest accumulator down toward the next highest, then drains equally, etc.
        /// Returns remaining penalty after all accumulators are drained to zero.
        /// </summary>
        public static double DrainAccumulatorsLeveling(List<(string key, double value)> accumulators, double penalty)
        {
            if (accumulators == null || accumulators.Count == 0 || penalty <= 0) return penalty;

            // Sort descending by value
            accumulators.Sort((a, b) => b.value.CompareTo(a.value));

            double remaining = penalty;

            while (remaining > 0)
            {
                // Find the current top value
                double topValue = accumulators[0].value;
                if (topValue <= 0) break; // All accumulators are drained

                // Find how many entries share the top tier and the next level down
                int topCount = 1;
                double nextLevel = 0;
                for (int i = 1; i < accumulators.Count; i++)
                {
                    if (accumulators[i].value >= topValue - 0.001)
                    {
                        topCount++;
                    }
                    else
                    {
                        nextLevel = accumulators[i].value;
                        break;
                    }
                }

                // Cost to bring all top entries down to nextLevel
                double dropPerEntry = topValue - nextLevel;
                double totalCost = dropPerEntry * topCount;

                if (remaining >= totalCost)
                {
                    // Fully drain this tier to the next level
                    for (int i = 0; i < topCount; i++)
                    {
                        accumulators[i] = (accumulators[i].key, nextLevel);
                    }
                    remaining -= totalCost;
                }
                else
                {
                    // Partially drain: distribute remaining evenly among top entries
                    double drainPerEntry = remaining / topCount;
                    for (int i = 0; i < topCount; i++)
                    {
                        accumulators[i] = (accumulators[i].key, accumulators[i].value - drainPerEntry);
                    }
                    remaining = 0;
                }
            }

            // Clamp any negative values from floating point
            for (int i = 0; i < accumulators.Count; i++)
            {
                if (accumulators[i].value < 0)
                    accumulators[i] = (accumulators[i].key, 0);
            }

            return remaining;
        }

        /// <summary>
        /// Compute the absolute position (total raw points ever earned) for a single tool,
        /// given its current accumulator value and increment size.
        /// The cost for the Nth credit from this tool is: baseIncrement + (N-1)*incrementStep.
        /// So if currentIncrementSize = baseIncrement + N*incrementStep, then N credits were earned.
        /// Absolute position = sum of costs for those N credits + current accumulator.
        /// </summary>
        public static double ToolToAbsolutePosition(double accumulator, int currentIncrementSize,
            int baseIncrement, int incrementStep)
        {
            // N = number of credits this tool has earned
            int N = incrementStep > 0 ? (currentIncrementSize - baseIncrement) / incrementStep : 0;
            if (N < 0) N = 0;
            // Sum of costs: N*base + step*N*(N-1)/2
            double sumOfCosts = (double)N * baseIncrement + (double)incrementStep * N * (N - 1) / 2.0;
            return sumOfCosts + Math.Max(0, accumulator);
        }

        /// <summary>
        /// Convert an absolute position back into (credits, accumulator, nextIncrementSize).
        /// Subtracts escalating costs until the remainder is less than the next cost.
        /// </summary>
        public static (int credits, double accumulator, int incrementSize) AbsolutePositionToToolState(
            double absolutePosition, int baseIncrement, int incrementStep)
        {
            if (absolutePosition <= 0)
                return (0, 0, baseIncrement);

            int credits = 0;
            double remaining = absolutePosition;
            while (true)
            {
                int nextCost = baseIncrement + credits * incrementStep;
                if (remaining < nextCost)
                    break;
                remaining -= nextCost;
                credits++;
            }
            int incrementSize = baseIncrement + credits * incrementStep;
            // Guard against floating-point edge case where remaining ≈ incrementSize
            if (remaining >= incrementSize - 0.01)
                remaining = incrementSize - 1;
            return (credits, remaining, incrementSize);
        }

        /// <summary>
        /// Apply absolute-position decay to a set of per-tool accumulators.
        /// 1) Convert each tool to absolute position
        /// 2) Water-level drain absolute positions via DrainAccumulatorsLeveling
        /// 3) Convert back to (credits, accumulator, incrementSize) per tool
        /// 4) Write back via delegates; remove entries at zero
        /// Returns (newTotalCredits, creditsLost).
        /// </summary>
        public static (int newTotalCredits, int creditsLost) ApplyAbsolutePositionDecay(
            List<(string key, double accumulator, int incrementSize)> toolEntries,
            double rawPenalty, int baseIncrement, int incrementStep, int oldTotalCredits,
            Action<string, double, int> writeBack,
            Action<string> removeEntry,
            StringBuilder verboseLog, string skillName)
        {
            // Step 1: Convert to absolute positions
            var absPositions = new List<(string key, double value)>();
            foreach (var entry in toolEntries)
            {
                double absPos = ToolToAbsolutePosition(entry.accumulator, entry.incrementSize, baseIncrement, incrementStep);
                absPositions.Add((entry.key, absPos));
            }

            // Step 2: Water-level drain
            double remaining = DrainAccumulatorsLeveling(absPositions, rawPenalty);

            // Step 3: Convert back and write
            int newTotalCredits = 0;
            var toRemove = new List<string>();
            foreach (var absEntry in absPositions)
            {
                var (credits, accum, incSize) = AbsolutePositionToToolState(absEntry.value, baseIncrement, incrementStep);
                if (credits == 0 && accum < 0.001)
                {
                    toRemove.Add(absEntry.key);
                }
                else
                {
                    writeBack(absEntry.key, accum, incSize);
                    newTotalCredits += credits;
                }

                if (verboseLog != null)
                    verboseLog.AppendLine($"  [{skillName}] {absEntry.key}: absPos={absEntry.value:F1} -> cr={credits}, acc={accum:F1}, inc={incSize}");
            }

            foreach (var key in toRemove)
                removeEntry(key);

            // If there's remaining penalty after all tools drained to zero, subtract from credits directly
            if (remaining > 0.001 && newTotalCredits > 0)
            {
                // This shouldn't normally happen since absolute positions encompass credits,
                // but handle edge case of oldTotalCredits > sum of per-tool credits
                int extraLoss = (int)Math.Floor(remaining / baseIncrement);
                newTotalCredits = Math.Max(0, newTotalCredits - extraLoss);
            }

            int creditsLost = Math.Max(0, oldTotalCredits - newTotalCredits);
            return (newTotalCredits, creditsLost);
        }

        /// <summary>
        /// Apply absolute-position decay to a single-accumulator skill.
        /// Computes absolute position from oldTotalCredits (more robust than from incrementSize),
        /// subtracts rawPenalty, converts back.
        /// </summary>
        public static (int newCredits, double newAccumulator, int newIncrementSize, int creditsLost)
            ApplySingleAccumulatorDecay(
            double currentAccumulator, int currentIncrementSize, int oldTotalCredits,
            double rawPenalty, int baseIncrement, int incrementStep,
            StringBuilder verboseLog, string skillName)
        {
            // Compute absolute position from oldTotalCredits rather than incrementSize for robustness
            // Sum of costs for N credits: N*base + step*N*(N-1)/2
            double sumOfCosts = (double)oldTotalCredits * baseIncrement +
                (double)incrementStep * oldTotalCredits * (oldTotalCredits - 1) / 2.0;
            double absolutePosition = sumOfCosts + Math.Max(0, currentAccumulator);

            double newAbsPosition = Math.Max(0, absolutePosition - rawPenalty);
            var (newCredits, newAccumulator, newIncSize) = AbsolutePositionToToolState(newAbsPosition, baseIncrement, incrementStep);

            int creditsLost = Math.Max(0, oldTotalCredits - newCredits);

            if (verboseLog != null)
                verboseLog.AppendLine($"  [{skillName}] absPos={absolutePosition:F1} - penalty={rawPenalty:F1} -> newAbsPos={newAbsPosition:F1}, cr={newCredits}, acc={newAccumulator:F1}, inc={newIncSize}");

            return (newCredits, newAccumulator, newIncSize, creditsLost);
        }

        /// <summary>
        /// Apply death penalty to all skills for a player.
        /// Uses binary-search to guarantee credit loss for per-tool dictionary skills.
        /// </summary>
        public static void ApplyDeathPenalty(IServerPlayer player)
        {
            if (!EnableDeathPenalty) return;
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var sb = new StringBuilder();
            int totalCreditsLost = 0;

            foreach (var definition in LoadedAttributes)
            {
                totalCreditsLost += definition.ApplyDeathPenalty(player, sb);
            }
            // --- Single accumulator skills ---

            // --- CO Proficiency (per-proficiency subcredit drain) ---
            if (!DeathPenaltyExemptSkills.Contains("coproficiency") && !DisabledSkills.Contains("coproficiency") && IsCOCompatEnabled)
            {
                if (COProgress.TryGetValue(playerUid, out var coProg))
                {
                    int coCreditsLost = 0;
                    var coSb = new StringBuilder();
                    foreach (var profKvp in coProg.Proficiencies)
                    {
                        if (profKvp.Value.TotalCredits > 0 || profKvp.Value.WeaponProgress.Count > 0)
                        {
                            int oldProfCredits = profKvp.Value.TotalCredits;

                            var toolEntries = profKvp.Value.WeaponProgress.Select(kvp =>
                                (kvp.Key, (double)kvp.Value.DamageInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                            if (toolEntries.Count > 0)
                            {
                                double rawPenalty = COBaseDamagePerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldProfCredits));
                                var (newCr, _) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                                    COBaseDamagePerIncrement, COIncrementStep, oldProfCredits,
                                    (k, a, s) =>
                                    {
                                        if (profKvp.Value.WeaponProgress.TryGetValue(k, out var p))
                                        {
                                            p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s;
                                        }
                                    },
                                    k => profKvp.Value.WeaponProgress.Remove(k), null, $"CO:{profKvp.Key}");
                                profKvp.Value.TotalCredits = newCr;
                                int actualLost = oldProfCredits - newCr;
                                coCreditsLost += actualLost;
                                coSb.AppendLine($"    {profKvp.Key}: {oldProfCredits} \u2192 {newCr} (-{actualLost} credits, {rawPenalty:F0} pts)");
                            }
                            else if (oldProfCredits > 0)
                            {
                                int intendedLoss = (int)Math.Floor(DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldProfCredits)));
                                intendedLoss = Math.Min(intendedLoss, oldProfCredits);
                                if (intendedLoss > 0)
                                {
                                    profKvp.Value.TotalCredits = Math.Max(0, oldProfCredits - intendedLoss);
                                    int actualLost = oldProfCredits - profKvp.Value.TotalCredits;
                                    coCreditsLost += actualLost;
                                    coSb.AppendLine($"    {profKvp.Key}: {oldProfCredits} \u2192 {profKvp.Value.TotalCredits} (-{actualLost} credits)");
                                }
                            }
                        }
                    }
                    if (coProg.SteadyAimCredits > 0)
                    {
                        int old = coProg.SteadyAimCredits;
                        int loss = (int)Math.Floor(DeathPenaltyFraction * Math.Sqrt(Math.Max(1, old)));
                        if (loss > 0)
                        {
                            coProg.SteadyAimCredits = Math.Max(0, old - loss);
                            int actualLoss = old - coProg.SteadyAimCredits;
                            coCreditsLost += actualLoss;
                            if (actualLoss > 0)
                                coSb.AppendLine($"    SteadyAim: {old} \u2192 {coProg.SteadyAimCredits} (-{actualLoss} credits)");
                        }
                    }
                    if (coCreditsLost > 0)
                    {
                        totalCreditsLost += coCreditsLost;
                        pendingCOProgressSave = true;
                        sb.AppendLine($"  CO Proficiency: -{coCreditsLost} credits");
                        sb.Append(coSb);
                    }
                }
            }

            // Re-apply bonuses and notify player
            if (totalCreditsLost > 0 || sb.Length > 0)
            {
                Instance?.ReapplyAllBonuses(player);

                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    $"Death penalty applied (-{totalCreditsLost} total credits):\n{sb}Fight on to regain your skills!",
                    EnumChatType.Notification);

                ServerApi?.Logger.Debug($"[SeraphLeveling] Death penalty applied to {player.PlayerName}: {totalCreditsLost} total credits lost");
            }
        }

        /// <summary>
        /// Handler for /trait sleepbuff command. Shows sleep buff status.
        /// </summary>
        private TextCommandResult OnTraitSleepBuffCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var sb = new StringBuilder();

            if (!EnableSleepBuff)
            {
                sb.AppendLine("Sleep Buff: DISABLED (not enabled in config)");
                return TextCommandResult.Success(sb.ToString());
            }

            sb.AppendLine("Sleep Buff: ENABLED");
            sb.AppendLine($"  Linen/Old bed multiplier: {SleepBuffLinenBedMultiplier:F1}x");
            sb.AppendLine($"  Hay bed multiplier: {SleepBuffHayBedMultiplier:F1}x");
            sb.AppendLine($"  Duration: {SleepBuffDurationDays:F1} in-game days");
            sb.AppendLine();

            string playerUid = player.PlayerUID;
            double currentDay = ServerApi?.World?.Calendar?.TotalDays ?? 0;

            if (SleepBuffExpiration.TryGetValue(playerUid, out double expiration) &&
                SleepBuffMultiplier.TryGetValue(playerUid, out float multiplier) &&
                currentDay < expiration)
            {
                double remainingDays = expiration - currentDay;
                double remainingHours = remainingDays * 24;
                sb.AppendLine($"  Status: ACTIVE");
                sb.AppendLine($"  Current multiplier: {multiplier:F1}x");
                sb.AppendLine($"  Time remaining: {remainingDays:F2} days ({remainingHours:F1} in-game hours)");
            }
            else
            {
                sb.AppendLine($"  Status: INACTIVE");
                sb.AppendLine($"  Sleep in a bed to activate the buff!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait decay command. Shows skill decay status.
        /// </summary>
        private TextCommandResult OnTraitDecayCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var sb = new StringBuilder();

            if (!EnableSkillDecay)
            {
                sb.AppendLine("Skill Decay: DISABLED (not enabled in config)");
                return TextCommandResult.Success(sb.ToString());
            }

            sb.AppendLine("Skill Decay: ENABLED (online-only, checked once per in-game day)");
            sb.AppendLine($"  Default grace period: {DecayGracePeriodDays:F1} days");
            sb.AppendLine($"  Default base decay: {DecayBasePointsPerDay} points/day");
            sb.AppendLine($"  Default max decay: {DecayMaxPointsPerDay} points/day");
            if (DecayExemptSkills.Count > 0)
            {
                sb.AppendLine($"  Exempt skills: {string.Join(", ", DecayExemptSkills)}");
            }
            sb.AppendLine();

            string playerUid = player.PlayerUID;
            double currentDay = ServerApi?.World?.Calendar?.TotalDays ?? 0;

            // Combat skills
            sb.AppendLine("--- Combat Skills ---");
            AppendDecayStatus(sb, "Mining", "mining", playerUid, currentDay,
                () => AttributeModifierDefinitions.MiningSpeed.ProgressDictionary.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Melee", "melee", playerUid, currentDay,
                () => AttributeModifierDefinitions.MeleeDamage.ProgressDictionary.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Ranged", "ranged", playerUid, currentDay,
                () => AttributeModifierDefinitions.RangedDamage.ProgressDictionary.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Ranged Accuracy", "rangedaccuracy", playerUid, currentDay,
                () => AttributeModifierDefinitions.RangedAccuracy.ProgressDictionary.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Ranged Distance", "rangeddistance", playerUid, currentDay,
                () => AttributeModifierDefinitions.RangedDistance.ProgressDictionary.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Precise", "precise", playerUid, currentDay,
                () => AttributeModifierDefinitions.Precise.ProgressDictionary.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));

            // Movement/Survival skills
            sb.AppendLine("--- Movement/Survival ---");
            AppendDecayStatus(sb, "Walking", "walking", playerUid, currentDay,
                () => AttributeModifierDefinitions.WalkingSpeed.ProgressDictionary.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Hunger", "hunger", playerUid, currentDay,
                () => AttributeModifierDefinitions.HungerRate.ProgressDictionary.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Furtive", "furtive", playerUid, currentDay,
                () => AttributeModifierDefinitions.Furtive.ProgressDictionary.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));

            // Utility skills
            sb.AppendLine("--- Utility ---");
            AppendDecayStatus(sb, "Mender", "mender", playerUid, currentDay,
                () => AttributeModifierDefinitions.Mender.ProgressDictionary.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Pilferer", "pilferer", playerUid, currentDay, // TODO: the other two pilferer subskills.
                () => AttributeModifierDefinitions.VesselDropRate.ProgressDictionary.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Resourceful", "resourceful", playerUid, currentDay,
                () => AttributeModifierDefinitions.AnimalDropRate.ProgressDictionary.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Animal Harvest Rate", "animalharvester", playerUid, currentDay,
                () => AttributeModifierDefinitions.AnimalHarvestRate.ProgressDictionary.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Looting Bonus", "foragerlooting", playerUid, currentDay,
                () => AttributeModifierDefinitions.ForageLootingBonus.ProgressDictionary.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Crop Drop Rate", "forager", playerUid, currentDay,
                () => AttributeModifierDefinitions.WildCropDropRate.ProgressDictionary.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));

            // CO Proficiency
            if (IsCOCompatEnabled)
            {
                sb.AppendLine("--- Combat Overhaul ---");
                AppendDecayStatus(sb, "CO Proficiency", "coproficiency", playerUid, currentDay,
                    () => COProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.SteadyAimCredits) : (0, 0));
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Helper to append decay status for a single skill with per-skill parameters.
        /// </summary>
        private void AppendDecayStatus(StringBuilder sb, string displayName, string skillKey, string playerUid, double currentDay, Func<(double lastActivity, int credits)> getProgress)
        {
            var (lastActivity, credits) = getProgress();
            bool exempt = DecayExemptSkills.Contains(skillKey);

            sb.Append($"  {displayName}: ");

            if (exempt)
            {
                sb.AppendLine("EXEMPT");
                return;
            }

            if (credits <= 0 && lastActivity <= 0)
            {
                sb.AppendLine("No progress yet");
                return;
            }

            if (lastActivity <= 0)
            {
                sb.AppendLine($"{credits} credits (no activity tracked)");
                return;
            }

            var (grace, basePoints, maxPoints) = GetDecayParams(skillKey);
            double daysSinceActivity = currentDay - lastActivity;
            double daysUntilDecay = grace - daysSinceActivity;
            int pendingDecay = CalculateDecayPoints(lastActivity, currentDay, grace, basePoints, maxPoints);

            // Show per-skill params if they differ from global defaults
            string paramInfo = "";
            if (grace != DecayGracePeriodDays || basePoints != DecayBasePointsPerDay || maxPoints != DecayMaxPointsPerDay)
            {
                paramInfo = $" [grace:{grace:F1}d, base:{basePoints}, max:{maxPoints}]";
            }

            if (daysUntilDecay > 0)
            {
                sb.AppendLine($"{credits} credits - Safe ({daysUntilDecay:F1} days until decay){paramInfo}");
            }
            else if (pendingDecay > 0)
            {
                sb.AppendLine($"{credits} credits - DECAYING (-{pendingDecay} pts/day, {daysSinceActivity:F1} days inactive){paramInfo}");
            }
            else
            {
                sb.AppendLine($"{credits} credits - Active{paramInfo}");
            }
        }

        /// <summary>
        /// Detect if Combat Overhaul mod is loaded and log the result.
        /// </summary>
        private void DetectCombatOverhaul(IModLoader modLoader)
        {
            // Accept the original Combat Overhaul AND the 1.22 fork
            // ("combatoverhaulfork"). The fork keeps CO's stat/trait names, so
            // detecting it re-enables proficiency progression, the bonus stat
            // application, and the /trait co* commands.
            IsCombatOverhaulLoaded = DetectAnyCombatOverhaul(modLoader);
            IsCombatOverhaulForkLoaded = modLoader.IsModEnabled("combatoverhaulfork");

            if (IsCombatOverhaulLoaded)
            {
                string which = IsCombatOverhaulForkLoaded
                    ? "Combat Overhaul (1.22 fork)" : "Combat Overhaul";
                if (COEnableCompat)
                {
                    ServerApi.Logger.Notification($"[SeraphLeveling] {which} detected - proficiency progression enabled");
                }
                else
                {
                    ServerApi.Logger.Notification($"[SeraphLeveling] {which} detected but compatibility disabled in config");
                }
            }
        }

        /// <summary>
        /// Get the max proficiency value for a given CO proficiency stat.
        /// </summary>
        public static float GetCOProficiencyMax(string proficiencyStat)
        {
            switch (proficiencyStat)
            {
                case CO_BOWS_PROFICIENCY: return COBowsProficiencyMax;
                case CO_CROSSBOWS_PROFICIENCY: return COCrossbowsProficiencyMax;
                case CO_FIREARMS_PROFICIENCY: return COFirearmsProficiencyMax;
                case CO_SLINGS_PROFICIENCY: return COSlingsProficiencyMax;
                case CO_ONE_HANDED_SWORDS_PROFICIENCY: return COOneHandedSwordsProficiencyMax;
                case CO_TWO_HANDED_SWORDS_PROFICIENCY: return COTwoHandedSwordsProficiencyMax;
                case CO_SPEARS_PROFICIENCY: return COSpearsProficiencyMax;
                case CO_JAVELINS_PROFICIENCY: return COJavelinsProficiencyMax;
                case CO_MACES_PROFICIENCY: return COMacesProficiencyMax;
                case CO_CLUBS_PROFICIENCY: return COClubsProficiencyMax;
                case CO_HALBERDS_PROFICIENCY: return COHalberdsProficiencyMax;
                case CO_POLEAXE_PROFICIENCY: return COPoleaxeProficiencyMax;
                case CO_AXES_PROFICIENCY: return COAxesProficiencyMax;
                case CO_QUARTERSTAFF_PROFICIENCY: return COQuarterstaffProficiencyMax;
                case CO_STEADY_AIM: return COSteadyAimMax;
                default: return 0.3f; // Safe default
            }
        }

        /// <summary>
        /// Get the max credits for a CO proficiency (max bonus * 100).
        /// </summary>
        public static int GetCOProficiencyMaxCredits(string proficiencyStat)
        {
            return (int)(GetCOProficiencyMax(proficiencyStat) * 100);
        }

        /// <summary>
        /// Get max Steady Aim credits for a player, accounting for Trembling Aim.
        /// Players with Trembling Aim get extra credits to compensate for the penalty.
        /// </summary>
        public static int GetCOSteadyAimMaxCreditsForPlayer(string playerUid)
        {
            int baseMax = GetCOProficiencyMaxCredits(CO_STEADY_AIM); // 50
            var cache = GetCachedTraits(playerUid);
            if (cache?.HasCOTremblingAim == true)
            {
                // Add 30 credits to cancel Trembling Aim penalty (0.30 * 100 = 30)
                return baseMax + (int)(CO_TREMBLING_AIM_PENALTY * 100); // 50 + 30 = 80
            }
            return baseMax;
        }

        /// <summary>
        /// Get max credits for a CO proficiency for a specific player, accounting for negative traits.
        /// Players with negative traits (Clumsy Hands, Weak Hand, Fear of Melee, Nervous) get extra
        /// credits to compensate for their penalties before earning positive bonuses.
        /// </summary>
        public static int GetCOProficiencyMaxCreditsForPlayer(string playerUid, string proficiencyStat)
        {
            int baseMax = GetCOProficiencyMaxCredits(proficiencyStat);
            var cache = GetCachedTraits(playerUid);
            if (cache == null) return baseMax;

            bool isRanged = IsCORangedProficiency(proficiencyStat);
            bool isPiercing = IsCOPiercingProficiency(proficiencyStat);

            // Clumsy Hands: affects ranged proficiencies (bows, crossbows, firearms, slings)
            if (isRanged && cache.HasCOClumsyHands)
            {
                return baseMax + (int)(CO_CLUMSY_HANDS_PENALTY * 100); // +30
            }
            // Weak Hand: affects ranged proficiencies (similar to Clumsy Hands)
            else if (isRanged && cache.HasCOWeakHand)
            {
                return baseMax + (int)(CO_WEAK_HAND_PENALTY * 100); // +30
            }
            // Fear of Melee: affects melee proficiencies (non-ranged)
            else if (!isRanged && cache.HasCOFearOfMelee)
            {
                return baseMax + CO_FEAR_OF_MELEE_TIER_PENALTY * 100; // +100
            }
            // Nervous: affects piercing proficiencies (spears, javelins)
            else if (isPiercing && cache.HasCONervous)
            {
                return baseMax + CO_NERVOUS_TIER_PENALTY * 100; // +100
            }

            return baseMax;
        }

        /// <summary>
        /// Calculate proficiency bonus from credits.
        /// Each credit = 0.01 bonus.
        /// </summary>
        public static float CalculateCOProficiencyBonus(int credits, float maxBonus)
        {
            int maxCredits = (int)(maxBonus * 100);
            int cappedCredits = Math.Min(credits, maxCredits);
            return cappedCredits * 0.01f;
        }

        /// <summary>
        /// Detect Combat Overhaul weapon type from item code.
        /// Returns (proficiencyStat, weaponCode) or (null, null) if not a CO weapon.
        /// </summary>
        public static (string proficiencyStat, string weaponCode) GetCOWeaponType(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return (null, null);

            // Remove namespace prefix for pattern matching
            string codeToCheck = itemCode;
            if (itemCode.Contains(":"))
            {
                codeToCheck = itemCode.Substring(itemCode.IndexOf(':') + 1);
            }
            string lowerCode = codeToCheck.ToLowerInvariant();

            // Crossbows (check before bows)
            if (lowerCode.StartsWith("crossbow-") || lowerCode.StartsWith("crossbow") ||
                lowerCode.Contains("crossbow"))
                return (CO_CROSSBOWS_PROFICIENCY, itemCode);

            // Firearms
            if (lowerCode.StartsWith("musket-") || lowerCode.StartsWith("pistol-") ||
                lowerCode.StartsWith("rifle-") || lowerCode.StartsWith("blunderbuss-") ||
                lowerCode.StartsWith("arquebus-") || lowerCode.Contains("firearm") ||
                lowerCode.Contains("gun-"))
                return (CO_FIREARMS_PROFICIENCY, itemCode);

            // Two-Handed Swords (check before one-handed)
            // Combat Armory uses "sword-great-" and "sword-long-" formats
            // Ancient Armory uses "aa-blade-claymore-" and "aa-blade-longsword-" formats
            if (lowerCode.StartsWith("greatsword-") || lowerCode.StartsWith("zweihander-") ||
                lowerCode.StartsWith("claymore-") || lowerCode.StartsWith("flamberge-") ||
                lowerCode.StartsWith("montante-") || lowerCode.StartsWith("nodachi-") ||
                lowerCode.StartsWith("2hsword-") || lowerCode.StartsWith("2h-sword-") ||
                lowerCode.StartsWith("twohandedsword-") || lowerCode.StartsWith("twohanded-sword-") ||
                lowerCode.StartsWith("sword-great-") || // Combat Armory greatswords
                lowerCode.StartsWith("sword-long-") ||  // Combat Armory longswords
                lowerCode.StartsWith("longsword-") ||   // Standard longsword prefix
                lowerCode.StartsWith("aa-blade-claymore-") ||  // Ancient Armory claymore
                lowerCode.StartsWith("aa-blade-longsword-") || // Ancient Armory longsword
                (lowerCode.Contains("twohanded") && lowerCode.Contains("sword")) ||
                (lowerCode.Contains("2h") && lowerCode.Contains("sword")))
                return (CO_TWO_HANDED_SWORDS_PROFICIENCY, itemCode);

            // One-Handed Swords
            // Note: sword-long- and longsword- are handled above as two-handed
            // Ancient Armory: aa-blade-gladius, aa-blade-arming, aa-blade-sabre, aa-blade-falchion, aa-knife-*
            if (lowerCode.StartsWith("sword-") || lowerCode.StartsWith("blade-") ||
                lowerCode.StartsWith("shortsword-") || lowerCode.StartsWith("sword-short-") ||
                lowerCode.StartsWith("sword-arming-") || // Combat Armory arming swords
                lowerCode.StartsWith("saber-") || lowerCode.StartsWith("sabre-") || // Both spellings
                lowerCode.StartsWith("rapier-") || lowerCode.StartsWith("scimitar-") ||
                lowerCode.StartsWith("cutlass-") || lowerCode.StartsWith("falx-") ||
                lowerCode.StartsWith("falchion-") || lowerCode.StartsWith("dagger-") ||
                lowerCode.StartsWith("knife-") || lowerCode.StartsWith("kopis-") ||
                lowerCode.StartsWith("gladius-") || lowerCode.StartsWith("messer-") ||
                lowerCode.StartsWith("aa-blade-gladius-") ||   // Ancient Armory gladius
                lowerCode.StartsWith("aa-blade-arming-") ||    // Ancient Armory arming sword
                lowerCode.StartsWith("aa-blade-sabre-") ||     // Ancient Armory sabre
                lowerCode.StartsWith("aa-blade-falchion-") ||  // Ancient Armory falchion
                lowerCode.StartsWith("aa-knife-"))             // Ancient Armory knives (dagger, stiletto, khanjar, baselard)
                return (CO_ONE_HANDED_SWORDS_PROFICIENCY, itemCode);

            // Poleaxes: the CO fork tracks these under a separate poleaxeProficiency
            // stat. The original CO has no such stat, so there we fall back to
            // halberds, preserving the previous behavior.
            if (lowerCode.StartsWith("poleaxe-"))
                return (IsCombatOverhaulForkLoaded ? CO_POLEAXE_PROFICIENCY : CO_HALBERDS_PROFICIENCY, itemCode);

            // Halberds (polearms with axe heads)
            // Ancient Armory: aa-spear-voulge is a halberd-type weapon
            if (lowerCode.StartsWith("halberd-") ||
                lowerCode.StartsWith("glaive-") || lowerCode.StartsWith("bardiche-") ||
                lowerCode.StartsWith("voulge-") || lowerCode.StartsWith("guisarme-") ||
                lowerCode.StartsWith("aa-spear-voulge-"))  // Ancient Armory voulge
                return (CO_HALBERDS_PROFICIENCY, itemCode);

            // Quarterstaff
            if (lowerCode.StartsWith("quarterstaff-") || lowerCode.StartsWith("staff-") ||
                lowerCode.StartsWith("bo-") || lowerCode.Contains("bo-staff"))
                return (CO_QUARTERSTAFF_PROFICIENCY, itemCode);

            // Maces
            // Ancient Armory: aa-club-* (flanged, morningstar, spiked, warhammer)
            if (lowerCode.StartsWith("mace-") || lowerCode.StartsWith("morningstar-") ||
                lowerCode.StartsWith("flail-") || lowerCode.StartsWith("warhammer-") ||
                lowerCode.StartsWith("aa-club-"))  // Ancient Armory clubs/maces
                return (CO_MACES_PROFICIENCY, itemCode);

            // Clubs
            if (lowerCode.StartsWith("club-") || lowerCode.StartsWith("cudgel-") ||
                lowerCode.StartsWith("baton-") || lowerCode.StartsWith("truncheon-"))
                return (CO_CLUBS_PROFICIENCY, itemCode);

            // Axes (combat axes, not tool axes)
            // Ancient Armory: aa-axe-* (bearded, battle, bardiche)
            if (lowerCode.StartsWith("battleaxe-") || lowerCode.StartsWith("waraxe-") ||
                lowerCode.StartsWith("handaxe-") || lowerCode.StartsWith("hatchet-") ||
                lowerCode.StartsWith("aa-axe-") ||  // Ancient Armory battle axes
                (lowerCode.StartsWith("axe-") && !lowerCode.Contains("pickaxe")))
                return (CO_AXES_PROFICIENCY, itemCode);

            // Javelins (thrown spears)
            if (lowerCode.StartsWith("javelin-") || lowerCode.StartsWith("pilum-") ||
                lowerCode.StartsWith("throwing-spear-") || lowerCode.StartsWith("thrown-spear-") ||
                lowerCode.StartsWith("dart-") || lowerCode.StartsWith("plumbata-") ||
                lowerCode.Contains("javelin") || lowerCode.Contains("throwingspear") ||
                lowerCode.Contains("thrownspear") || lowerCode.Contains("throwing-spear"))
                return (CO_JAVELINS_PROFICIENCY, itemCode);

            // Spears (melee)
            // Ancient Armory: aa-spear-boar, aa-spear-fork, aa-spear-ranseur (but NOT voulge - that's a halberd)
            if (lowerCode.StartsWith("spear-") || lowerCode.StartsWith("pike-") ||
                lowerCode.StartsWith("lance-") || lowerCode.StartsWith("trident-") ||
                lowerCode.StartsWith("aa-spear-boar-") ||     // Ancient Armory boar spear
                lowerCode.StartsWith("aa-spear-fork-") ||     // Ancient Armory fork
                lowerCode.StartsWith("aa-spear-ranseur-"))    // Ancient Armory ranseur
                return (CO_SPEARS_PROFICIENCY, itemCode);

            // Bows (standard - after crossbows check)
            if (lowerCode.StartsWith("bow-") || lowerCode.StartsWith("longbow-") ||
                lowerCode.StartsWith("shortbow-") || lowerCode.StartsWith("recurvebow-") ||
                lowerCode.StartsWith("recurve-") || lowerCode.StartsWith("composite-bow"))
                return (CO_BOWS_PROFICIENCY, itemCode);

            // Slings
            if (lowerCode.StartsWith("sling-") || lowerCode == "sling")
                return (CO_SLINGS_PROFICIENCY, itemCode);

            // Debug logging for unmatched weapons - helps identify missing item codes
            if (DebugLoggingEnabled &&
                (lowerCode.Contains("sword") || lowerCode.Contains("blade") || lowerCode.Contains("spear") ||
                lowerCode.Contains("javelin") || lowerCode.Contains("axe") || lowerCode.Contains("mace") ||
                lowerCode.Contains("hammer") || lowerCode.Contains("club") || lowerCode.Contains("bow") ||
                lowerCode.Contains("staff") || lowerCode.Contains("halberd") || lowerCode.Contains("pike") ||
                lowerCode.Contains("sabre") || lowerCode.Contains("weapon") || lowerCode.Contains("dagger")))
            {
                ServerApi?.Logger?.Debug($"[SeraphLeveling] CO: Unmatched weapon item code: '{itemCode}' (normalized: '{lowerCode}')");
            }

            return (null, null);
        }

        /// <summary>
        /// Check if a proficiency stat is a ranged proficiency (contributes to Steady Aim).
        /// </summary>
        public static bool IsCORangedProficiency(string proficiencyStat)
        {
            return proficiencyStat == CO_BOWS_PROFICIENCY ||
                   proficiencyStat == CO_CROSSBOWS_PROFICIENCY ||
                   proficiencyStat == CO_FIREARMS_PROFICIENCY ||
                   proficiencyStat == CO_SLINGS_PROFICIENCY;
        }

        /// <summary>
        /// Process Combat Overhaul proficiency damage dealt by a player.
        /// Called from Harmony patch when CO is enabled.
        /// </summary>
        public static void ProcessCOProficiencyDamage(IServerPlayer attackerPlayer, string proficiencyStat, string weaponCode, float damage)
        {
            if (attackerPlayer?.Entity == null || string.IsNullOrEmpty(proficiencyStat) || string.IsNullOrEmpty(weaponCode)) return;

            // Skip if CO compat is disabled
            if (!IsCOCompatEnabled) return;

            string playerUid = attackerPlayer.PlayerUID;

            // Get or create player CO progress data
            var playerProgress = COProgress.GetOrAdd(playerUid, _ => new COPlayerProgressData());

            // Get max credits for this proficiency (player-aware, accounts for negative traits)
            int maxCredits = GetCOProficiencyMaxCreditsForPlayer(playerUid, proficiencyStat);

            // Get or create progress for this proficiency type
            var proficiencyProgress = playerProgress.GetProficiencyProgress(proficiencyStat);

            // Skip if already at max for this proficiency
            if (proficiencyProgress.TotalCredits >= maxCredits)
            {
                // Still process Steady Aim if this is ranged and steady aim isn't maxed
                if (IsCORangedProficiency(proficiencyStat))
                {
                    ProcessCOSteadyAimProgress(attackerPlayer, playerProgress, damage);
                }
                return;
            }

            // Get or create progress for this specific weapon (using per-proficiency base)
            int profBase = GetCOProficiencyBase(proficiencyStat);
            int profIncrement = GetCOProficiencyIncrement(proficiencyStat);
            var weaponProgress = proficiencyProgress.GetWeaponProgress(weaponCode, profBase);

            int oldCredits = proficiencyProgress.TotalCredits;

            // Add damage to this weapon's progress (apply sleep buff multiplier if active)
            float modifiedDamage = ApplyXPMultiplier(attackerPlayer.PlayerUID, damage);
            weaponProgress.DamageInIncrement += modifiedDamage;

            // Check if we've earned any new credits with this weapon
            while (weaponProgress.DamageInIncrement >= weaponProgress.CurrentIncrementSize && proficiencyProgress.TotalCredits < maxCredits)
            {
                // Earn a credit
                proficiencyProgress.TotalCredits++;
                weaponProgress.DamageInIncrement -= weaponProgress.CurrentIncrementSize;
                weaponProgress.CurrentIncrementSize += profIncrement;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {attackerPlayer.PlayerName} earned CO {proficiencyStat} credit {proficiencyProgress.TotalCredits} with {weaponCode}");
            }

            pendingCOProgressSave = true;

            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "coproficiency");

            // If credits increased, update the stat and notify player
            if (proficiencyProgress.TotalCredits > oldCredits)
            {
                ApplyCOProficiencyBonus(attackerPlayer, proficiencyStat, proficiencyProgress.TotalCredits);

                // Update negative trait remaining display based on all proficiencies
                UpdateCONegativeTraitRemaining(attackerPlayer);

                // Notify player of level up
                float bonus = CalculateCOProficiencyBonus(proficiencyProgress.TotalCredits, GetCOProficiencyMax(proficiencyStat));
                attackerPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                    $"Proficiency level up! Your {GetCOProficiencyDisplayName(proficiencyStat)} is now +{bonus * 100:F0}%.",
                    EnumChatType.Notification);
            }

            // Also process Steady Aim if this is a ranged proficiency
            if (IsCORangedProficiency(proficiencyStat))
            {
                ProcessCOSteadyAimProgress(attackerPlayer, playerProgress, damage);
            }
        }

        /// <summary>
        /// Process Steady Aim progression (shared with ranged proficiencies).
        /// </summary>
        private static void ProcessCOSteadyAimProgress(IServerPlayer player, COPlayerProgressData playerProgress, float damage)
        {
            // Use player-aware max that accounts for Trembling Aim
            int maxSteadyAimCredits = GetCOSteadyAimMaxCreditsForPlayer(player.PlayerUID);

            // Skip if already at max
            if (playerProgress.SteadyAimCredits >= maxSteadyAimCredits) return;

            // Use a simple progression for Steady Aim (not per-weapon)
            // We'll use SteadyAimCredits to track total credits earned
            // For simplicity, every ranged damage point adds to a shared progress counter
            // We'll track damage in a special "steadyaim" key in the bows proficiency
            int steadyAimBase = GetCOProficiencyBase(CO_STEADY_AIM);
            int steadyAimIncrement = GetCOProficiencyIncrement(CO_STEADY_AIM);
            var steadyAimProgress = playerProgress.GetProficiencyProgress(CO_STEADY_AIM);
            var sharedProgress = steadyAimProgress.GetWeaponProgress("_ranged_combined", steadyAimBase);

            int oldCredits = playerProgress.SteadyAimCredits;

            // Apply sleep buff multiplier if active
            float modifiedDamage = ApplyXPMultiplier(player.PlayerUID, damage);
            sharedProgress.DamageInIncrement += modifiedDamage;

            while (sharedProgress.DamageInIncrement >= sharedProgress.CurrentIncrementSize && playerProgress.SteadyAimCredits < maxSteadyAimCredits)
            {
                playerProgress.SteadyAimCredits++;
                sharedProgress.DamageInIncrement -= sharedProgress.CurrentIncrementSize;
                sharedProgress.CurrentIncrementSize += steadyAimIncrement;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned CO Steady Aim credit {playerProgress.SteadyAimCredits}");
            }

            if (playerProgress.SteadyAimCredits > oldCredits)
            {
                ApplyCOSteadyAimBonus(player, playerProgress.SteadyAimCredits);

                float bonus = CalculateCOProficiencyBonus(playerProgress.SteadyAimCredits, COSteadyAimMax);
                player.SendMessage(GlobalConstants.GeneralChatGroup,
                    $"Steady Aim improved! Your aim stability is now +{bonus * 100:F0}%.",
                    EnumChatType.Notification);
            }
        }

        /// <summary>
        /// Apply a Combat Overhaul proficiency bonus to a player.
        /// Delegates to ApplyCOProficiencyBonusWithCancellation for negative trait handling.
        /// </summary>
        private static void ApplyCOProficiencyBonus(IServerPlayer player, string proficiencyStat, int credits)
        {
            ApplyCOProficiencyBonusWithCancellation(player, proficiencyStat, credits);
        }

        /// <summary>
        /// Apply Combat Overhaul Steady Aim bonus to a player.
        /// Handles Trembling Aim negative trait cancellation.
        /// </summary>
        private static void ApplyCOSteadyAimBonus(IServerPlayer player, int credits)
        {
            if (player?.Entity == null) return;

            // Check for Trembling Aim negative trait
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasTremblingAim = cache?.HasCOTremblingAim ?? false;

            // Calculate remaining penalty and net bonus
            float tremblingAimRemaining = 0f;
            float netBonus = 0f;

            if (hasTremblingAim)
            {
                // Trembling Aim penalty is 0.3, cancelled by Steady Aim credits (30 credits to cancel)
                int creditsToCancel = (int)(CO_TREMBLING_AIM_PENALTY * 100); // 30
                tremblingAimRemaining = Math.Max(0, CO_TREMBLING_AIM_PENALTY - credits * 0.01f);
                netBonus = CalculateCOProficiencyBonus(Math.Max(0, credits - creditsToCancel), COSteadyAimMax);
            }
            else
            {
                netBonus = CalculateCOProficiencyBonus(credits, COSteadyAimMax);
            }

            // Trembling Aim's penalty is already in this stat under the code "trait", and
            // stat values sum, so withholding our own bonus for the first 30 credits never
            // removed it. Those credits bought nothing. Cancel it explicitly instead: add
            // back the part of the penalty the player has paid off, and keep our earned
            // bonus on top. A class perk on steadyAim is positive and is left alone.
            float steadyAimTraitPenalty = TraitStatPenalty(player.Entity, CO_STEADY_AIM);
            float steadyAimRemaining = Math.Max(0f, -steadyAimTraitPenalty - credits * 0.01f);
            string statCode = CO_STAT_PREFIX + CO_STEADY_AIM;
            player.Entity.Stats.Set(CO_STEADY_AIM, statCode, netBonus - steadyAimRemaining - steadyAimTraitPenalty, false);

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_CO_STEADY_AIM_CREDITS, credits);
            player.Entity.WatchedAttributes.SetFloat(WATCHED_CO_TREMBLING_AIM_REMAINING, tremblingAimRemaining);
            player.Entity.WatchedAttributes.SetBool(WATCHED_CO_HAS_TREMBLING_AIM, hasTremblingAim);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_STEADY_AIM_CREDITS);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_TREMBLING_AIM_REMAINING);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_HAS_TREMBLING_AIM);
        }

        /// <summary>
        /// Apply a Combat Overhaul proficiency bonus to a player.
        /// Handles negative trait cancellation for various proficiencies.
        /// </summary>
        private static void ApplyCOProficiencyBonusWithCancellation(IServerPlayer player, string proficiencyStat, int credits)
        {
            if (player?.Entity == null) return;

            var cache = GetCachedTraits(player.PlayerUID);
            bool hasClumsyHands = cache?.HasCOClumsyHands ?? false;
            bool hasWeakHand = cache?.HasCOWeakHand ?? false;
            bool hasFearOfMelee = cache?.HasCOFearOfMelee ?? false;
            bool hasNervous = cache?.HasCONervous ?? false;

            float maxBonus = GetCOProficiencyMax(proficiencyStat);
            float netBonus = 0f;
            bool isRanged = IsCORangedProficiency(proficiencyStat);
            bool isPiercing = IsCOPiercingProficiency(proficiencyStat);

            // Handle Clumsy Hands for ranged proficiencies (bows, crossbows, firearms)
            if (hasClumsyHands && isRanged)
            {
                // Clumsy Hands gives -0.3 to bows, crossbows, firearms (30 credits to cancel each)
                int creditsToCancel = (int)(CO_CLUMSY_HANDS_PENALTY * 100); // 30
                netBonus = CalculateCOProficiencyBonus(Math.Max(0, credits - creditsToCancel), maxBonus);
                // Note: Remaining penalty UI is updated by UpdateCONegativeTraitRemaining() after all proficiencies are applied
            }
            // Handle Weak Hand for ranged proficiencies (similar to Clumsy Hands)
            else if (hasWeakHand && isRanged)
            {
                // Weak Hand gives -0.3 to ranged proficiencies (30 credits to cancel each)
                int creditsToCancel = (int)(CO_WEAK_HAND_PENALTY * 100); // 30
                netBonus = CalculateCOProficiencyBonus(Math.Max(0, credits - creditsToCancel), maxBonus);
                // Note: Remaining penalty UI is updated by UpdateCONegativeTraitRemaining() after all proficiencies are applied
            }
            // Handle Fear of Melee for melee proficiencies (tier-based)
            else if (hasFearOfMelee && !isRanged)
            {
                // Fear of Melee gives -1 slashing damage tier
                // This needs 100 credits to cancel (1 tier = 100 credits in our system)
                int creditsToCancel = CO_FEAR_OF_MELEE_TIER_PENALTY * 100; // 100
                netBonus = CalculateCOProficiencyBonus(Math.Max(0, credits - creditsToCancel), maxBonus);
                // Note: Remaining penalty UI is updated by UpdateCONegativeTraitRemaining() after all proficiencies are applied
            }
            // Handle Nervous for piercing melee (spears, javelins) - tier-based
            else if (hasNervous && isPiercing)
            {
                // Nervous gives -1 damage tier for piercing melee
                // This needs 100 credits to cancel (1 tier = 100 credits in our system)
                int creditsToCancel = CO_NERVOUS_TIER_PENALTY * 100; // 100
                netBonus = CalculateCOProficiencyBonus(Math.Max(0, credits - creditsToCancel), maxBonus);
                // Note: Remaining penalty UI is updated by UpdateCONegativeTraitRemaining() after all proficiencies are applied
            }
            else
            {
                netBonus = CalculateCOProficiencyBonus(credits, maxBonus);
            }

            // A trait penalty on this proficiency, Clumsy Hands being the one Combat Overhaul
            // ships, is already in the stat under the code "trait", and stat values sum, so
            // withholding our own bonus above never removed it. Cancel it explicitly: add back
            // the part the player has paid off, then stack our earned bonus on top. Reading
            // the applied value means this covers whichever proficiencies the penalty really
            // touches, rather than every stat IsCORangedProficiency happens to return true for.
            // A class perk on a proficiency is positive and is left alone to stack.
            float profTraitPenalty = TraitStatPenalty(player.Entity, proficiencyStat);
            float profPenaltyRemaining = Math.Max(0f, -profTraitPenalty - credits * 0.01f);

            // Apply stat using CO stat name with our prefix
            string statCode = CO_STAT_PREFIX + proficiencyStat;
            player.Entity.Stats.Set(proficiencyStat, statCode, netBonus - profPenaltyRemaining - profTraitPenalty, false);

            // Sync credits to WatchedAttributes
            string watchedKey = $"sitCO{proficiencyStat}Credits";
            player.Entity.WatchedAttributes.SetInt(watchedKey, credits);
            player.Entity.WatchedAttributes.MarkPathDirty(watchedKey);
        }

        /// <summary>
        /// Update the remaining penalty display for CO negative traits.
        /// This calculates the MAXIMUM remaining penalty across all affected proficiencies
        /// to show the worst-case remaining penalty in the UI.
        /// Should be called after all proficiency bonuses are applied.
        /// </summary>
        private static void UpdateCONegativeTraitRemaining(IServerPlayer player)
        {
            if (!IsCOCompatEnabled || player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var cache = GetCachedTraits(playerUid);
            if (cache == null) return;

            if (!COProgress.TryGetValue(playerUid, out var playerProgress)) return;

            // Clumsy Hands: affects bows, crossbows, firearms, slings (all ranged)
            if (cache.HasCOClumsyHands)
            {
                float maxRemaining = CO_CLUMSY_HANDS_PENALTY; // Start with full penalty
                var rangedProficiencies = new[] { CO_BOWS_PROFICIENCY, CO_CROSSBOWS_PROFICIENCY, CO_FIREARMS_PROFICIENCY, CO_SLINGS_PROFICIENCY };

                foreach (var prof in rangedProficiencies)
                {
                    if (playerProgress.Proficiencies.TryGetValue(prof, out var profData))
                    {
                        float remaining = Math.Max(0, CO_CLUMSY_HANDS_PENALTY - profData.TotalCredits * 0.01f);
                        maxRemaining = Math.Max(maxRemaining, remaining);
                    }
                }

                // Find the minimum remaining (most progress made on any proficiency)
                float minRemaining = CO_CLUMSY_HANDS_PENALTY;
                foreach (var prof in rangedProficiencies)
                {
                    if (playerProgress.Proficiencies.TryGetValue(prof, out var profData))
                    {
                        float remaining = Math.Max(0, CO_CLUMSY_HANDS_PENALTY - profData.TotalCredits * 0.01f);
                        minRemaining = Math.Min(minRemaining, remaining);
                    }
                }

                player.Entity.WatchedAttributes.SetFloat(WATCHED_CO_CLUMSY_HANDS_REMAINING, minRemaining);
                player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_CLUMSY_HANDS_REMAINING);
            }

            // Weak Hand: similar to Clumsy Hands, affects ranged proficiencies
            if (cache.HasCOWeakHand)
            {
                var rangedProficiencies = new[] { CO_BOWS_PROFICIENCY, CO_CROSSBOWS_PROFICIENCY, CO_FIREARMS_PROFICIENCY, CO_SLINGS_PROFICIENCY };

                float minRemaining = CO_WEAK_HAND_PENALTY;
                foreach (var prof in rangedProficiencies)
                {
                    if (playerProgress.Proficiencies.TryGetValue(prof, out var profData))
                    {
                        float remaining = Math.Max(0, CO_WEAK_HAND_PENALTY - profData.TotalCredits * 0.01f);
                        minRemaining = Math.Min(minRemaining, remaining);
                    }
                }

                player.Entity.WatchedAttributes.SetFloat(WATCHED_CO_WEAK_HAND_REMAINING, minRemaining);
                player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_WEAK_HAND_REMAINING);
            }

            // Fear of Melee: affects all melee proficiencies (tier-based)
            if (cache.HasCOFearOfMelee)
            {
                var meleeProficiencies = new[] {
                    CO_ONE_HANDED_SWORDS_PROFICIENCY, CO_TWO_HANDED_SWORDS_PROFICIENCY,
                    CO_SPEARS_PROFICIENCY, CO_JAVELINS_PROFICIENCY, CO_MACES_PROFICIENCY,
                    CO_CLUBS_PROFICIENCY, CO_HALBERDS_PROFICIENCY, CO_POLEAXE_PROFICIENCY, CO_AXES_PROFICIENCY,
                    CO_QUARTERSTAFF_PROFICIENCY
                };

                int minRemainingTiers = CO_FEAR_OF_MELEE_TIER_PENALTY;
                foreach (var prof in meleeProficiencies)
                {
                    if (playerProgress.Proficiencies.TryGetValue(prof, out var profData))
                    {
                        int remainingTiers = Math.Max(0, CO_FEAR_OF_MELEE_TIER_PENALTY - profData.TotalCredits / 100);
                        minRemainingTiers = Math.Min(minRemainingTiers, remainingTiers);
                    }
                }

                player.Entity.WatchedAttributes.SetInt(WATCHED_CO_FEAR_OF_MELEE_REMAINING, minRemainingTiers);
                player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_FEAR_OF_MELEE_REMAINING);
            }

            // Nervous: affects piercing melee (spears, javelins) - tier-based
            if (cache.HasCONervous)
            {
                var piercingProficiencies = new[] { CO_SPEARS_PROFICIENCY, CO_JAVELINS_PROFICIENCY };

                int minRemainingTiers = CO_NERVOUS_TIER_PENALTY;
                foreach (var prof in piercingProficiencies)
                {
                    if (playerProgress.Proficiencies.TryGetValue(prof, out var profData))
                    {
                        int remainingTiers = Math.Max(0, CO_NERVOUS_TIER_PENALTY - profData.TotalCredits / 100);
                        minRemainingTiers = Math.Min(minRemainingTiers, remainingTiers);
                    }
                }

                player.Entity.WatchedAttributes.SetInt(WATCHED_CO_NERVOUS_REMAINING, minRemainingTiers);
                player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_NERVOUS_REMAINING);
            }
        }


        /// <summary>
        /// Check if a proficiency is a piercing melee proficiency (affected by Nervous trait).
        /// </summary>
        private static bool IsCOPiercingProficiency(string proficiencyStat)
        {
            return proficiencyStat == CO_SPEARS_PROFICIENCY || proficiencyStat == CO_JAVELINS_PROFICIENCY;
        }

        /// <summary>
        /// Get a display-friendly name for a CO proficiency stat.
        /// </summary>
        public static string GetCOProficiencyDisplayName(string proficiencyStat)
        {
            switch (proficiencyStat)
            {
                case CO_BOWS_PROFICIENCY: return "Bows Proficiency";
                case CO_CROSSBOWS_PROFICIENCY: return "Crossbows Proficiency";
                case CO_FIREARMS_PROFICIENCY: return "Firearms Proficiency";
                case CO_SLINGS_PROFICIENCY: return "Slings Proficiency";
                case CO_ONE_HANDED_SWORDS_PROFICIENCY: return "One-Handed Swords Proficiency";
                case CO_TWO_HANDED_SWORDS_PROFICIENCY: return "Two-Handed Swords Proficiency";
                case CO_SPEARS_PROFICIENCY: return "Spears Proficiency";
                case CO_JAVELINS_PROFICIENCY: return "Javelins Proficiency";
                case CO_MACES_PROFICIENCY: return "Maces Proficiency";
                case CO_CLUBS_PROFICIENCY: return "Clubs Proficiency";
                case CO_HALBERDS_PROFICIENCY: return "Halberds Proficiency";
                case CO_POLEAXE_PROFICIENCY: return "Poleaxe Proficiency";
                case CO_AXES_PROFICIENCY: return "Axes Proficiency";
                case CO_QUARTERSTAFF_PROFICIENCY: return "Quarterstaff Proficiency";
                case CO_STEADY_AIM: return "Steady Aim";
                default: return proficiencyStat;
            }
        }

        /// <summary>
        /// Apply all CO bonuses for a player (called on join/reconnect).
        /// </summary>
        public static void ApplyAllCOBonuses(IServerPlayer player)
        {
            if (!IsCOCompatEnabled || player?.Entity == null) return;

            string playerUid = player.PlayerUID;

            // Initialize CO negative trait WatchedAttributes even if no progress exists yet.
            // This ensures the UI shows the correct penalties for new players.
            InitializeCONegativeTraitDefaults(player);

            if (!COProgress.TryGetValue(playerUid, out var playerProgress)) return;

            // Apply each proficiency bonus
            foreach (var proficiency in playerProgress.Proficiencies)
            {
                if (proficiency.Key != CO_STEADY_AIM)
                {
                    ApplyCOProficiencyBonus(player, proficiency.Key, proficiency.Value.TotalCredits);
                }
            }

            // Apply Steady Aim bonus
            if (playerProgress.SteadyAimCredits > 0)
            {
                ApplyCOSteadyAimBonus(player, playerProgress.SteadyAimCredits);
            }

            // Update negative trait remaining display based on all proficiencies
            UpdateCONegativeTraitRemaining(player);
        }

        /// <summary>
        /// Initialize CO negative trait WatchedAttributes to their full penalty values
        /// for players who have those traits but no CO progress yet.
        /// This prevents the UI from showing 0 remaining (which hides the penalties).
        /// </summary>
        private static void InitializeCONegativeTraitDefaults(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            var cache = GetCachedTraits(player.PlayerUID);
            if (cache == null) return;

            var watchedAttrs = player.Entity.WatchedAttributes;

            if (cache.HasCOTremblingAim)
            {
                // Only set defaults if not already initialized (avoids overwriting earned progress)
                if (watchedAttrs.GetFloat(WATCHED_CO_TREMBLING_AIM_REMAINING, -1f) < 0)
                {
                    watchedAttrs.SetFloat(WATCHED_CO_TREMBLING_AIM_REMAINING, 1.0f);
                    watchedAttrs.SetBool(WATCHED_CO_HAS_TREMBLING_AIM, true);
                    watchedAttrs.MarkPathDirty(WATCHED_CO_TREMBLING_AIM_REMAINING);
                }
            }

            if (cache.HasCOClumsyHands)
            {
                if (watchedAttrs.GetFloat(WATCHED_CO_CLUMSY_HANDS_REMAINING, -1f) < 0)
                {
                    watchedAttrs.SetFloat(WATCHED_CO_CLUMSY_HANDS_REMAINING, CO_CLUMSY_HANDS_PENALTY);
                    watchedAttrs.MarkPathDirty(WATCHED_CO_CLUMSY_HANDS_REMAINING);
                }
            }

            if (cache.HasCOWeakHand)
            {
                if (watchedAttrs.GetFloat(WATCHED_CO_WEAK_HAND_REMAINING, -1f) < 0)
                {
                    watchedAttrs.SetFloat(WATCHED_CO_WEAK_HAND_REMAINING, CO_WEAK_HAND_PENALTY);
                    watchedAttrs.MarkPathDirty(WATCHED_CO_WEAK_HAND_REMAINING);
                }
            }

            if (cache.HasCOFearOfMelee)
            {
                if (watchedAttrs.GetInt(WATCHED_CO_FEAR_OF_MELEE_REMAINING, -1) < 0)
                {
                    watchedAttrs.SetInt(WATCHED_CO_FEAR_OF_MELEE_REMAINING, CO_FEAR_OF_MELEE_TIER_PENALTY);
                    watchedAttrs.MarkPathDirty(WATCHED_CO_FEAR_OF_MELEE_REMAINING);
                }
            }

            if (cache.HasCONervous)
            {
                if (watchedAttrs.GetInt(WATCHED_CO_NERVOUS_REMAINING, -1) < 0)
                {
                    watchedAttrs.SetInt(WATCHED_CO_NERVOUS_REMAINING, CO_NERVOUS_TIER_PENALTY);
                    watchedAttrs.MarkPathDirty(WATCHED_CO_NERVOUS_REMAINING);
                }
            }
        }
        /// <summary>
        /// Persist the current settings. Everything now lives in
        /// ModConfig/SeraphLeveling.json, so this just writes that file.
        ///
        /// Settings used to be written into the world save under CONFIG_SAVE_KEY
        /// instead, which had two problems. The blob was reloaded on every
        /// SaveGameLoaded and overwrote whatever the admin had put in the config
        /// file, so edits to the file appeared to do nothing on any world that had
        /// ever run a /trait command. And the blob only ever stored the mining,
        /// melee, ranged, walking, hunger, armor and Combat Overhaul values, so
        /// in-game changes to the other systems, Clothier and Mender and the decay
        /// settings among them, were silently dropped on restart.
        ///
        /// LoadConfig migrates any surviving blob into the file and then erases it.
        /// </summary>
        private void PersistConfig()
        {
            if (ServerApi == null) return;

            SaveConfigFile();
            ServerApi.Logger.Debug($"[SeraphLeveling] Config saved to ModConfig/{CONFIG_FILE_NAME} (Mining: Base={AttributeModifierDefinitions.MiningSpeed.BaseIncrement}, Max={AttributeModifierDefinitions.MiningSpeed.GlobalMaxCredits}% | Melee: Base={BaseDamagePerIncrement}, Max={MaxMeleeDamagePercent}% | CO: {COProficiencyBaseOverrides.Count} base overrides, {COProficiencyIncrementOverrides.Count} increment overrides)");
        }

        /// <summary>
        /// One-time migration of the old world-save config blob into
        /// ModConfig/SeraphLeveling.json. Runs on SaveGameLoaded, after
        /// LoadConfigFile has already applied the file.
        ///
        /// Worlds saved by 1.18.1 and earlier carry a binary snapshot of the
        /// tuning values under CONFIG_SAVE_KEY, and that snapshot used to win over
        /// the config file on every load. Read it one last time so the world keeps
        /// playing exactly as it did, write those values into the file, then erase
        /// the blob. From the next load on there is nothing here to read and the
        /// file alone decides. Supports blob versions 1-10.
        /// </summary>
        private void LoadConfig()
        {
            if (ServerApi == null) return;

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(CONFIG_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No legacy world config to migrate, using ModConfig/" + CONFIG_FILE_NAME);
                    return;
                }

                if (LoadedConfigVersion >= CURRENT_CONFIG_VERSION)
                {
                    // Already migrated on an earlier run. The erase below only reaches
                    // disk on the next world save, so a server that was killed before
                    // saving still has the blob sitting there. Reapplying it would undo
                    // whatever the admin has since put in the file, so just clear it.
                    ServerApi.WorldManager.SaveGame.StoreData(CONFIG_SAVE_KEY, Array.Empty<byte>());
                    ServerApi.Logger.Debug("[SeraphLeveling] Discarded a stale legacy world config; ModConfig/" + CONFIG_FILE_NAME + " already owns these settings");
                    return;
                }

                SaveConfigFile();
                ServerApi.WorldManager.SaveGame.StoreData(CONFIG_SAVE_KEY, Array.Empty<byte>());
                pendingConfigSave = false;
                ServerApi.Logger.Notification(
                    $"[SeraphLeveling] Migrated this world's saved settings into ModConfig/{CONFIG_FILE_NAME}. " +
                    "That file is now the only place settings are read from, so edits to it take effect on the next restart, or immediately with /trait reloadconfig.");
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load config: {ex.Message}");
            }
        }

        // =========================================================================
        // CLOTHIER TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Tick handler for clothing tracking.
        /// </summary>
        private void OnClothingTick(float dt)
        {
            if (ServerApi == null) return;

            // Skip clothier progression if disabled
            if (IsAttributeModifierDisabled(AttributeModifierDefinitions.Clothier)) return;

            foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers.Cast<IServerPlayer>())
            {
                if (player?.Entity == null) continue;
                if (!player.Entity.Alive) continue;

                // Skip if already unlocked
                if (AttributeModifierDefinitions.Clothier.IsUnlockedForPlayer(player)) continue;

                // Get the player's currently equipped clothing using character inventory
                var characterInventory = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
                if (characterInventory != null)
                {
                    characterInventory
                        .Where(slot => slot?.Itemstack?.Collectible != null)
                        .Select(slot => slot.Itemstack.Collectible.Code?.ToString())
                        .Foreach(itemCode => AttributeModifierDefinitions.Clothier.AddCollectedItem(player, itemCode));
                }
            }
        }

        /// <summary>
        /// Check if an item code represents clothing (not armor) and is not blacklisted.
        /// Starting class outfits are blacklisted by default to prevent easy Clothier progression.
        /// </summary>
        public static bool IsClothingItem(string itemCode)
        {
            return AttributeModifierDefinitions.Clothier.IsItemValid(itemCode);
        }

        /// <summary>
        /// Tick handler for Mender repair detection.
        /// Uses two detection methods:
        /// 1. Tracks sewing kit consumption from mouse cursor (most reliable)
        /// 2. Tracks durability increases on wearable items (backup method)
        /// </summary>
        private void OnMenderRepairTick(float dt)
        {
            if (ServerApi == null) return;

            foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;
                if (!player.Entity.Alive) continue;

                string playerUid = player.PlayerUID;

                // =============================================
                // METHOD 1: Track sewing kit consumption from mouse cursor
                // When player holds sewing kits and clicks on clothing, the count decreases
                // =============================================
                var mouseSlot = player.InventoryManager?.MouseItemSlot;
                if (mouseSlot?.Itemstack?.Collectible != null)
                {
                    string mouseItemCode = mouseSlot.Itemstack.Collectible.Code?.ToString()?.ToLowerInvariant() ?? "";

                    if (mouseItemCode.Contains("sewingkit"))
                    {
                        int currentCount = mouseSlot.Itemstack.StackSize;

                        if (TrackedSewingKitCounts.TryGetValue(playerUid, out int previousCount))
                        {
                            if (currentCount < previousCount)
                            {
                                // Sewing kit was consumed - repair happened!
                                int kitsUsed = previousCount - currentCount;
                                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} used {kitsUsed} sewing kit(s) for repair");

                                for (int i = 0; i < kitsUsed; i++)
                                {
                                    ProcessMenderRepair(player);
                                }
                            }
                        }

                        // Update tracked count
                        TrackedSewingKitCounts[playerUid] = currentCount;
                    }
                    else
                    {
                        // Not holding sewing kit anymore, clear tracking
                        TrackedSewingKitCounts.TryRemove(playerUid, out _);
                    }
                }
                else
                {
                    // Mouse slot empty, clear tracking
                    TrackedSewingKitCounts.TryRemove(playerUid, out _);
                }

                // =============================================
                // METHOD 2: Track durability increases on wearable items (backup)
                // =============================================
                var characterInventory = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
                if (characterInventory == null) continue;

                int slotIndex = 0;
                foreach (var slot in characterInventory)
                {
                    slotIndex++;
                    if (slot?.Itemstack?.Collectible == null) continue;

                    string itemCode = slot.Itemstack.Collectible.Code?.ToString();
                    if (string.IsNullOrEmpty(itemCode)) continue;

                    // Only track clothing and armor
                    if (!IsClothingItem(itemCode) && !IsArmorItem(itemCode)) continue;

                    // Get current durability
                    int currentDurability = slot.Itemstack.Collectible.GetRemainingDurability(slot.Itemstack);
                    int maxDurability = slot.Itemstack.Collectible.GetMaxDurability(slot.Itemstack);

                    // Skip items without durability
                    if (maxDurability <= 0) continue;

                    // Create a tracking key for this item in this slot
                    string trackingKey = $"{playerUid}_{slotIndex}_{itemCode}";

                    // Check if durability increased (repair happened)
                    if (TrackedItemDurabilities.TryGetValue(trackingKey, out int previousDurability))
                    {
                        if (currentDurability > previousDurability)
                        {
                            // Durability increased - a repair happened!
                            int durabilityRestored = currentDurability - previousDurability;
                            int repairPercent = (durabilityRestored * 100) / maxDurability;

                            // Only credit significant repairs (at least 5% durability restored)
                            // This filters out minor fluctuations and avoids double-counting with method 1
                            // Use a higher threshold since method 1 should catch most sewing kit repairs
                            if (repairPercent >= 10)
                            {
                                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} repaired {itemCode} (+{repairPercent}% durability) via durability tracking");
                                ProcessMenderRepair(player);
                            }
                        }
                    }

                    // Update tracked durability
                    TrackedItemDurabilities[trackingKey] = currentDurability;
                }
            }
        }

        /// <summary>
        /// Check if an item code represents armor.
        /// </summary>
        private static bool IsArmorItem(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return false;
            string lowerCode = itemCode.ToLowerInvariant();
            return lowerCode.Contains("armor-");
        }

        // =========================================================================
        // FURTIVE TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Called every 500ms to track sneaking distance for all online players.
        /// Calculates 2D horizontal distance moved while sneaking (ignoring Y-axis).
        /// </summary>
        private void OnSneakingTick(float dt)
        {
            foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;

                string playerUid = player.PlayerUID;

                // Check if player is sneaking
                bool isSneaking = player.Entity.Controls?.Sneak ?? false;

                if (!isSneaking)
                {
                    // Not sneaking, clear last position so movement doesn't count
                    lastSneakingPositions.TryRemove(playerUid, out _);
                    continue;
                }

                double currentX = player.Entity.Pos.X;
                double currentZ = player.Entity.Pos.Z;

                // Get or initialize last sneaking position (using Position2D struct to avoid Vec3d allocations)
                if (!lastSneakingPositions.TryGetValue(playerUid, out Position2D lastPos))
                {
                    lastSneakingPositions[playerUid] = new Position2D(currentX, currentZ);
                    continue;
                }

                // Calculate 2D horizontal distance (ignore Y axis to avoid counting climbing/falling)
                double dx = currentX - lastPos.X;
                double dz = currentZ - lastPos.Z;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz);

                // Update last position (no allocation - struct assignment)
                lastSneakingPositions[playerUid] = new Position2D(currentX, currentZ);

                // Skip if no movement or teleportation (too far)
                if (distance < 0.01f || distance > MAX_DISTANCE_PER_TICK) continue;

                var playerProgress = AttributeModifierDefinitions.Furtive.GetForPlayer(playerUid);
                playerProgress.DoEvent(player, distance);
            }
        }


        // =========================================================================
        // PRECISE TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Check if an entity is a mechanical creature (e.g., locust, bell, etc.).
        /// </summary>
        public static bool IsMechanicalCreature(Entity entity)
        {
            if (entity == null) return false;

            string entityCode = entity.Code?.ToString()?.ToLowerInvariant() ?? "";

            // Check for known mechanical creatures
            // Locusts are the main mechanical enemies in Vintage Story
            if (entityCode.Contains("locust")) return true;
            if (entityCode.Contains("bell")) return true;
            if (entityCode.Contains("mechanical")) return true;
            if (entityCode.Contains("automaton")) return true;
            if (entityCode.Contains("construct")) return true;

            // Also check the entity class
            string entityClass = entity.GetType().Name.ToLowerInvariant();
            if (entityClass.Contains("locust")) return true;

            return false;
        }

        /// <summary>
        /// Process damage dealt to a mechanical creature by a player.
        /// Adds progress toward the Precise trait.
        /// </summary>
        public static void ProcessPreciseDamage(IServerPlayer attackerPlayer, string weaponType, float damage)
        {
            if (attackerPlayer?.Entity == null || damage <= 0) return;
            if (string.IsNullOrEmpty(weaponType)) return;

            string playerUid = attackerPlayer.PlayerUID;

            var damageProgress = AttributeModifierDefinitions.Precise.GetForPlayer(playerUid);
            damageProgress.DoEvent(attackerPlayer, weaponType, damage);
        }


        /// <summary>
        /// Process a sewing kit repair (called externally or via Harmony patch).
        /// </summary>
        public static void ProcessMenderRepair(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            AttributeModifierDefinitions.Mender.GetForPlayer(playerUid).DoEvent(player, 1);
        }

        public static void ProcessPoulticeHeal(IServerPlayer player, string poulticeType)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            AttributeModifierDefinitions.HealUseSpeed.GetForPlayer(playerUid).DoEvent(player, poulticeType, 1);
        }

        /// <summary>
        /// Process cracked vessel break (called from OnBlockBroken for cracked vessels).
        /// Only cracked vessels count - they can't be re-placed by players.
        /// </summary>
        public static void ProcessVesselBreak(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;

            AttributeModifierDefinitions.GearDropRate.GetForPlayer(playerUid).DoEvent(player, PILFERER_VESSEL_POINTS);
            AttributeModifierDefinitions.VesselDropRate.GetForPlayer(playerUid).DoEvent(player, PILFERER_VESSEL_POINTS);
            AttributeModifierDefinitions.WholeVesselRate.GetForPlayer(playerUid).DoEvent(player, PILFERER_VESSEL_POINTS);
        }

        private static void ProcessCharcoalBreak(IServerPlayer player, int pointValue)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;

            AttributeModifierDefinitions.CharcoalDropRate.GetForPlayer(playerUid).DoEvent(player, pointValue);
        }

        /// <summary>
        /// Process animal harvested (called from Harmony patch when player harvests an animal).
        /// </summary>
        public static void ProcessAnimalHarvested(IServerPlayer player)
        {
            if (player?.Entity == null) return;
            string playerUid = player.PlayerUID;

            AttributeModifierDefinitions.AnimalDropRate.GetForPlayer(playerUid).DoEvent(player, 1);
            AttributeModifierDefinitions.AnimalHarvestRate.GetForPlayer(playerUid).DoEvent(player, 1);
        }

        /// <summary>
        /// Process wild crop broken (for Forager progression).
        /// </summary>
        public static void ProcessWildCropBroken(IServerPlayer player)
        {
            if (player?.Entity == null) return;
            string playerUid = player.PlayerUID;

            AttributeModifierDefinitions.ForageLootingBonus.GetForPlayer(playerUid).DoEvent(player, 1);
            AttributeModifierDefinitions.WildCropDropRate.GetForPlayer(playerUid).DoEvent(player, 1);
        }

        /// <summary>
        /// Check if a block is a wild crop (for Forager progression).
        /// Wild crops are crops like turnip, flax, spelt that grow on dirt/soil (not farmland).
        /// Berry bushes are NOT counted since they can be replanted infinitely.
        /// </summary>
        private static bool IsWildCropBlock(int blockId, BlockPos blockPos)
        {
            if (ServerApi == null) return false;

            Block block = ServerApi.World.GetBlock(blockId);
            if (block == null) return false;

            string blockCode = block.Code?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(blockCode)) return false;

            // Check if it's a crop block (like crop-turnip-4, crop-flax-7, etc.)
            if (blockCode.Contains("crop-"))
            {
                // Skip if it's explicitly a "wild" block - those are already wild
                // Regular crops on farmland should NOT count
                // Wild crops spawn on dirt/soil naturally

                // Check if the block below is farmland - if so, this is a planted crop, not wild
                if (blockPos != null)
                {
                    BlockPos belowPos = blockPos.DownCopy();
                    Block blockBelow = ServerApi.World.BlockAccessor.GetBlock(belowPos);
                    string belowCode = blockBelow?.Code?.ToString()?.ToLowerInvariant() ?? "";

                    // If on farmland, this is a cultivated crop - don't count it
                    if (belowCode.Contains("farmland"))
                    {
                        return false;
                    }

                    // If on dirt, soil, grass, or other natural blocks - this is a wild crop
                    if (belowCode.Contains("soil") || belowCode.Contains("dirt") ||
                        belowCode.Contains("grass") || belowCode.Contains("forest") ||
                        belowCode.Contains("peat") || belowCode.Contains("sand") ||
                        belowCode.Contains("gravel") || belowCode.Contains("clay"))
                    {
                        return true;
                    }
                }

                // If position is null or block below couldn't be checked,
                // only count if explicitly marked as "wild"
                if (blockCode.Contains("wild"))
                {
                    return true;
                }

                return false;
            }

            // Mushrooms count as wild forage
            if (blockCode.Contains("mushroom-")) return true;

            // NOT included:
            // - tallgrass, flowers, ferns, cattails, reeds, waterlily, seaweed (too common/farmable)
            // - berry- (berry bushes can be replanted)
            // - wildvine (can be replanted/grown)

            return false;
        }

        /// <summary>
        /// Check if a block is a loot vessel / cracked vessel (for Pilferer progression).
        /// Only loot vessels count - they can't be re-placed by players, preventing exploits.
        /// Storage vessels and urns are excluded since players can place and break them repeatedly.
        /// Block code: game:lootvessel-*
        /// </summary>
        private static bool IsCrackedVesselBlock(int blockId)
        {
            if (ServerApi == null) return false;

            Block block = ServerApi.World.GetBlock(blockId);
            if (block == null) return false;

            string blockCode = block.Code?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(blockCode)) return false;

            // Only loot vessels (cracked vessels) count - they don't drop themselves when broken
            if (blockCode.Contains("lootvessel")) return true;

            return false;
        }

        private static bool IsCharcoalPile(int blockId, out int points)
        {
            // Set a default points output value for failure cases
            points = 0;

            if (ServerApi == null) return false;

            Block block = ServerApi.World.GetBlock(blockId);
            if (block == null) return false;

            string blockCode = block.Code?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrEmpty(blockCode)) return false;

            // Only charcoal piles from charcoal pits count - extract the point value from the number of charcoal in the pile by default
            const string PREFIX = "charcoalpile-";
            if (blockCode.StartsWith(PREFIX))
            {
                if (!int.TryParse(blockCode[PREFIX.Length..], out points))
                {
                    // If for some reason parsing of the block code fails, default to one point
                    points = 1;
                }
                return true;
            }

            return false;
        }

        // =========================================================================
        // NEW TRAIT COMMAND HANDLERS
        // =========================================================================

        /// <summary>
        /// Process a translocator repair (called from Harmony patch).
        /// Gives progress toward Technical trait unlock.
        /// </summary>
        public static void ProcessTranslocatorRepair(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            int modifiedRepairs = ApplyXPMultiplier(playerUid, 1);

            AttributeModifierDefinitions.Technical.AddCredits(player, ApplyXPMultiplier(playerUid, modifiedRepairs));
        }

        /// <summary>
        /// Handler for /trait reset command.
        /// Resets all trait progression to 0 for the calling player.
        /// </summary>
        private TextCommandResult OnTraitResetCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            ResetProgressForPlayer(player);

            string coNote = IsCombatOverhaulLoaded ? " (including Combat Overhaul proficiencies)" : "";
            return TextCommandResult.Success($"All trait progression has been reset to 0{coNote}.");
        }

        /// <summary>
        /// Clears every progression system back to class defaults for one player
        /// and strips the applied stat/trait bonuses. Shared by /trait reset and
        /// the pre-step of /trait import. Sends no chat message.
        /// </summary>
        private void ResetProgressForPlayer(IServerPlayer player)
        {
            string playerUid = player.PlayerUID;

            // Reset Mining
            foreach (var definition in LoadedAttributes)
            {
                definition.ResetProgress(player);
            }

            // Clear sleep buff
            SleepBuffExpiration.TryRemove(playerUid, out _);
            SleepBuffMultiplier.TryRemove(playerUid, out _);
            pendingSleepBuffSave = true;

            // Also reset Combat Overhaul proficiency progression so no stale per-weapon
            // bonuses linger after a full reset. No-op if CO isn't loaded.
            ResetCOProgressForPlayer(player);
        }

        // ============================================================
        //  Cross-world progression transfer (/trait export, /trait import)
        //  File-based JSON so a full character (which can be many KB) is not
        //  limited by chat length, and the file persists outside any single
        //  world save. Files live in a global data folder so a different world
        //  can read them. Admin-only (single-player has this privilege).
        // ============================================================

        /// <summary>Reduce a player name to a safe, world-portable file stem.</summary>
        private static string SanitizeExportName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "player";
            var sb = new StringBuilder();
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') sb.Append(c);
                else sb.Append('_');
            }
            string s = sb.ToString().Trim('_');
            return s.Length == 0 ? "player" : s;
        }

        /// <summary>Global (not per-world) folder where transfer files are stored.</summary>
        private string GetTransferDirectory()
        {
            return ServerApi.GetOrCreateDataPath("SeraphLeveling");
        }

        /// <summary>Snapshot every progression system this player has into one container.</summary>
        private PlayerProgressExport BuildProgressExport(IServerPlayer player)
        {
            string uid = player.PlayerUID;
            var ex = new PlayerProgressExport
            {
                FormatVersion = 1,
                SourcePlayerName = player.PlayerName,
                SourcePlayerUid = uid,
                ExportedGameDay = ServerApi.World.Calendar.TotalDays,
                Attributes = [],
            };

            foreach (var kvp in LoadedAttributes)
            {
                if (((dynamic)kvp).ProgressDictionary.TryGetValue(uid, out object data))
                {
                    ex.Attributes[kvp.Id] = data;
                }
            }
            return ex;
        }

        /// <summary>Install imported progression under a UID and flag each system for save.</summary>
        private void ApplyImportedProgress(string uid, PlayerProgressExport ex)
        {
            foreach (var (id, obj) in ex.Attributes)
            {
                var attr = LoadedAttributes.FirstOrDefault(x => x.Id == id);
                if (attr != null)
                {
                    dynamic dict = ((dynamic)attr).ProgressDictionary;
                    Type dictType = dict.GetType();
                    Type targetType = dictType.GetGenericArguments()[1];
                    object stronglyTypedObj = obj;
                    if (obj is Newtonsoft.Json.Linq.JToken token)
                    {
                        stronglyTypedObj = token.ToObject(targetType);
                    }
                    dict[uid] = (dynamic)stronglyTypedObj;
                    attr.PendingSave = true;
                }
            }
        }

        private TextCommandResult OnTraitExportCommand(TextCommandCallingArgs args)
        {
            IServerPlayer caller = args.Caller.Player as IServerPlayer;
            if (caller?.Entity == null) return TextCommandResult.Error("Player not found.");

            IServerPlayer target = caller;
            string nameArg = args[0] as string;
            if (!string.IsNullOrWhiteSpace(nameArg))
            {
                target = ResolvePlayerByName(nameArg);
                if (target == null) return TextCommandResult.Error($"Could not find online player matching '{nameArg}'.");
            }
            if (target?.Entity == null) return TextCommandResult.Error("Target player not found.");

            try
            {
                var ex = BuildProgressExport(target);
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(ex, Newtonsoft.Json.Formatting.Indented);
                string stem = SanitizeExportName(target.PlayerName);
                string dir = GetTransferDirectory();
                string path = Path.Combine(dir, stem + ".json");
                File.WriteAllText(path, json);

                return TextCommandResult.Success(
                    $"Exported {target.PlayerName}'s progression to:\n{path}\nIn another world, run: /trait import {stem}");
            }
            catch (Exception e)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Export failed: {e}");
                return TextCommandResult.Error($"Export failed: {e.Message}");
            }
        }

        private TextCommandResult OnTraitImportCommand(TextCommandCallingArgs args)
        {
            IServerPlayer caller = args.Caller.Player as IServerPlayer;
            if (caller?.Entity == null) return TextCommandResult.Error("Player not found.");

            string fileArg = args[0] as string;
            if (string.IsNullOrWhiteSpace(fileArg))
                return TextCommandResult.Error("Usage: /trait import &lt;filename&gt; [playername]");

            IServerPlayer target = caller;
            string nameArg = args[1] as string;
            if (!string.IsNullOrWhiteSpace(nameArg))
            {
                target = ResolvePlayerByName(nameArg);
                if (target == null) return TextCommandResult.Error($"Could not find online player matching '{nameArg}'.");
            }
            if (target?.Entity == null) return TextCommandResult.Error("Target player not found or not online.");

            // Resolve the file strictly inside the transfer folder (no path traversal).
            string stem = SanitizeExportName(Path.GetFileNameWithoutExtension(fileArg));
            string dir = GetTransferDirectory();
            string path = Path.Combine(dir, stem + ".json");
            if (!File.Exists(path))
                return TextCommandResult.Error($"No export file named '{stem}.json' in {dir}. Run /trait export first.");

            PlayerProgressExport ex;
            try
            {
                string json = File.ReadAllText(path);
                ex = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayerProgressExport>(json);
            }
            catch (Exception e)
            {
                return TextCommandResult.Error($"Failed to read or parse '{stem}.json': {e.Message}");
            }
            if (ex == null) return TextCommandResult.Error("Export file was empty or invalid.");

            string uid = target.PlayerUID;

            // 1. Strip the target's current progression and applied bonuses.
            ResetProgressForPlayer(target);
            // 2. Install the imported data under the target's UID, flag for save.
            ApplyImportedProgress(uid, ex);
            // 3. Re-apply all bonuses live (the same routine that runs on join).
            OnPlayerJoin(target);

            string origin = string.IsNullOrEmpty(ex.SourcePlayerName) ? "" : $" (originally {ex.SourcePlayerName})";
            return TextCommandResult.Success(
                $"Imported progression from '{stem}.json'{origin} onto {target.PlayerName}. Previous progress was replaced.");
        }

        /// <summary>
        /// Handler for /trait maxall command.
        /// Sets all trait progression to maximum for testing purposes.
        /// </summary>
        private TextCommandResult OnTraitMaxAllCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;

            foreach (var definition in LoadedAttributes)
            {
                definition.MaxStat(player);
            }

            return TextCommandResult.Success("All trait progression has been set to maximum for testing.");
        }

        /// <summary>
        /// Handler for /trait testsuite1 command.
        /// Sets every progression skill to exactly 1 credit. Used to visually verify the
        /// dynamic trait display formatting at the smallest non-zero progression: every
        /// dynamic line renders, but values stay low enough that negative-trait cancellation
        /// branches also show their reduced-penalty format.
        ///
        /// Does NOT touch the binary unlock traits (Hardy Health, Bowyer, Improviser,
        /// Tinkerer, Merciless, Technical, Sewing Kit, Claustrophobic Removal). Those are
        /// already covered by individual /trait &lt;name&gt;unlock commands.
        /// </summary>
        private TextCommandResult OnTraitTestSuite1Command(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            const int CREDITS = 1;

            foreach (var definition in LoadedAttributes)
            {
                definition.ApplyTraitTestSuite1Command(player);
            }

            // Combat Overhaul proficiencies (only if CO is loaded)
            string coNote = "";
            if (IsCombatOverhaulLoaded)
            {
                var coProgress = COProgress.GetOrAdd(playerUid, _ => new COPlayerProgressData());
                foreach (var proficiencyStat in AllCOProficiencies)
                {
                    var profProgress = coProgress.GetProficiencyProgress(proficiencyStat);
                    profProgress.TotalCredits = CREDITS;
                    profProgress.WeaponProgress.Clear();
                    ApplyCOProficiencyBonusWithCancellation(player, proficiencyStat, CREDITS);
                }
                coProgress.SteadyAimCredits = CREDITS;
                ApplyCOSteadyAimBonus(player, CREDITS);
                pendingCOProgressSave = true;
                coNote = " (including Combat Overhaul proficiencies)";
            }

            return TextCommandResult.Success($"All progression skills set to 1 credit{coNote}. Open the character sheet to inspect dynamic trait formatting.");
        }

        /// <summary>
        /// Handler for /trait testsuite command.
        /// Runs automated tests for trait calculations.
        /// </summary>
        private TextCommandResult OnTraitTestSuiteCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string category = (string)args[0];
            string result = TraitTestSuite.RunTests(category, player);

            return TextCommandResult.Success(result);
        }

        /// <summary>
        /// Every stat category this mod writes to. Used by /trait verify.
        /// </summary>
        private static readonly string[] VerifyVanillaStats =
        {
            "miningSpeedMul", "oreDropRate", "meleeWeaponsDamage", "rangedWeaponsDamage",
            "rangedWeaponsAcc", "bowDrawingStrength", "walkspeed", "hungerrate",
            "healingeffectivness", "maxhealthExtraPoints", "armorDurabilityLoss",
            "armorWalkSpeedAffectedness", "animalSeekingRange", "mechanicalsDamage",
            "animalLootDropRate", "animalHarvestingTime", "forageDropRate", "wildCropDropRate",
            "vesselContentsDropRate", "rustyGearDropRate", "wholeVesselLootChance",
            "temporalGearTLRepairCost"
        };

        /// <summary>Combat Overhaul stat categories, only listed when Combat Overhaul is loaded.</summary>
        private static readonly string[] VerifyCOStats =
        {
            CO_BOWS_PROFICIENCY, CO_CROSSBOWS_PROFICIENCY, CO_FIREARMS_PROFICIENCY, CO_SLINGS_PROFICIENCY,
            CO_ONE_HANDED_SWORDS_PROFICIENCY, CO_TWO_HANDED_SWORDS_PROFICIENCY, CO_SPEARS_PROFICIENCY,
            CO_JAVELINS_PROFICIENCY, CO_MACES_PROFICIENCY, CO_CLUBS_PROFICIENCY, CO_HALBERDS_PROFICIENCY,
            CO_POLEAXE_PROFICIENCY, CO_AXES_PROFICIENCY, CO_QUARTERSTAFF_PROFICIENCY, CO_STEADY_AIM,
            CO_MELEE_TIER_SLASHING, CO_RANGED_TIER_SLASHING, CO_HEAD_DAMAGE_FACTOR, CO_FACE_DAMAGE_FACTOR,
            CO_LEGS_DAMAGE_FACTOR, CO_FEET_DAMAGE_FACTOR, CO_JUMP_HEIGHT
        };

        /// <summary>
        /// Handler for /trait verify command.
        ///
        /// Prints, for each stat this mod writes, the value the game will actually use and
        /// the individual contributions it is made of. A stat blends by summing its codes
        /// onto a base of 1, so "base 1, trait 0.5, sitCOheadDamage 0.5" means the game sees
        /// 2.0. That makes a trait being counted twice, or a bonus written to a stat nothing
        /// reads, visible at a glance. Stats with nothing but their base are skipped.
        /// </summary>
        private TextCommandResult OnTraitVerifyCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var sb = new StringBuilder();
            sb.AppendLine($"Stat contributions for {player.PlayerName}.");
            sb.AppendLine("Each stat sums its codes onto a base of 1. 'blended' is the number the game uses.");

            int shown = 0;
            foreach (string category in VerifyVanillaStats)
            {
                shown += AppendStatBreakdown(sb, player.Entity, category) ? 1 : 0;
            }

            if (IsCombatOverhaulLoaded)
            {
                sb.AppendLine("Combat Overhaul stats:");
                foreach (string category in VerifyCOStats)
                {
                    shown += AppendStatBreakdown(sb, player.Entity, category) ? 1 : 0;
                }
            }

            if (shown == 0)
            {
                return TextCommandResult.Success("No stat this mod writes has any contribution yet. Earn some progress first.");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Appends one stat's blended value and per-code contributions. Returns false and
        /// appends nothing when the stat is absent or holds nothing but its base of 1.
        /// </summary>
        private static bool AppendStatBreakdown(StringBuilder sb, EntityPlayer entity, string category)
        {
            EntityFloatStats stats = null;
            foreach (KeyValuePair<string, EntityFloatStats> stat in entity.Stats)
            {
                if (stat.Key == category) { stats = stat.Value; break; }
            }
            if (stats?.ValuesByKey == null) return false;

            bool hasContribution = false;
            bool fromTrait = false;
            bool fromThisMod = false;
            foreach (KeyValuePair<string, EntityStat<float>> entry in stats.ValuesByKey)
            {
                if (entry.Key == "base") continue;
                if (Math.Abs(entry.Value.Value) > 0.0001f) hasContribution = true;
                if (entry.Key == "trait") fromTrait = true;
                else if (entry.Key.StartsWith("sit", StringComparison.Ordinal)) fromThisMod = true;
            }
            if (!hasContribution) return false;

            string overlap = fromTrait && fromThisMod ? "  (a trait and this mod both contribute)" : "";
            sb.AppendLine($"{category}: blended {entity.Stats.GetBlended(category):0.###} [{stats.BlendType}]{overlap}");
            foreach (KeyValuePair<string, EntityStat<float>> entry in stats.ValuesByKey)
            {
                sb.AppendLine($"   {entry.Key} = {entry.Value.Value:0.###}");
            }
            return true;
        }

        /// <summary>
        /// Handler for /trait reloadconfig command.
        /// Re-reads ModConfig/SeraphLeveling.json and reapplies every online
        /// player's bonuses against the new caps, so an admin can edit the file on
        /// a dedicated server and see it take hold without restarting.
        /// </summary>
        private TextCommandResult OnTraitReloadConfigCommand(TextCommandCallingArgs args)
        {
            if (ServerApi == null) return TextCommandResult.Error("Server API not available.");

            LoadConfigFile(ServerApi);

            int reapplied = 0;
            foreach (var onlinePlayer in ServerApi.World.AllOnlinePlayers)
            {
                if (onlinePlayer is IServerPlayer player && player.Entity != null)
                {
                    ReapplyAllBonuses(player);
                    reapplied++;
                }
            }

            // The file we just read is already what is on disk, so nothing needs
            // writing back. Drop any queued save so the next world save cannot
            // overwrite the file with values from before the reload.
            pendingConfigSave = false;

            return TextCommandResult.Success(
                $"Reloaded ModConfig/{CONFIG_FILE_NAME} and reapplied bonuses for {reapplied} online player(s).");
        }

        // =========================================================================
        // COMBAT OVERHAUL COMMAND HANDLERS
        // =========================================================================

        /// <summary>
        /// Handler for /trait coproficiency command.
        /// Shows all Combat Overhaul proficiency progression.
        /// </summary>
        private TextCommandResult OnTraitCOProficiencyCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            if (!IsCombatOverhaulLoaded)
            {
                return TextCommandResult.Error("Combat Overhaul mod is not installed.");
            }

            if (!COEnableCompat)
            {
                return TextCommandResult.Error("Combat Overhaul compatibility is disabled in config.");
            }

            string playerUid = player.PlayerUID;
            var sb = new StringBuilder();
            sb.AppendLine("=== Combat Overhaul Proficiency Progression ===");

            if (!COProgress.TryGetValue(playerUid, out var playerProgress))
            {
                sb.AppendLine("No proficiency progress recorded yet. Deal damage with CO weapons to earn credits.");
                return TextCommandResult.Success(sb.ToString());
            }

            // Show Steady Aim (use player-aware max for Trembling Aim)
            var cache = GetCachedTraits(playerUid);
            bool hasTremblingAim = cache?.HasCOTremblingAim == true;
            int steadyAimMaxCredits = GetCOSteadyAimMaxCreditsForPlayer(playerUid);

            // Calculate net bonus (after cancellation if applicable)
            float steadyAimNetBonus;
            if (hasTremblingAim)
            {
                int creditsForBonus = Math.Max(0, playerProgress.SteadyAimCredits - 30);
                steadyAimNetBonus = Math.Min(creditsForBonus * 0.01f, COSteadyAimMax);
            }
            else
            {
                steadyAimNetBonus = CalculateCOProficiencyBonus(playerProgress.SteadyAimCredits, COSteadyAimMax);
            }
            sb.AppendLine($"Steady Aim: {playerProgress.SteadyAimCredits}/{steadyAimMaxCredits} credits (net +{steadyAimNetBonus * 100:F0}%)");

            // Show each proficiency
            sb.AppendLine("\n--- Ranged Proficiencies ---");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_BOWS_PROFICIENCY, "Bows");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_CROSSBOWS_PROFICIENCY, "Crossbows");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_FIREARMS_PROFICIENCY, "Firearms");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_SLINGS_PROFICIENCY, "Slings");

            sb.AppendLine("\n--- One-Handed Melee ---");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_ONE_HANDED_SWORDS_PROFICIENCY, "One-Handed Swords");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_MACES_PROFICIENCY, "Maces");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_CLUBS_PROFICIENCY, "Clubs");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_AXES_PROFICIENCY, "Axes");

            sb.AppendLine("\n--- Two-Handed Melee ---");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_TWO_HANDED_SWORDS_PROFICIENCY, "Two-Handed Swords");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_HALBERDS_PROFICIENCY, "Halberds");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_POLEAXE_PROFICIENCY, "Poleaxe");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_QUARTERSTAFF_PROFICIENCY, "Quarterstaff");

            sb.AppendLine("\n--- Polearms ---");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_SPEARS_PROFICIENCY, "Spears");
            ShowCOProficiencyStats(sb, playerProgress, playerUid, CO_JAVELINS_PROFICIENCY, "Javelins");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Helper to show CO proficiency stats in the command output.
        /// </summary>
        private static void ShowCOProficiencyStats(StringBuilder sb, COPlayerProgressData playerProgress, string playerUid, string proficiencyStat, string displayName)
        {
            if (playerProgress.Proficiencies.TryGetValue(proficiencyStat, out var prof))
            {
                float bonus = CalculateCOProficiencyBonus(prof.TotalCredits, GetCOProficiencyMax(proficiencyStat));
                int maxCredits = GetCOProficiencyMaxCreditsForPlayer(playerUid, proficiencyStat);
                sb.AppendLine($"  {displayName}: {prof.TotalCredits}/{maxCredits} credits (+{bonus * 100:F0}%)");

                // Show per-weapon progress if any
                if (prof.WeaponProgress.Count > 0)
                {
                    foreach (var weapon in prof.WeaponProgress.Take(3)) // Show top 3 weapons
                    {
                        string shortCode = weapon.Key.Contains(":") ? weapon.Key.Substring(weapon.Key.IndexOf(':') + 1) : weapon.Key;
                        sb.AppendLine($"    {shortCode}: {weapon.Value.DamageInIncrement:F0}/{weapon.Value.CurrentIncrementSize} toward next");
                    }
                }
            }
            else
            {
                sb.AppendLine($"  {displayName}: 0 credits (+0.00)");
            }
        }

        /// <summary>
        /// Handler for /trait colevel command.
        /// Sets Combat Overhaul proficiency credits directly.
        /// Usage: /trait colevel <proficiency> <credits>
        /// Proficiency names: bows, crossbows, firearms, slings, 1hswords, 2hswords, spears, javelins, maces, clubs, halberds, axes, quarterstaff, steadyaim
        /// </summary>
        private TextCommandResult OnTraitCOLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            if (!IsCombatOverhaulLoaded)
            {
                return TextCommandResult.Error("Combat Overhaul mod is not installed.");
            }

            string proficiencyArg = (string)args[0];
            int credits = (int)args[1];
            string toolName = (string)args[2];

            // Map short names to full proficiency stat names
            string proficiencyStat = proficiencyArg.ToLowerInvariant() switch
            {
                "bows" or "bow" => CO_BOWS_PROFICIENCY,
                "crossbows" or "crossbow" or "xbow" => CO_CROSSBOWS_PROFICIENCY,
                "firearms" or "firearm" or "guns" or "gun" => CO_FIREARMS_PROFICIENCY,
                "slings" or "sling" => CO_SLINGS_PROFICIENCY,
                "1hswords" or "1hsword" or "1h" or "onehanded" => CO_ONE_HANDED_SWORDS_PROFICIENCY,
                "2hswords" or "2hsword" or "2h" or "twohanded" => CO_TWO_HANDED_SWORDS_PROFICIENCY,
                "spears" or "spear" => CO_SPEARS_PROFICIENCY,
                "javelins" or "javelin" or "jav" => CO_JAVELINS_PROFICIENCY,
                "maces" or "mace" => CO_MACES_PROFICIENCY,
                "clubs" or "club" => CO_CLUBS_PROFICIENCY,
                "halberds" or "halberd" => CO_HALBERDS_PROFICIENCY,
                "poleaxe" or "poleaxes" => CO_POLEAXE_PROFICIENCY,
                "axes" or "axe" => CO_AXES_PROFICIENCY,
                "quarterstaff" or "staff" or "staves" => CO_QUARTERSTAFF_PROFICIENCY,
                "steadyaim" or "steady" or "aim" => CO_STEADY_AIM,
                _ => null
            };

            if (proficiencyStat == null)
            {
                return TextCommandResult.Error($"Unknown proficiency '{proficiencyArg}'. Valid options: bows, crossbows, firearms, slings, 1hswords, 2hswords, spears, javelins, maces, clubs, halberds, poleaxe, axes, quarterstaff, steadyaim");
            }

            return SetCOLevelForPlayer(player, proficiencyStat, credits, toolName);
        }

        /// <summary>
        /// Handler for /trait coreset command.
        /// Resets all Combat Overhaul progression to 0. Also works when CO is uninstalled,
        /// to clear lingering progress data saved on the player.
        /// </summary>
        private TextCommandResult OnTraitCOResetCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool coLoaded = IsCombatOverhaulLoaded;
            ResetCOProgressForPlayer(player);

            return TextCommandResult.Success(coLoaded
                ? "All Combat Overhaul proficiency progression has been reset to 0."
                : "Combat Overhaul is not currently installed; cleared any lingering proficiency data saved on the player.");
        }

        /// <summary>
        /// Shared helper for resetting Combat Overhaul progression. Used by both /trait coreset
        /// and /trait reset. Runs even when CO isn't loaded so stale watched attributes /
        /// progress dictionary entries left over from a prior install can still be cleaned up
        /// (otherwise the display postfix keeps rendering ghost CO traits forever).
        /// </summary>
        private void ResetCOProgressForPlayer(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;

            if (COProgress.TryRemove(playerUid, out _))
            {
                pendingCOProgressSave = true;
            }

            // Clear all CO stats and reapply with 0 credits (properly recalculates negative trait penalties)
            foreach (var proficiencyStat in AllCOProficiencies)
            {
                string statCode = CO_STAT_PREFIX + proficiencyStat;
                player.Entity.Stats.Remove(proficiencyStat, statCode);

                // Reapply with 0 credits to properly set remaining penalty values
                ApplyCOProficiencyBonusWithCancellation(player, proficiencyStat, 0);
            }
            player.Entity.Stats.Remove(CO_STEADY_AIM, CO_STAT_PREFIX + CO_STEADY_AIM);

            // Reapply Steady Aim with 0 credits to properly set Trembling Aim remaining
            ApplyCOSteadyAimBonus(player, 0);
        }

        /// <summary>
        /// Handler for /trait comaxall command.
        /// Sets all Combat Overhaul proficiencies to max for testing.
        /// Dynamically adapts based on player's class and negative traits.
        /// </summary>
        private TextCommandResult OnTraitCOMaxAllCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            if (!IsCombatOverhaulLoaded)
            {
                return TextCommandResult.Error("Combat Overhaul mod is not installed.");
            }

            string playerUid = player.PlayerUID;
            var cache = GetCachedTraits(playerUid);
            var playerProgress = COProgress.GetOrAdd(playerUid, _ => new COPlayerProgressData());

            var sb = new StringBuilder();
            sb.AppendLine("=== Setting all CO proficiencies to max ===");

            // Set each proficiency to max (accounting for negative traits)
            foreach (var proficiencyStat in AllCOProficiencies)
            {
                int maxCredits = GetCOProficiencyMaxCredits(proficiencyStat);

                // Add extra credits for negative trait cancellation
                bool isRanged = IsCORangedProficiency(proficiencyStat);
                bool isPiercing = IsCOPiercingProficiency(proficiencyStat);

                if (isRanged && (cache?.HasCOClumsyHands == true))
                {
                    maxCredits += (int)(CO_CLUMSY_HANDS_PENALTY * 100); // +30
                }
                else if (isRanged && (cache?.HasCOWeakHand == true))
                {
                    maxCredits += (int)(CO_WEAK_HAND_PENALTY * 100); // +30
                }
                else if (!isRanged && (cache?.HasCOFearOfMelee == true))
                {
                    maxCredits += CO_FEAR_OF_MELEE_TIER_PENALTY * 100; // +100
                }
                else if (isPiercing && (cache?.HasCONervous == true))
                {
                    maxCredits += CO_NERVOUS_TIER_PENALTY * 100; // +100
                }

                var profProgress = playerProgress.GetProficiencyProgress(proficiencyStat);
                profProgress.TotalCredits = maxCredits;
                profProgress.WeaponProgress.Clear();
                ApplyCOProficiencyBonusWithCancellation(player, proficiencyStat, maxCredits);

                float bonus = CalculateCOProficiencyBonus(maxCredits, GetCOProficiencyMax(proficiencyStat));
                sb.AppendLine($"  {GetCOProficiencyDisplayName(proficiencyStat)}: {maxCredits} credits (+{bonus * 100:F0}%)");
            }

            // Set Steady Aim to max (accounting for Trembling Aim)
            int steadyAimMax = GetCOSteadyAimMaxCreditsForPlayer(playerUid);
            playerProgress.SteadyAimCredits = steadyAimMax;
            ApplyCOSteadyAimBonus(player, steadyAimMax);

            float steadyAimBonus;
            if (cache?.HasCOTremblingAim == true)
            {
                int creditsForBonus = Math.Max(0, steadyAimMax - 30);
                steadyAimBonus = Math.Min(creditsForBonus * 0.01f, COSteadyAimMax);
            }
            else
            {
                steadyAimBonus = CalculateCOProficiencyBonus(steadyAimMax, COSteadyAimMax);
            }
            sb.AppendLine($"  Steady Aim: {steadyAimMax} credits (net +{steadyAimBonus * 100:F0}%)");

            pendingCOProgressSave = true;

            sb.AppendLine("\nAll CO proficiencies set to max!");
            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Unified handler for per-proficiency CO commands.
        /// Handles viewing stats and configuring base/increment/level/max.
        /// Usage: /trait [proficiency] [action] [value]
        /// Actions: base, increment, level, max (admin only for setting values)
        /// </summary>
        private TextCommandResult OnTraitCOProficiencyConfigCommand(TextCommandCallingArgs args, string proficiencyStat)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            if (!IsCombatOverhaulLoaded)
            {
                return TextCommandResult.Error("Combat Overhaul mod is not installed.");
            }

            string action = args[0] as string;
            int? value = args[1] as int?;

            string displayName = GetCOProficiencyDisplayName(proficiencyStat);
            string playerUid = player.PlayerUID;
            var playerProgress = COProgress.GetOrAdd(playerUid, _ => new COPlayerProgressData());

            // No action = view stats
            if (string.IsNullOrEmpty(action))
            {
                return ShowCOProficiencyStatsForPlayer(player, playerProgress, proficiencyStat, displayName);
            }

            // Check admin privilege for configuration actions
            bool isAdmin = player.HasPrivilege(Privilege.controlserver);
            action = action.ToLowerInvariant();

            switch (action)
            {
                case "base":
                    if (!value.HasValue)
                    {
                        int currentBase = GetCOProficiencyBase(proficiencyStat);
                        int currentIncrement = GetCOProficiencyIncrement(proficiencyStat);
                        return TextCommandResult.Success($"{displayName} base damage per increment: {currentBase}\nIncrement step: +{currentIncrement} per credit");
                    }
                    if (!isAdmin) return TextCommandResult.Error("Setting base requires admin privileges.");
                    if (value.Value < 1) return TextCommandResult.Error("Base damage must be at least 1.");
                    SetCOProficiencyBase(proficiencyStat, value.Value);
                    pendingConfigSave = true;
                    return TextCommandResult.Success($"{displayName} base damage per increment set to {value.Value}.");

                case "increment":
                    if (!value.HasValue)
                    {
                        int currentBase = GetCOProficiencyBase(proficiencyStat);
                        int currentIncrement = GetCOProficiencyIncrement(proficiencyStat);
                        return TextCommandResult.Success($"{displayName} increment step: +{currentIncrement} per credit\nBase damage: {currentBase}");
                    }
                    if (!isAdmin) return TextCommandResult.Error("Setting increment requires admin privileges.");
                    if (value.Value < 0) return TextCommandResult.Error("Increment step cannot be negative.");
                    SetCOProficiencyIncrement(proficiencyStat, value.Value);
                    pendingConfigSave = true;
                    return TextCommandResult.Success($"{displayName} increment step set to +{value.Value} per credit.");

                case "level":
                    if (!value.HasValue)
                    {
                        int credits = proficiencyStat == CO_STEADY_AIM
                            ? playerProgress.SteadyAimCredits
                            : playerProgress.GetProficiencyProgress(proficiencyStat).TotalCredits;
                        return TextCommandResult.Success($"{displayName} current level: {credits} credits");
                    }
                    if (!isAdmin) return TextCommandResult.Error("Setting level requires admin privileges.");
                    return SetCOLevelForPlayer(player, proficiencyStat, value.Value, null);

                case "max":
                    if (!value.HasValue)
                    {
                        float currentMax = GetCOProficiencyMax(proficiencyStat);
                        return TextCommandResult.Success($"{displayName} max bonus: +{currentMax * 100:F0}%");
                    }
                    if (!isAdmin) return TextCommandResult.Error("Setting max requires admin privileges.");
                    // Max is stored as float (0.5 = +0.5 bonus), but user enters as percentage points (50 = +0.5)
                    float newMax = value.Value / 100f;
                    if (newMax < 0) return TextCommandResult.Error("Max bonus cannot be negative.");
                    SetCOProficiencyMax(proficiencyStat, newMax);
                    pendingConfigSave = true;
                    return TextCommandResult.Success($"{displayName} max bonus set to +{newMax * 100:F0}% ({value.Value} credits).");

                default:
                    return TextCommandResult.Error($"Unknown action '{action}'. Valid actions: base, increment, level, max");
            }
        }

        /// <summary>
        /// Show detailed stats for a specific CO proficiency.
        /// </summary>
        private TextCommandResult ShowCOProficiencyStatsForPlayer(IServerPlayer player, COPlayerProgressData playerProgress, string proficiencyStat, string displayName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {displayName} ===");

            int credits;
            float maxBonus = GetCOProficiencyMax(proficiencyStat);
            int maxCredits = (int)(maxBonus * 100);

            if (proficiencyStat == CO_STEADY_AIM)
            {
                credits = playerProgress.SteadyAimCredits;
                var steadyAimProgress = playerProgress.GetProficiencyProgress(CO_STEADY_AIM);

                sb.AppendLine($"Credits: {credits} / {maxCredits}");
                sb.AppendLine($"Bonus: +{CalculateCOProficiencyBonus(credits, maxBonus) * 100:F0}%");
                sb.AppendLine($"Base damage: {GetCOProficiencyBase(proficiencyStat)}");
                sb.AppendLine($"Increment step: +{GetCOProficiencyIncrement(proficiencyStat)} per credit");

                if (steadyAimProgress.WeaponProgress.Count > 0)
                {
                    sb.AppendLine("\nWeapon progress:");
                    foreach (var kvp in steadyAimProgress.WeaponProgress.OrderBy(p => p.Value.CurrentIncrementSize))
                    {
                        string weaponName = kvp.Key;
                        if (weaponName.Contains(":"))
                            weaponName = weaponName.Substring(weaponName.IndexOf(':') + 1);
                        sb.AppendLine($"  {weaponName}: {kvp.Value.DamageInIncrement:F0}/{kvp.Value.CurrentIncrementSize} damage");
                    }
                }
            }
            else
            {
                var profProgress = playerProgress.GetProficiencyProgress(proficiencyStat);
                credits = profProgress.TotalCredits;

                sb.AppendLine($"Credits: {credits} / {maxCredits}");
                sb.AppendLine($"Bonus: +{CalculateCOProficiencyBonus(credits, maxBonus) * 100:F0}%");
                sb.AppendLine($"Base damage: {GetCOProficiencyBase(proficiencyStat)}");
                sb.AppendLine($"Increment step: +{GetCOProficiencyIncrement(proficiencyStat)} per credit");

                if (profProgress.WeaponProgress.Count > 0)
                {
                    sb.AppendLine("\nWeapon progress:");
                    foreach (var kvp in profProgress.WeaponProgress.OrderBy(p => p.Value.CurrentIncrementSize))
                    {
                        string weaponName = kvp.Key;
                        if (weaponName.Contains(":"))
                            weaponName = weaponName.Substring(weaponName.IndexOf(':') + 1);
                        sb.AppendLine($"  {weaponName}: {kvp.Value.DamageInIncrement:F0}/{kvp.Value.CurrentIncrementSize} damage");
                    }
                }
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Get the base damage per increment for a specific CO proficiency.
        /// Returns per-proficiency value if set, otherwise falls back to global default.
        /// </summary>
        private static int GetCOProficiencyBase(string proficiencyStat)
        {
            if (COProficiencyBaseOverrides.TryGetValue(proficiencyStat, out int value))
                return value;
            return COBaseDamagePerIncrement;
        }

        /// <summary>
        /// Set the base damage per increment for a specific CO proficiency.
        /// </summary>
        private static void SetCOProficiencyBase(string proficiencyStat, int value)
        {
            COProficiencyBaseOverrides[proficiencyStat] = value;
        }

        /// <summary>
        /// Get the increment step for a specific CO proficiency.
        /// Returns per-proficiency value if set, otherwise falls back to global default.
        /// </summary>
        private static int GetCOProficiencyIncrement(string proficiencyStat)
        {
            if (COProficiencyIncrementOverrides.TryGetValue(proficiencyStat, out int value))
                return value;
            return COIncrementStep;
        }

        /// <summary>
        /// Set the increment step for a specific CO proficiency.
        /// </summary>
        private static void SetCOProficiencyIncrement(string proficiencyStat, int value)
        {
            COProficiencyIncrementOverrides[proficiencyStat] = value;
        }

        /// <summary>
        /// Set the max bonus for a specific CO proficiency.
        /// </summary>
        private static void SetCOProficiencyMax(string proficiencyStat, float value)
        {
            switch (proficiencyStat)
            {
                case CO_BOWS_PROFICIENCY: COBowsProficiencyMax = value; break;
                case CO_CROSSBOWS_PROFICIENCY: COCrossbowsProficiencyMax = value; break;
                case CO_FIREARMS_PROFICIENCY: COFirearmsProficiencyMax = value; break;
                case CO_SLINGS_PROFICIENCY: COSlingsProficiencyMax = value; break;
                case CO_ONE_HANDED_SWORDS_PROFICIENCY: COOneHandedSwordsProficiencyMax = value; break;
                case CO_TWO_HANDED_SWORDS_PROFICIENCY: COTwoHandedSwordsProficiencyMax = value; break;
                case CO_SPEARS_PROFICIENCY: COSpearsProficiencyMax = value; break;
                case CO_JAVELINS_PROFICIENCY: COJavelinsProficiencyMax = value; break;
                case CO_MACES_PROFICIENCY: COMacesProficiencyMax = value; break;
                case CO_CLUBS_PROFICIENCY: COClubsProficiencyMax = value; break;
                case CO_HALBERDS_PROFICIENCY: COHalberdsProficiencyMax = value; break;
                case CO_POLEAXE_PROFICIENCY: COPoleaxeProficiencyMax = value; break;
                case CO_AXES_PROFICIENCY: COAxesProficiencyMax = value; break;
                case CO_QUARTERSTAFF_PROFICIENCY: COQuarterstaffProficiencyMax = value; break;
                case CO_STEADY_AIM: COSteadyAimMax = value; break;
            }
        }

        // =========================================================================
        // COMBAT OVERHAUL PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist Combat Overhaul proficiency progress to world save data.
        /// </summary>
        public static void PersistCOProgress()
        {
            PersistProgress<COPlayerProgressData>();
        }

        /// <summary>
        /// Load Combat Overhaul proficiency progress from world save data.
        /// </summary>
        private void LoadCOProgress()
        {
            LoadProgress<COPlayerProgressData>();
        }

        // =========================================================================
        // SLEEP BUFF PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist sleep buff data to world save data.
        /// </summary>
        public static void PersistSleepBuffData()
        {
            if (ServerApi == null) return;

            lock (persistLock)
            {
                if (SleepBuffExpiration.IsEmpty)
                {
                    return;
                }

                try
                {
                    var snapshot = SleepBuffExpiration.ToArray();

                    byte[] data;
                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            // Magic bytes: "SLB" (Sleep Buff)
                            writer.Write((byte)0x53); // 'S'
                            writer.Write((byte)0x4C); // 'L'
                            writer.Write((byte)0x42); // 'B'
                            writer.Write((byte)1);    // Version 1

                            writer.Write(snapshot.Length);
                            foreach (var entry in snapshot)
                            {
                                writer.Write(entry.Key); // Player UID
                                writer.Write(entry.Value); // Expiration day
                                // Get multiplier (default to 1.0 if somehow missing)
                                float mult = SleepBuffMultiplier.TryGetValue(entry.Key, out float m) ? m : 1.0f;
                                writer.Write(mult);
                            }
                        }
                        data = ms.ToArray();
                    }

                    ServerApi.WorldManager.SaveGame.StoreData(SLEEP_BUFF_SAVE_KEY, data);
                    ServerApi.Logger.Debug($"[SeraphLeveling] Persisted sleep buff data for {snapshot.Length} players");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[SeraphLeveling] Failed to persist sleep buff data: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Load sleep buff data from world save data.
        /// Expired buffs are discarded on load.
        /// </summary>
        private void LoadSleepBuffData()
        {
            if (ServerApi == null) return;

            SleepBuffExpiration.Clear();
            SleepBuffMultiplier.Clear();

            try
            {
                byte[] data = ServerApi.WorldManager.SaveGame.GetData(SLEEP_BUFF_SAVE_KEY);
                if (data == null || data.Length == 0)
                {
                    ServerApi.Logger.Debug("[SeraphLeveling] No sleep buff data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        byte b3 = reader.ReadByte();

                        if (b1 != 0x53 || b2 != 0x4C || b3 != 0x42) // "SLB"
                        {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid sleep buff data format");
                            return;
                        }

                        byte version = reader.ReadByte();
                        int count = reader.ReadInt32();

                        if (version == 1)
                        {
                            double currentDay = ServerApi.World?.Calendar?.TotalDays ?? 0;
                            int loaded = 0;

                            for (int i = 0; i < count; i++)
                            {
                                try
                                {
                                    string playerUid = reader.ReadString();
                                    double expiration = reader.ReadDouble();
                                    float multiplier = reader.ReadSingle();

                                    // Only restore buffs that haven't expired
                                    if (currentDay < expiration)
                                    {
                                        SleepBuffExpiration[playerUid] = expiration;
                                        SleepBuffMultiplier[playerUid] = multiplier;
                                        loaded++;
                                    }
                                }
                                catch (Exception innerEx)
                                {
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i + 1}/{count} in sleep buff data: {innerEx.Message}");
                                    break;
                                }
                            }

                            ServerApi.Logger.Notification($"[SeraphLeveling] Loaded sleep buff data: {loaded} active buffs ({count - loaded} expired)");
                        }
                        else
                        {
                            ServerApi.Logger.Warning($"[SeraphLeveling] Unknown sleep buff save format version {version}");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[SeraphLeveling] Failed to load sleep buff data: {ex.Message}");
            }
        }

    }

    /// <summary>
    /// Client-side mod system that displays mining progression in the character traits dialog.
    /// Uses Harmony to patch the CharacterSystem's trait display method and adds scrollable traits UI.
    /// </summary>
    public class SeraphLevelingClientSystem : ModSystem
    {
        private ICoreClientAPI clientApi;
        private Harmony harmony;

        // Scroll-related fields for traits UI
        private GuiDialogCharacterBase charDlg;
        private ElementBounds clippingBounds;
        private ElementBounds scrollbarBounds;
        private GuiElementRichtext richtextElem;
        private bool hasHookedDialog = false;
        private object characterSystemInstance;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            clientApi = api;
            // Mirror the server's mod detection on the client.
            SeraphLevelingModSystem.DetectLoadedMods(api.ModLoader);

            // Register network channel for receiving level-up sounds from server
            api.Network.RegisterChannel("seraphleveling")
                .RegisterMessageType<LevelUpSoundMessage>()
                .SetMessageHandler<LevelUpSoundMessage>(OnLevelUpSoundReceived);

            // Apply Harmony patches manually for better control
            const string HARMONY_ID = "seraphleveling";
            harmony = new Harmony(HARMONY_ID);
            try
            {
                if (!Harmony.HasAnyPatches(HARMONY_ID))
                {
                    ApplyPatches(api);
                    api.Logger.Notification("[SeraphLeveling] Client-side mod loaded, Harmony patches applied");
                }
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[SeraphLeveling] Failed to apply Harmony patches: {ex.Message}");
                api.Logger.Error($"[SeraphLeveling] Stack trace: {ex.StackTrace}");
            }

            // Register event to hook into character dialog when it's loaded
            api.Event.PlayerJoin += OnPlayerJoin;
        }

        /// <summary>
        /// Called when the server sends a level-up sound message. Plays the sound locally on the client.
        /// </summary>
        private void OnLevelUpSoundReceived(LevelUpSoundMessage message)
        {
            try
            {
                var player = clientApi?.World?.Player?.Entity;
                if (player != null && !string.IsNullOrEmpty(message?.SoundName))
                {
                    float clamped = Math.Clamp(message.Volume, 0f, 1f);
                    clientApi.World.PlaySoundAt(new AssetLocation(message.SoundName), player, null, true, 16f, clamped);

                    if (message.IsTest)
                    {
                        string rawVolStr = message.Volume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                        string playedVolStr = clamped.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                        string label = $"[SeraphLeveling] Test sound received. sound={message.SoundName}, volume sent={rawVolStr}, volume played={playedVolStr}";
                        clientApi.Logger.Notification(label);
                        clientApi.ShowChatMessage(label);
                    }
                }
            }
            catch (Exception ex)
            {
                clientApi?.Logger.Warning($"[SeraphLeveling] Failed to play level-up sound: {ex.Message}");
            }
        }

        private void OnPlayerJoin(IClientPlayer byPlayer)
        {
            // Try to hook into the character dialog after a short delay to ensure it's loaded
            clientApi.Event.RegisterCallback(TryHookCharacterDialog, 500);
        }

        private void TryHookCharacterDialog(float dt)
        {
            if (hasHookedDialog) return;

            try
            {
                // Find the character dialog in loaded GUIs
                charDlg = clientApi.Gui.LoadedGuis.Find(dlg => dlg is GuiDialogCharacterBase) as GuiDialogCharacterBase;

                if (charDlg == null)
                {
                    // Dialog not loaded yet, try again later
                    clientApi.Event.RegisterCallback(TryHookCharacterDialog, 1000);
                    return;
                }

                // Find the CharacterSystem instance to call getClassTraitText
                var characterSystemType = AccessTools.TypeByName("Vintagestory.GameContent.CharacterSystem");
                if (characterSystemType != null)
                {
                    // Get the mod system using the type's full name
                    characterSystemInstance = clientApi.ModLoader.GetModSystem(characterSystemType.FullName);
                }

                // Remove the vanilla CharacterSystem's trait tab handler and track its position
                Action<GuiComposer> handlerToRemove = null;
                int handlerIndex = -1;
                for (int i = 0; i < charDlg.RenderTabHandlers.Count; i++)
                {
                    var handler = charDlg.RenderTabHandlers[i];
                    if (handler.Target?.ToString()?.Contains("CharacterSystem") == true)
                    {
                        handlerToRemove = handler;
                        handlerIndex = i;
                        break;
                    }
                }

                if (handlerToRemove != null)
                {
                    charDlg.RenderTabHandlers.Remove(handlerToRemove);
                    clientApi.Logger.Debug("[SeraphLeveling] Removed vanilla CharacterSystem trait tab handler at index {0}", handlerIndex);
                }

                // Insert our scrollable trait tab handler at the same position (or at index 1 if not found)
                int insertIndex = handlerIndex >= 0 ? handlerIndex : Math.Min(1, charDlg.RenderTabHandlers.Count);
                charDlg.RenderTabHandlers.Insert(insertIndex, ComposeTraitsTab);
                clientApi.Logger.Debug("[SeraphLeveling] Inserted our trait tab handler at index {0}", insertIndex);
                hasHookedDialog = true;

                clientApi.Logger.Notification("[SeraphLeveling] Successfully hooked into character dialog for scrollable traits");
            }
            catch (Exception ex)
            {
                clientApi.Logger.Error($"[SeraphLeveling] Failed to hook character dialog: {ex.Message}");
                // Retry after a delay
                clientApi.Event.RegisterCallback(TryHookCharacterDialog, 2000);
            }
        }

        /// <summary>
        /// Composes the traits tab with scrolling support.
        /// </summary>
        private void ComposeTraitsTab(GuiComposer compo)
        {
            // Get the trait text from the CharacterSystem (our postfix patch will modify it)
            string traitText = GetClassTraitText();

            // Define bounds for the scrollable area
            // Standard traits tab area is approximately 385x310 pixels
            clippingBounds = ElementBounds.Fixed(0, 25, 385, 310);

            // Begin clip area for scrollable content
            compo.BeginClip(clippingBounds);

            // Add richtext element for trait display
            // Use a tall container to hold all traits (will be clipped and scrolled)
            ElementBounds textBounds = ElementBounds.Fixed(0, 0, 370, 1000);
            compo.AddRichtext(traitText, CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15), textBounds, "traitsText");

            compo.EndClip();

            // Add scrollbar to the right of the clipping area
            scrollbarBounds = clippingBounds.RightCopy().WithFixedWidth(10).WithFixedPadding(3, 0);
            compo.AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "traitsScrollbar");

            // Get reference to the richtext element for scroll updates
            richtextElem = compo.GetRichtext("traitsText");

            // Calculate and set scroll heights after composition
            // We need to set this after the composer has composed to get accurate heights
            compo.OnComposed += () =>
            {
                SetScrollbarHeights(compo);
            };
        }

        /// <summary>
        /// Sets the scrollbar heights based on actual content size.
        /// </summary>
        private void SetScrollbarHeights(GuiComposer compo)
        {
            try
            {
                var scrollbar = compo.GetScrollbar("traitsScrollbar");
                var richtext = compo.GetRichtext("traitsText");

                if (scrollbar != null && richtext != null)
                {
                    float visibleHeight = (float)clippingBounds.fixedHeight;
                    // Get actual content height from richtext bounds
                    float totalHeight = (float)richtext.Bounds.fixedHeight;

                    // If content fits, use visible height as total (no scrolling needed)
                    if (totalHeight < visibleHeight)
                    {
                        totalHeight = visibleHeight;
                    }

                    scrollbar.SetHeights(visibleHeight, totalHeight);
                }
            }
            catch (Exception ex)
            {
                clientApi?.Logger?.Debug($"[SeraphLeveling] Error setting scrollbar heights: {ex.Message}");
            }
        }

        /// <summary>
        /// Callback when scrollbar value changes - adjusts content position.
        /// </summary>
        private void OnNewScrollbarValue(float value)
        {
            if (richtextElem != null)
            {
                richtextElem.Bounds.fixedY = 0 - value;
                richtextElem.Bounds.CalcWorldBounds();
            }
        }

        /// <summary>
        /// Gets the trait text by calling the CharacterSystem's getClassTraitText method.
        /// Our postfix patch will modify this text to include dynamic trait values.
        /// </summary>
        private string GetClassTraitText()
        {
            try
            {
                if (characterSystemInstance != null)
                {
                    var method = AccessTools.Method(characterSystemInstance.GetType(), "getClassTraitText");
                    if (method != null)
                    {
                        return method.Invoke(characterSystemInstance, null) as string ?? "";
                    }
                }

                // Fallback: return empty if we can't get the trait text
                return Lang.Get("charactersheet-notraits");
            }
            catch (Exception ex)
            {
                clientApi?.Logger?.Debug($"[SeraphLeveling] Error getting trait text: {ex.Message}");
                if (ex.InnerException != null)
                {
                    clientApi?.Logger?.Debug($"   [SeraphLeveling] Error getting trait text inner exception: {ex.InnerException.Message}");
                }
                return "";
            }
        }

        private void ApplyPatches(ICoreClientAPI api)
        {
            // Set the API reference for the patch to use
            CharacterSystemPatches.ClientApi = api;

            // Find the CharacterSystem type
            var characterSystemType = AccessTools.TypeByName("Vintagestory.GameContent.CharacterSystem");
            if (characterSystemType == null)
            {
                api.Logger.Warning("[SeraphLeveling] Could not find CharacterSystem type");
                return;
            }

            // Find the getClassTraitText method
            var targetMethod = AccessTools.Method(characterSystemType, "getClassTraitText");
            if (targetMethod == null)
            {
                api.Logger.Warning("[SeraphLeveling] Could not find getClassTraitText method");

                // List available methods for debugging
                var methods = characterSystemType.GetMethods(System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                api.Logger.Debug($"[SeraphLeveling] Available methods in CharacterSystem:");
                foreach (var m in methods)
                {
                    if (m.Name.ToLower().Contains("trait"))
                    {
                        api.Logger.Debug($"  - {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}) -> {m.ReturnType.Name}");
                    }
                }
                return;
            }

            api.Logger.Debug($"[SeraphLeveling] Found method: {targetMethod.Name}, params: {string.Join(", ", targetMethod.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}");

            // Get our postfix method
            var postfixMethod = AccessTools.Method(typeof(CharacterSystemPatches), nameof(CharacterSystemPatches.GetClassTraitText_Postfix));

            // Apply the patch
            harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfixMethod));
            api.Logger.Notification("[SeraphLeveling] Successfully patched getClassTraitText");
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll("seraphleveling");

            // Unhook from character dialog
            if (charDlg != null && hasHookedDialog)
            {
                try
                {
                    charDlg.RenderTabHandlers.Remove(ComposeTraitsTab);
                }
                catch { }
            }

            if (clientApi != null)
            {
                clientApi.Event.PlayerJoin -= OnPlayerJoin;
            }

            base.Dispose();
        }
    }
}
