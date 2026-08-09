using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        public static int BaseBlocksPerIncrement = 100;  // Base points needed for first credit
        public static int IncrementStep = 100;           // How much more points each subsequent credit needs
        public static int MaxMiningSpeedPercent = 50;    // 50% max bonus
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

        // Storage for melee progress - keyed by player UID
        public static ConcurrentDictionary<string, MeleeProgressData> MeleeProgress = new ConcurrentDictionary<string, MeleeProgressData>();

        // Flag to indicate pending melee progress save
        public static volatile bool pendingMeleeProgressSave = false;

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

        // Vanilla Focused trait bonuses (used for cap calculations)
        public const int VANILLA_FOCUSED_DAMAGE_BONUS = 20;
        public const int VANILLA_FOCUSED_ACCURACY_BONUS = 30;
        public const int VANILLA_FOCUSED_DISTANCE_BONUS = 20;

        // Storage for ranged progress - keyed by player UID
        public static ConcurrentDictionary<string, RangedProgressData> RangedProgress = new ConcurrentDictionary<string, RangedProgressData>();

        // Flag to indicate pending ranged progress save
        public static volatile bool pendingRangedProgressSave = false;

        // Keys for walking speed progression system
        public const string WALKING_STAT_CODE = "sitWalkingBonus";

        // WatchedAttributes keys for client sync (walking)
        public const string WATCHED_WALKING_LEVEL = "sitWalkingLevel";
        public const string WATCHED_WALKING_BONUS = "sitWalkingBonusPercent";

        // Trait code for the walking speed mastery trait (Fleetfooted)
        public const string WALKING_TRAIT_CODE = "sitwalkingmastery";

        // Walking speed progression configuration
        // Base blocks for first 1%: 1000 blocks
        // Each subsequent 1% requires +1000 more blocks (1000, 2000, 3000, etc.)
        public static int BaseBlocksWalkedPerIncrement = 1000;  // Base blocks needed for first credit
        public static int WalkingIncrementStep = 1000;          // How much more blocks each subsequent credit needs
        public static int MaxWalkingSpeedPercent = 15;          // 15% max bonus (115% total speed)

        // Vanilla Fleetfooted trait walk speed bonus (used for cap calculations)
        public const int VANILLA_FLEETFOOTED_WALK_BONUS = 10;

        // Storage for walking progress - keyed by player UID
        public static ConcurrentDictionary<string, WalkingProgressData> WalkingProgress = new ConcurrentDictionary<string, WalkingProgressData>();

        // Flag to indicate pending walking progress save
        public static volatile bool pendingWalkingProgressSave = false;

        // Tracking last known positions for walking distance calculation (using Position2D to avoid Vec3d allocations)
        private static ConcurrentDictionary<string, Position2D> lastPlayerPositions = new ConcurrentDictionary<string, Position2D>();

        // Maximum distance per tick to count (prevents teleportation from counting)
        private const float MAX_DISTANCE_PER_TICK = 10f;

        // Cache for vanilla trait checks - populated once on player join
        private static ConcurrentDictionary<string, CachedVanillaTraits> VanillaTraitsCache = new ConcurrentDictionary<string, CachedVanillaTraits>();

        // Keys for hunger rate progression system
        public const string HUNGER_STAT_CODE = "sitHungerBonus";
        // WatchedAttributes keys for client sync (hunger)
        public const string WATCHED_HUNGER_LEVEL = "sitHungerLevel";
        public const string WATCHED_HUNGER_BONUS = "sitHungerBonusPercent";

        // Trait code for the hunger mastery trait
        public const string HUNGER_TRAIT_CODE = "sithungermastery";

        // Hunger rate progression configuration
        // Base seconds at full saturation for first 1%: 300 seconds (5 minutes)
        // Each subsequent 1% requires +60 more seconds (5 min, 6 min, 7 min, etc.)
        public static int BaseSecondsPerIncrement = 300;   // Base seconds needed for first credit (5 minutes)
        public static int HungerIncrementStep = 60;        // How many more seconds each subsequent credit needs (1 minute)
        public static int MaxHungerReductionPercent = 25;  // 25% max hunger rate reduction (to 75% rate)

        // Vanilla Ravenous trait hunger rate increase (used for cap calculations)
        // Blackguard has +30% hunger rate, so earning 25% brings them back to nearly normal
        public const int VANILLA_RAVENOUS_HUNGER_PENALTY = 30;
        public const string WATCHED_RAVENOUS_REMAINING = "sitRavenousRemaining";

        // Storage for hunger progress - keyed by player UID
        public static ConcurrentDictionary<string, HungerProgressData> HungerProgress = new ConcurrentDictionary<string, HungerProgressData>();

        // Flag to indicate pending hunger progress save
        public static volatile bool pendingHungerProgressSave = false;

        // Keys for armor progression system
        public const string ARMOR_DURABILITY_STAT_CODE = "sitArmorDurabilityBonus";
        public const string ARMOR_WALKSPEED_STAT_CODE = "sitArmorWalkSpeedBonus";
        // WatchedAttributes keys for client sync (armor)
        public const string WATCHED_ARMOR_DURABILITY_LEVEL = "sitArmorDurabilityLevel";
        public const string WATCHED_ARMOR_DURABILITY_BONUS = "sitArmorDurabilityBonusPercent";
        public const string WATCHED_ARMOR_WALKSPEED_LEVEL = "sitArmorWalkSpeedLevel";
        public const string WATCHED_ARMOR_WALKSPEED_BONUS = "sitArmorWalkSpeedBonusPercent";

        // Trait code for the armor mastery trait (Soldier)
        public const string ARMOR_TRAIT_CODE = "sitarmormastery";

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

        // WatchedAttributes keys for new armor stats
        public const string WATCHED_ARMOR_HUNGER_REDUCTION = "sitArmorHungerReduction";
        public const string WATCHED_ARMOR_HEALING_BONUS = "sitArmorHealingBonus";

        // Vanilla Soldier trait armor bonuses (used for cap calculations)
        public const int VANILLA_SOLDIER_ARMOR_DURABILITY_BONUS = 15;
        public const int VANILLA_SOLDIER_ARMOR_WALKSPEED_BONUS = 25;

        // Storage for armor progress - keyed by player UID
        public static ConcurrentDictionary<string, ArmorProgressData> ArmorProgress = new ConcurrentDictionary<string, ArmorProgressData>();

        // Flag to indicate pending armor progress save
        public static volatile bool pendingArmorProgressSave = false;

        // Tracking currently equipped armor for each player (for time tracking and equip detection)
        private static ConcurrentDictionary<string, Dictionary<string, string>> playerEquippedArmor = new ConcurrentDictionary<string, Dictionary<string, string>>();

        // =========================================================================
        // CLOTHIER TRAIT - Tracks unique clothing worn to unlock sewing kit crafting
        // =========================================================================
        public const string CLOTHIER_STAT_CODE = "sitClothierBonus";
        public const string WATCHED_CLOTHIER_COUNT = "sitClothierCount";
        public const string WATCHED_CLOTHIER_UNLOCKED = "sitClothierUnlocked";
        public const string CLOTHIER_TRAIT_CODE = "sitclothiermastery";

        // Clothier progression configuration
        public static int ClothierRequiredUniqueClothes = 20; // Number of unique clothes to unlock sewing kit
        public static string[] ClothierBlacklistedItems = null;
        public static void initializeClothierBlacklistedItems(ICoreAPI api)
        {
            bool hasSacredLib = SeraphLevelingModSystem.DetectAnySacredLib(api.ModLoader);
            api.Logger.Notification($"[SeraphLeveling] Initializing Clothier Blacklisted Items. Sacred Classes compatibility enabled: {hasSacredLib}");
            ClothierBlacklistedItems = hasSacredLib ? 
            new string[]
            {
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
            }
            :
            new string[] {
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
                "clothes-upperbody-clockmaker-shirt", "clothes-shoulder-clockmaker-apron",
                // Commoner
                "clothes-upperbody-commoner-shirt", "clothes-upperbodyover-commoner-coat",
                "clothes-lowerbody-commoner-trousers", "clothes-foot-commoner-boots", "clothes-hand-commoner-gloves"
            };

        }
        // Vanilla Clothier trait (Tailor exclusive)
        public const int VANILLA_CLOTHIER_BONUS = 0; // No vanilla bonus, this is unlock-based

        // Storage for clothier progress
        public static ConcurrentDictionary<string, ClothierProgressData> ClothierProgress = new ConcurrentDictionary<string, ClothierProgressData>();
        public static volatile bool pendingClothierProgressSave = false;

        // Tracking currently equipped clothing for each player
        private static ConcurrentDictionary<string, Dictionary<string, string>> playerEquippedClothing = new ConcurrentDictionary<string, Dictionary<string, string>>();

        // =========================================================================
        // MENDER TRAIT - Tracks sewing kit repairs for durability bonus
        // =========================================================================
        public const string MENDER_STAT_CODE = "sitMenderBonus";
        public const string WATCHED_MENDER_LEVEL = "sitMenderLevel";
        public const string WATCHED_MENDER_BONUS = "sitMenderBonusPercent";
        public const string MENDER_TRAIT_CODE = "sitmendermastery";

        // Mender progression configuration
        public static int BaseMenderRepairsPerIncrement = 5;   // Base repairs for first credit
        public static int MenderIncrementStep = 1;              // Increment step per credit
        public static int MaxMenderPercent = 25;                // 25% total cap (matches vanilla Mender +25% so Tailor and non-Tailor end up equal)

        // Vanilla Mender trait bonus (used for cap calculations)
        // Vanilla Mender shows "+25% armor durability" (armorDurabilityLoss: -0.25). This is
        // used both for cap math (Tailor's earnable = MaxMenderPercent - 25, so total caps at
        // MaxMenderPercent like every other class) and for inline display Replace.
        public const int VANILLA_MENDER_ARMOR_DURABILITY_BONUS = 25;

        // Storage for mender progress
        public static ConcurrentDictionary<string, MenderProgressData> MenderProgress = new ConcurrentDictionary<string, MenderProgressData>();
        public static volatile bool pendingMenderProgressSave = false;

        // Durability tracking for repair detection - key is "playerUid_slotId", value is last known durability
        private static ConcurrentDictionary<string, int> TrackedItemDurabilities = new ConcurrentDictionary<string, int>();

        // Sewing kit consumption tracking - key is playerUid, value is last known sewing kit count on mouse cursor
        private static ConcurrentDictionary<string, int> TrackedSewingKitCounts = new ConcurrentDictionary<string, int>();

        // =========================================================================
        // PILFERER TRAIT - Tracks chests/vessels for loot bonuses
        // =========================================================================
        public const string PILFERER_RUSTY_GEAR_STAT_CODE = "sitPilfererRustyGear";
        public const string PILFERER_VESSEL_CONTENTS_STAT_CODE = "sitPilfererVesselContents";
        public const string PILFERER_WHOLE_VESSEL_STAT_CODE = "sitPilfererWholeVessel";
        public const string WATCHED_PILFERER_LEVEL = "sitPilfererLevel";
        public const string WATCHED_PILFERER_BONUS = "sitPilfererBonusPercent";
        // Per-stat displayed bonuses. Pilferer's three stats have different vanilla values
        // (vessel +15%, rusty gear +10%, whole vessel +12%), so a single shared bonus value
        // can't drive all three to the same cap simultaneously for Malefactor (vanilla
        // Pilferer). Tracking each stat's earned amount independently keeps every class at
        // exactly +20% per stat at maxall.
        public const string WATCHED_PILFERER_VESSEL_BONUS = "sitPilfererVesselBonus";
        public const string WATCHED_PILFERER_RUSTY_BONUS = "sitPilfererRustyBonus";
        public const string WATCHED_PILFERER_WHOLE_BONUS = "sitPilfererWholeBonus";
        public const string PILFERER_TRAIT_CODE = "sitpilferermastery";

        // Pilferer progression configuration
        public static int BasePilfererPointsPerIncrement = 10;  // Base points for first credit
        public static int PilfererIncrementStep = 10;           // Increment step per credit
        public static int MaxPilfererPercent = 20;              // 20% max bonus for all three stats
        public const int PILFERER_VESSEL_POINTS = 2;            // Points per broken loot vessel

        // Vanilla Pilferer trait bonuses (Malefactor exclusive)
        public const int VANILLA_PILFERER_RUSTY_GEAR_BONUS = 10;
        public const int VANILLA_PILFERER_VESSEL_CONTENTS_BONUS = 15;
        public const int VANILLA_PILFERER_WHOLE_VESSEL_BONUS = 12;

        // Storage for pilferer progress
        public static ConcurrentDictionary<string, PilfererProgressData> PilfererProgress = new ConcurrentDictionary<string, PilfererProgressData>();
        public static volatile bool pendingPilfererProgressSave = false;

        // =========================================================================
        // RESOURCEFUL TRAIT - Tracks animal harvesting for loot/speed bonuses
        // =========================================================================
        public const string RESOURCEFUL_LOOT_STAT_CODE = "sitResourcefulLoot";
        public const string RESOURCEFUL_SPEED_STAT_CODE = "sitResourcefulSpeed";
        public const string WATCHED_RESOURCEFUL_LEVEL = "sitResourcefulLevel";
        public const string WATCHED_RESOURCEFUL_LOOT_BONUS = "sitResourcefulLootBonusPercent";
        public const string WATCHED_RESOURCEFUL_SPEED_BONUS = "sitResourcefulSpeedBonusPercent";
        public const string RESOURCEFUL_TRAIT_CODE = "sitresourcefulmastery";

        // Resourceful progression configuration
        public static int BaseResourcefulAnimalsPerIncrement = 10;  // Base animals for first credit
        public static int ResourcefulIncrementStep = 10;            // Increment step per credit
        public static int MaxResourcefulLootPercent = 20;           // 20% max animal loot bonus
        public static int MaxResourcefulSpeedPercent = 25;          // 25% max harvesting speed bonus

        // Vanilla Resourceful trait bonuses (Hunter/Malefactor)
        public const int VANILLA_RESOURCEFUL_LOOT_BONUS = 10;
        public const int VANILLA_RESOURCEFUL_SPEED_BONUS = 25;

        // Storage for resourceful progress
        public static ConcurrentDictionary<string, ResourcefulProgressData> ResourcefulProgress = new ConcurrentDictionary<string, ResourcefulProgressData>();
        public static volatile bool pendingResourcefulProgressSave = false;

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

        // Storage for forager progress
        public static ConcurrentDictionary<string, ForagerProgressData> ForagerProgress = new ConcurrentDictionary<string, ForagerProgressData>();
        public static volatile bool pendingForagerProgressSave = false;

        // =========================================================================
        // FURTIVE TRAIT - Tracks sneaking blocks for animal detection range reduction
        // =========================================================================
        public const string FURTIVE_STAT_CODE = "sitFurtiveBonus";
        public const string WATCHED_FURTIVE_LEVEL = "sitFurtiveLevel";
        public const string WATCHED_FURTIVE_BONUS = "sitFurtiveBonusPercent";
        public const string FURTIVE_TRAIT_CODE = "sitfurtivemastery";

        // Furtive progression configuration
        public static int BaseFurtiveSneakBlocksPerIncrement = 100;  // Base sneaking blocks for first credit
        public static int FurtiveIncrementStep = 100;                 // Increment step per credit
        public static int MaxFurtivePercent = 35;                     // 35% max animal detection range reduction

        // Vanilla Furtive trait bonus (Malefactor)
        public const int VANILLA_FURTIVE_DETECTION_REDUCTION = 35;

        // Storage for furtive progress
        public static ConcurrentDictionary<string, FurtiveProgressData> FurtiveProgress = new ConcurrentDictionary<string, FurtiveProgressData>();
        public static volatile bool pendingFurtiveProgressSave = false;

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

        // Storage for precise progress
        public static ConcurrentDictionary<string, PreciseProgressData> PreciseProgress = new ConcurrentDictionary<string, PreciseProgressData>();
        public static volatile bool pendingPreciseProgressSave = false;

        // =========================================================================
        // TECHNICAL TRAIT - Unlocks after repairing translocators
        // =========================================================================
        public const string TECHNICAL_STAT_CODE = "sitTechnicalBonus";
        public const string WATCHED_TECHNICAL_UNLOCKED = "sitTechnicalUnlocked";
        public const string WATCHED_TECHNICAL_REPAIRS = "sitTechnicalRepairs";
        public const string TECHNICAL_TRAIT_CODE = "sittechnicalmastery";

        // Technical progression configuration
        public static int TechnicalRequiredTranslocatorRepairs = 5;  // Repairs needed to unlock

        // Storage for technical progress
        public static ConcurrentDictionary<string, TechnicalProgressData> TechnicalProgress = new ConcurrentDictionary<string, TechnicalProgressData>();
        public static volatile bool pendingTechnicalProgressSave = false;

        // =========================================================================
        // HARDY HEALTH TRAIT - Unlocks +5 HP after reaching mining and armor thresholds
        // =========================================================================
        public const string HARDY_HEALTH_STAT_CODE = "sitHardyHealthBonus";
        public const string WATCHED_HARDY_HEALTH_UNLOCKED = "sitHardyHealthUnlocked";
        public const string HARDY_HEALTH_TRAIT_CODE = "sithardyhealthmastery";

        // Hardy health unlock thresholds
        public static int HardyHealthMiningThreshold = 10;           // 10% mining speed bonus required (10 credits)
        public static int HardyHealthArmorDurabilityThreshold = 10;  // 10% armor durability bonus required
        public static int HardyHealthBonus = 5;                      // +5 HP bonus

        // Storage for hardy health progress
        public static ConcurrentDictionary<string, HardyHealthProgressData> HardyHealthProgress = new ConcurrentDictionary<string, HardyHealthProgressData>();
        public static volatile bool pendingHardyHealthProgressSave = false;

        // =========================================================================
        // BOWYER TRAIT - Unlocks crude bow/arrows after ranged damage + bow damage
        // =========================================================================
        public const string BOWYER_STAT_CODE = "sitBowyerBonus";
        public const string WATCHED_BOWYER_UNLOCKED = "sitBowyerUnlocked";
        public const string WATCHED_BOWYER_BOW_DAMAGE = "sitBowyerBowDamage";
        public const string BOWYER_TRAIT_CODE = "sitbowyermastery";

        // Bowyer unlock thresholds
        public static int BowyerRangedDamageThreshold = 10;          // 10% ranged damage bonus required
        public static int BowyerBowDamageThreshold = 300;            // 300 total bow damage required

        // Storage for bowyer progress
        public static ConcurrentDictionary<string, BowyerProgressData> BowyerProgress = new ConcurrentDictionary<string, BowyerProgressData>();
        public static volatile bool pendingBowyerProgressSave = false;

        // =========================================================================
        // IMPROVISER TRAIT - Unlocks sling after thrown rock damage
        // =========================================================================
        public const string IMPROVISER_STAT_CODE = "sitImproviserBonus";
        public const string WATCHED_IMPROVISER_UNLOCKED = "sitImproviserUnlocked";
        public const string WATCHED_IMPROVISER_ROCK_DAMAGE = "sitImproviserRockDamage";
        public const string IMPROVISER_TRAIT_CODE = "sitimprovisermastery";

        // Improviser unlock threshold
        public static int ImproviserRockDamageThreshold = 300;       // 300 total thrown rock damage required

        // Storage for improviser progress
        public static ConcurrentDictionary<string, ImproviserProgressData> ImproviserProgress = new ConcurrentDictionary<string, ImproviserProgressData>();
        public static volatile bool pendingImproviserProgressSave = false;

        // =========================================================================
        // TINKERER TRAIT - Unlocks tuning spear after Technical + Precise threshold
        // =========================================================================
        public const string TINKERER_STAT_CODE = "sitTinkererBonus";
        public const string WATCHED_TINKERER_UNLOCKED = "sitTinkererUnlocked";
        public const string TINKERER_TRAIT_CODE = "sittinkerermastery";

        // Tinkerer unlock threshold
        public static int TinkererPreciseThreshold = 10;              // 10% Precise damage bonus required (plus Technical)

        // Storage for tinkerer progress
        public static ConcurrentDictionary<string, TinkererProgressData> TinkererProgress = new ConcurrentDictionary<string, TinkererProgressData>();
        public static volatile bool pendingTinkererProgressSave = false;

        // =========================================================================
        // MERCILESS TRAIT - Unlocks shortsword/shield after armor + melee thresholds
        // =========================================================================
        public const string MERCILESS_STAT_CODE = "sitMercilessBonus";
        public const string WATCHED_MERCILESS_UNLOCKED = "sitMercilessUnlocked";
        public const string MERCILESS_TRAIT_CODE = "sitmercilessmastery";

        // Merciless unlock thresholds
        public static int MercilessArmorDurabilityThreshold = 10;    // 10% armor durability bonus required
        public static int MercilessMeleeDamageThreshold = 15;        // 15% melee damage bonus required

        // Storage for merciless progress
        public static ConcurrentDictionary<string, MercilessProgressData> MercilessProgress = new ConcurrentDictionary<string, MercilessProgressData>();
        public static volatile bool pendingMercilessProgressSave = false;

        // =========================================================================
        // CLAUSTROPHOBIC REMOVAL - Removes trait after reaching mining threshold (Hunter)
        // =========================================================================
        public const string WATCHED_CLAUSTROPHOBIC_REMOVED = "sitClaustrophobicRemoved";
        public const string CLAUSTROPHOBIC_REMOVED_TRAIT_CODE = "sitclaustrophobicremoved";

        // Claustrophobic removal threshold
        public static int ClaustrophobicRemovalMiningThreshold = 100;  // 100% mining speed bonus required

        public static int HeavyFootedFurtiveThreshold = 50;
        public static int HeavyFootedWalkingThreshold = 10;

        public const string WATCHED_HEAVYFOOTED_REMOVED = "sitHeavyFootedRemoved";
        public const string HEAVYFOOTED_REMOVED_TRAIT_CODE = "sitheavyfootedremoved";

        // Storage for claustrophobic removal progress
        public static ConcurrentDictionary<string, ClaustrophobicRemovalProgressData> ClaustrophobicRemovalProgress = new ConcurrentDictionary<string, ClaustrophobicRemovalProgressData>();
        public static volatile bool pendingClaustrophobicRemovalProgressSave = false;
        // Storage for heavyfooted removal progress
        public static ConcurrentDictionary<string, HeavyFootedRemovalProgressData> HeavyFootedRemovalProgress = new ConcurrentDictionary<string, HeavyFootedRemovalProgressData>();
        public static volatile bool pendingHeavyFootedRemovalProgressSave = false;

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
        public const float VANILLA_FRAIL_HP_PENALTY = 2.5f;
        public const int VANILLA_FRAIL_DISTANCE_PENALTY = 25;
        public const string WATCHED_FRAIL_HP_REMAINING = "sitFrailHpRemaining";
        public const string WATCHED_FRAIL_DISTANCE_REMAINING = "sitFrailDistanceRemaining";
        public const string FRAIL_HP_CANCEL_STAT_CODE = "sitFrailHpCancel";

        // Civil (Tailor): -10% loot from foraging
        public const int VANILLA_CIVIL_FORAGING_PENALTY = 10;
        public const string WATCHED_CIVIL_REMAINING = "sitCivilRemaining";

        // Weak (Tailor): -2 HP, -10% mining speed
        public const int VANILLA_WEAK_HP_PENALTY = 2;
        public const int VANILLA_WEAK_MINING_PENALTY = 10;
        public const string WATCHED_WEAK_HP_REMAINING = "sitWeakHpRemaining";
        public const string WATCHED_WEAK_MINING_REMAINING = "sitWeakMiningRemaining";
        public const string WEAK_HP_CANCEL_STAT_CODE = "sitWeakHpCancel";

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
        public const string WATCHED_CLAUSTROPHOBIC_ORE_REMAINING = "sitClaustrophobicOreRemaining";
        public const string WATCHED_CLAUSTROPHOBIC_MINING_REMAINING = "sitClaustrophobicMiningRemaining";

        private const string CONFIG_SAVE_KEY = "sitConfig";
        private const string CONFIG_FILE_NAME = "SeraphLeveling.json";

        /// <summary>Version stamped into the config file. 1 means the world-save blob has been folded in.</summary>
        private const int CURRENT_CONFIG_VERSION = 1;

        /// <summary>ConfigVersion read from the file this run. Zero for files written before 1.19.0.</summary>
        private static int LoadedConfigVersion = 0;

        // Vanilla Hardy trait mining speed bonus (used for cap calculations)
        public const int VANILLA_HARDY_MINING_BONUS = 10;

        // Storage for mining progress - keyed by player UID
        public static ConcurrentDictionary<string, MiningProgressData> MiningProgress = new ConcurrentDictionary<string, MiningProgressData>();

        // Lock object for persistence operations
        private static readonly object persistLock = new object();

        // Flag to indicate pending mining progress save
        public static volatile bool pendingMiningProgressSave = false;

        // Flag to indicate pending config save
        private static volatile bool pendingConfigSave = false;

        // Auto-save configuration
        public static int AutoSaveIntervalSeconds = 300;  // Default 5 minutes
        private static long autoSaveTimerId = 0;

        // Disabled skills set for quick lookup (lowercase)
        public static HashSet<string> DisabledSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
        private void DetectSacredLib(ICoreServerAPI api)
        {
            SeraphLevelingModSystem.IsSacredLibLoaded = SeraphLevelingModSystem.DetectAnySacredLib(api.ModLoader);
            if (SeraphLevelingModSystem.IsSacredLibLoaded)
            {
                if (SacredLibEnableCompat)
                {
                    api.Logger.Notification($"[SeraphLeveling] Sacred Classes mod detected. Compatibility enabled.");
                }
                else
                {
                    api.Logger.Notification($"[SeraphLeveling] Sacred Classes mod detected, but compatibility is disabled in config.");
                }
            }
            else
            {
                api.Logger.Notification($"[SeraphLeveling]Sacred Classes mod not detected. Compatibility disabled.");
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

            // Detect Combat Overhaul mod
            DetectCombatOverhaul(api);
            DetectSacredLib(api);

            // Register /trait command with subcommands
            api.ChatCommands.Create("trait")
                .WithDescription("Manage and view trait progression")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(OnTraitHelpCommand)
                .BeginSubCommand("mining")
                    .WithDescription("View your mining progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMiningCommand)
                .EndSubCommand()
                .BeginSubCommand("miningbase")
                    .WithDescription("Get or set the base blocks per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("blocks"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMiningBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("mininglevel")
                    .WithDescription("Get or set your mining level (admin only). Usage: /trait mininglevel [level] [toolname]")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"), api.ChatCommands.Parsers.OptionalWord("toolname"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMiningLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("miningmax")
                    .WithDescription("Get or set the max mining speed bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMiningMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("miningincrement")
                    .WithDescription("Get or set the increment step per credit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("step"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMiningIncrementCommand)
                .EndSubCommand()
                .BeginSubCommand("melee")
                    .WithDescription("View your melee damage progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMeleeCommand)
                .EndSubCommand()
                .BeginSubCommand("meleebase")
                    .WithDescription("Get or set the base damage per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("damage"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMeleeBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("meleelevel")
                    .WithDescription("Get or set your melee level (admin only). Usage: /trait meleelevel [level] [toolname]")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"), api.ChatCommands.Parsers.OptionalWord("toolname"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMeleeLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("meleemax")
                    .WithDescription("Get or set the max melee damage bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMeleeMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("meleeincrement")
                    .WithDescription("Get or set the melee increment step per credit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("step"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMeleeIncrementCommand)
                .EndSubCommand()
                .BeginSubCommand("ranged")
                    .WithDescription("View your ranged damage progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitRangedCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedbase")
                    .WithDescription("Get or set the base damage per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("damage"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitRangedBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedlevel")
                    .WithDescription("Get or set your ranged level (admin only). Usage: /trait rangedlevel [level] [toolname]")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"), api.ChatCommands.Parsers.OptionalWord("toolname"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitRangedLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedmax")
                    .WithDescription("Get or set the max ranged damage bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitRangedMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedmaxacc")
                    .WithDescription("Get or set the max ranged accuracy bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitRangedMaxAccuracyCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedmaxdist")
                    .WithDescription("Get or set the max ranged distance bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitRangedMaxDistanceCommand)
                .EndSubCommand()
                .BeginSubCommand("rangedincrement")
                    .WithDescription("Get or set the ranged increment step per credit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("step"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitRangedIncrementCommand)
                .EndSubCommand()
                .BeginSubCommand("walking")
                    .WithDescription("View your walking speed progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitWalkingCommand)
                .EndSubCommand()
                .BeginSubCommand("walkingbase")
                    .WithDescription("Get or set the base blocks per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("blocks"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitWalkingBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("walkinglevel")
                    .WithDescription("Get or set your walking level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(WalkingProgressData.HandleLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("walkingmax")
                    .WithDescription("Get or set the max walking speed bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(WalkingProgressData.HandleMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("walkingincrement")
                    .WithDescription("Get or set the walking increment step per credit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("step"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitWalkingIncrementCommand)
                .EndSubCommand()
                .BeginSubCommand("hunger")
                    .WithDescription("View your hunger rate progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitHungerCommand)
                .EndSubCommand()
                .BeginSubCommand("hungerbase")
                    .WithDescription("Get or set the base seconds per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("seconds"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitHungerBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("hungerlevel")
                    .WithDescription("Get or set your hunger level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitHungerLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("hungermax")
                    .WithDescription("Get or set the max hunger rate reduction percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitHungerMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("hungerincrement")
                    .WithDescription("Get or set the hunger increment step per credit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("step"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitHungerIncrementCommand)
                .EndSubCommand()
                .BeginSubCommand("armor")
                    .WithDescription("View your armor progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitArmorCommand)
                .EndSubCommand()
                .BeginSubCommand("armorlevel")
                    .WithDescription("Get or set your armor durability level (admin only). Usage: /trait armorlevel [level] [armorpiece]")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"), api.ChatCommands.Parsers.OptionalWord("armorpiece"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitArmorLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("armorwalkspeedlevel")
                    .WithDescription("Get or set your armor walk speed penalty reduction level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitArmorWalkSpeedLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("armordurabilitymax")
                    .WithDescription("Get or set the max armor durability bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitArmorDurabilityMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("armorwalkspeedmax")
                    .WithDescription("Get or set the max walk speed penalty reduction percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitArmorWalkSpeedMaxCommand)
                .EndSubCommand()
                .BeginSubCommand("armortimebase")
                    .WithDescription("Get or set the base seconds in armor per increment (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("seconds"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitArmorTimeBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("armordamagebase")
                    .WithDescription("Get or set the base damage blocked per increment (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("damage"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitArmorDamageBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("armorrepairbase")
                    .WithDescription("Get or set the base repairs per increment (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("repairs"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitArmorRepairBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("testwalkspeed")
                    .WithDescription("Apply a test walk speed modifier (admin only, use 0 to clear)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTestWalkSpeedCommand)
                .EndSubCommand()
                // Clothier trait commands
                .BeginSubCommand("clothier")
                    .WithDescription("View your clothier progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitClothierCommand)
                .EndSubCommand()
                .BeginSubCommand("clothierrequired")
                    .WithDescription("Get or set the required unique clothes to unlock sewing kit (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("count"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitClothierRequiredCommand)
                .EndSubCommand()
                .BeginSubCommand("clothierlevel")
                    .WithDescription("Get or set your clothier progress (unique clothes count) (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitClothierLevelCommand)
                .EndSubCommand()
                // Mender trait commands
                .BeginSubCommand("mender")
                    .WithDescription("View your mender progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMenderCommand)
                .EndSubCommand()
                .BeginSubCommand("menderbase")
                    .WithDescription("Get or set the base repairs per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("repairs"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMenderBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("menderlevel")
                    .WithDescription("Get or set your mender level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMenderLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("mendermax")
                    .WithDescription("Get or set the max mender bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitMenderMaxCommand)
                .EndSubCommand()
                // Pilferer trait commands
                .BeginSubCommand("pilferer")
                    .WithDescription("View your pilferer progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitPilfererCommand)
                .EndSubCommand()
                .BeginSubCommand("pilfererbase")
                    .WithDescription("Get or set the base points per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("points"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitPilfererBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("pilfererlevel")
                    .WithDescription("Get or set your pilferer level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitPilfererLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("pilferermax")
                    .WithDescription("Get or set the max pilferer bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitPilfererMaxCommand)
                .EndSubCommand()
                // Resourceful trait commands
                .BeginSubCommand("resourceful")
                    .WithDescription("View your resourceful progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitResourcefulCommand)
                .EndSubCommand()
                .BeginSubCommand("resourcefulbase")
                    .WithDescription("Get or set the base animals per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("animals"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitResourcefulBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("resourcefullevel")
                    .WithDescription("Get or set your resourceful level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitResourcefulLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("resourcefulmax")
                    .WithDescription("Get or set the max resourceful loot bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitResourcefulMaxCommand)
                .EndSubCommand()
                // Forager trait commands
                .BeginSubCommand("forager")
                    .WithDescription("View your forager progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitForagerCommand)
                .EndSubCommand()
                .BeginSubCommand("foragerbase")
                    .WithDescription("Get or set the base crops per level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("crops"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitForagerBaseCommand)
                .EndSubCommand()
                .BeginSubCommand("foragerlevel")
                    .WithDescription("Get or set your forager level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitForagerLevelCommand)
                .EndSubCommand()
                .BeginSubCommand("foragermax")
                    .WithDescription("Get or set the max forager bonus percent (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("percent"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitForagerMaxCommand)
                .EndSubCommand()
                // Furtive trait commands
                .BeginSubCommand("furtive")
                    .WithDescription("View your furtive (sneaking) progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitFurtiveCommand)
                .EndSubCommand()
                .BeginSubCommand("furtivelevel")
                    .WithDescription("Get or set your furtive level (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitFurtiveLevelCommand)
                .EndSubCommand()
                // Precise trait commands
                .BeginSubCommand("precise")
                    .WithDescription("View your precise (mechanical damage) progression stats")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitPreciseCommand)
                .EndSubCommand()
                .BeginSubCommand("preciselevel")
                    .WithDescription("Get or set your precise level (admin only). Usage: /trait preciselevel [level] [toolname]")
                    .WithArgs(api.ChatCommands.Parsers.OptionalInt("level"), api.ChatCommands.Parsers.OptionalWord("toolname"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitPreciseLevelCommand)
                .EndSubCommand()
                // Technical trait commands
                .BeginSubCommand("technical")
                    .WithDescription("View your technical trait progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTechnicalCommand)
                .EndSubCommand()
                .BeginSubCommand("technicalunlock")
                    .WithDescription("Manually unlock or lock technical trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTechnicalUnlockCommand)
                .EndSubCommand()
                // Hardy health trait commands
                .BeginSubCommand("hardyhealth")
                    .WithDescription("View your hardy health unlock progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitHardyHealthCommand)
                .EndSubCommand()
                .BeginSubCommand("hardyhealthunlock")
                    .WithDescription("Manually unlock or lock hardy health trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitHardyHealthUnlockCommand)
                .EndSubCommand()
                // Bowyer trait commands
                .BeginSubCommand("bowyer")
                    .WithDescription("View your bowyer unlock progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitBowyerCommand)
                .EndSubCommand()
                .BeginSubCommand("bowyerunlock")
                    .WithDescription("Manually unlock or lock bowyer trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitBowyerUnlockCommand)
                .EndSubCommand()
                // Improviser trait commands
                .BeginSubCommand("improviser")
                    .WithDescription("View your improviser unlock progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitImproviserCommand)
                .EndSubCommand()
                .BeginSubCommand("improviserunlock")
                    .WithDescription("Manually unlock or lock improviser trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitImproviserUnlockCommand)
                .EndSubCommand()
                // Tinkerer trait commands
                .BeginSubCommand("tinkerer")
                    .WithDescription("View your tinkerer unlock progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTinkererCommand)
                .EndSubCommand()
                .BeginSubCommand("tinkererunlock")
                    .WithDescription("Manually unlock or lock tinkerer trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitTinkererUnlockCommand)
                .EndSubCommand()
                // Merciless trait commands
                .BeginSubCommand("merciless")
                    .WithDescription("View your merciless unlock progress")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMercilessCommand)
                .EndSubCommand()
                .BeginSubCommand("mercilessunlock")
                    .WithDescription("Manually unlock or lock merciless trait (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("unlock"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitMercilessUnlockCommand)
                .EndSubCommand()
                // Claustrophobic removal commands
                .BeginSubCommand("claustrophobic")
                    .WithDescription("View your claustrophobic removal progress (Hunter only)")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitClaustrophobicCommand)
                .EndSubCommand()
                .BeginSubCommand("heavyfooted")
                    .WithDescription("View your heavyfooted removal progress (Hunter only)")
                    .RequiresPrivilege(Privilege.chat)
                    .RequiresPlayer()
                    .HandleWith(OnTraitHeavyFootedCommand)
                .EndSubCommand()
                .BeginSubCommand("claustrophobicunlock")
                    .WithDescription("Manually set claustrophobic removed status (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("removed"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitClaustrophobicUnlockCommand)
                .EndSubCommand()
                .BeginSubCommand("heavyfootedunlock")
                    .WithDescription("Manually set heavyfooted removed status (admin only)")
                    .WithArgs(api.ChatCommands.Parsers.Bool("removed"))
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .HandleWith(OnTraitHeavyFootedUnlockCommand)
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
                // Reset all config values to defaults
                .BeginSubCommand("resetconfig")
                    .WithDescription("Reset all trait config values (base, increment, max) to defaults (admin only)")
                    .RequiresPrivilege(Privilege.controlserver)
                    .HandleWith(OnTraitResetConfigCommand)
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
                .EndSubCommand()
                // Combat Overhaul proficiency commands
                .BeginSubCommand("coproficiency")
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
                .EndSubCommand()
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
                .EndSubCommand();

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
            api.Event.SaveGameLoaded += LoadMiningProgress;
            api.Event.SaveGameLoaded += LoadMeleeProgress;
            api.Event.SaveGameLoaded += LoadRangedProgress;
            api.Event.SaveGameLoaded += LoadWalkingProgress;
            api.Event.SaveGameLoaded += LoadHungerProgress;
            api.Event.SaveGameLoaded += LoadArmorProgress;
            api.Event.SaveGameLoaded += LoadClothierProgress;
            api.Event.SaveGameLoaded += LoadMenderProgress;
            api.Event.SaveGameLoaded += LoadPilfererProgress;
            api.Event.SaveGameLoaded += LoadResourcefulProgress;
            api.Event.SaveGameLoaded += LoadForagerProgress;
            api.Event.SaveGameLoaded += LoadFurtiveProgress;
            api.Event.SaveGameLoaded += LoadPreciseProgress;
            api.Event.SaveGameLoaded += LoadTechnicalProgress;
            api.Event.SaveGameLoaded += LoadHardyHealthProgress;
            api.Event.SaveGameLoaded += LoadBowyerProgress;
            api.Event.SaveGameLoaded += LoadImproviserProgress;
            api.Event.SaveGameLoaded += LoadTinkererProgress;
            api.Event.SaveGameLoaded += LoadMercilessProgress;
            api.Event.SaveGameLoaded += LoadClaustrophobicRemovalProgress;
            api.Event.SaveGameLoaded += LoadHeavyFootedRemovalProgress;
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
        /// Handler for /trait command (shows help).
        /// </summary>
        private TextCommandResult OnTraitHelpCommand(TextCommandCallingArgs args)
        {
            return TextCommandResult.Success(
                "Usage:\n" +
                "  /trait mining - View your mining progression stats\n" +
                "  /trait miningbase [value] - Get or set base points for first credit (admin)\n" +
                "  /trait miningincrement [value] - Get or set increment step per credit (admin)\n" +
                "  /trait mininglevel [level] [toolname] - Get or set your mining level (admin)\n" +
                "  /trait miningmax [percent] - Get or set max mining speed bonus (admin)\n" +
                "  /trait melee - View your melee damage progression stats\n" +
                "  /trait meleebase [value] - Get or set base damage for first credit (admin)\n" +
                "  /trait meleeincrement [value] - Get or set melee increment step per credit (admin)\n" +
                "  /trait meleelevel [level] [toolname] - Get or set your melee level (admin)\n" +
                "  /trait meleemax [percent] - Get or set max melee damage bonus (admin)\n" +
                "  /trait ranged - View your ranged damage progression stats\n" +
                "  /trait rangedbase [value] - Get or set base damage for first credit (admin)\n" +
                "  /trait rangedincrement [value] - Get or set ranged increment step per credit (admin)\n" +
                "  /trait rangedlevel [level] [toolname] - Get or set your ranged level (admin)\n" +
                "  /trait rangedmax [percent] - Get or set max ranged damage bonus (admin)\n" +
                "  /trait rangedmaxacc [percent] - Get or set max ranged accuracy bonus (admin)\n" +
                "  /trait rangedmaxdist [percent] - Get or set max ranged distance bonus (admin)\n" +
                "  /trait walking - View your walking speed progression stats\n" +
                "  /trait walkingbase [value] - Get or set base blocks for first credit (admin)\n" +
                "  /trait walkingincrement [value] - Get or set walking increment step per credit (admin)\n" +
                "  /trait walkinglevel [level] - Get or set your walking level (admin)\n" +
                "  /trait walkingmax [percent] - Get or set max walking speed bonus (admin)\n" +
                "  /trait hunger - View your hunger rate progression stats\n" +
                "  /trait hungerbase [value] - Get or set base seconds for first credit (admin)\n" +
                "  /trait hungerincrement [value] - Get or set hunger increment step per credit (admin)\n" +
                "  /trait hungerlevel [level] - Get or set your hunger level (admin)\n" +
                "  /trait hungermax [percent] - Get or set max hunger rate reduction (admin)\n" +
                "  /trait armor - View your armor progression stats\n" +
                "  /trait armorlevel [level] [armorpiece] - Get or set your armor durability level (admin)\n" +
                "  /trait armorwalkspeedlevel [level] - Get or set walk speed penalty reduction level (admin)\n" +
                "  /trait armordurabilitymax [percent] - Get or set max durability bonus (admin)\n" +
                "  /trait armorwalkspeedmax [percent] - Get or set max walk speed reduction (admin)\n" +
                "  /trait all - View all trait progression at once\n" +
                "  /trait soundvolume [0.0-1.0] - Get or set the level-up ding volume (admin)\n" +
                "  /trait testsound [0.0-1.0] - Play the level-up ding once for testing (admin)\n" +
                "  /trait setplayer &lt;name&gt; &lt;trait&gt; &lt;level&gt; [toolname] - Set trait level for another player (admin)\n" +
                "  /trait reset - Reset all trait progression to 0 (admin)\n" +
                "  /trait resetconfig - Reset all config values to defaults (admin)\n" +
                "  /trait reloadconfig - Re-read ModConfig/SeraphLeveling.json without restarting (admin)\n" +
                "  /trait verify - Show what each stat this mod writes is actually made of (admin)\n" +
                "  /trait maxall - Set all trait progression to maximum for testing (admin)");
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

            // Progression traits
            var miningProg = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            int miningMax = GetMaxMiningCredits(player.Entity);
            sb.AppendLine($"Mining: {miningProg.TotalCredits}/{miningMax} (+{CalculateMiningBonusPercent(miningProg.TotalCredits)}% speed)");

            var meleeProg = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
            sb.AppendLine($"Melee: {meleeProg.TotalCredits}/{MaxMeleeDamagePercent} (+{meleeProg.TotalCredits}% damage)");

            var rangedProg = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
            sb.AppendLine($"Ranged: {rangedProg.TotalCredits}/{MaxRangedDamagePercent} (+{rangedProg.TotalCredits}% dmg, +{rangedProg.TotalCredits}% acc, +{rangedProg.TotalCredits}% dist)");

            WalkingProgressData.GetTraitAllCommandLine(player, sb);

            var hungerProg = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData { CurrentIncrementSize = BaseSecondsPerIncrement });
            sb.AppendLine($"Hunger: {hungerProg.TotalCredits}/{MaxHungerReductionPercent} (-{hungerProg.TotalCredits}% hunger rate)");

            var armorProg = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            sb.AppendLine($"Armor: +{armorProg.TotalDurabilityCredits}/{MaxArmorDurabilityPercent}% durability, -{armorProg.TotalWalkSpeedCredits}/{MaxArmorWalkSpeedPercent}% walk penalty");

            var menderProg = MenderProgress.GetOrAdd(playerUid, _ => new MenderProgressData { CurrentIncrementSize = BaseMenderRepairsPerIncrement });
            sb.AppendLine($"Mender: {menderProg.TotalCredits}/{MaxMenderPercent} (+{menderProg.TotalCredits}% repair bonus)");

            var pilfererProg = PilfererProgress.GetOrAdd(playerUid, _ => new PilfererProgressData { CurrentIncrementSize = BasePilfererPointsPerIncrement });
            sb.AppendLine($"Pilferer: {pilfererProg.TotalCredits}/{MaxPilfererPercent} (+{pilfererProg.TotalCredits}% vessel loot)");

            var resourcefulProg = ResourcefulProgress.GetOrAdd(playerUid, _ => new ResourcefulProgressData { CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement });
            sb.AppendLine($"Resourceful: {resourcefulProg.TotalCredits}/{MaxResourcefulLootPercent} (+{resourcefulProg.TotalCredits}% animal loot)");

            var foragerProg = ForagerProgress.GetOrAdd(playerUid, _ => new ForagerProgressData { CurrentIncrementSize = BaseForagerCropsPerIncrement });
            sb.AppendLine($"Forager: {foragerProg.TotalCredits}/{MaxForagerLootPercent} (+{foragerProg.TotalCredits}% foraging loot)");

            var furtiveProg = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData { CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement });
            sb.AppendLine($"Furtive: {furtiveProg.TotalCredits}/{MaxFurtivePercent} (-{furtiveProg.TotalCredits}% detection range)");

            var preciseProg = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());
            sb.AppendLine($"Precise: {preciseProg.TotalCredits}/{MaxPrecisePercent} (+{preciseProg.TotalCredits}% mechanical dmg)");

            // Unlock traits
            sb.AppendLine("\n--- Unlock Traits ---");

            var clothierProg = ClothierProgress.GetOrAdd(playerUid, _ => new ClothierProgressData());
            sb.AppendLine($"Clothier: {clothierProg.UniqueClothesWorn.Count}/{ClothierRequiredUniqueClothes} clothes ({(clothierProg.SewingKitUnlocked ? "UNLOCKED" : "locked")})");

            var technicalProg = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            sb.AppendLine($"Technical: {technicalProg.TranslocatorsRepaired}/{TechnicalRequiredTranslocatorRepairs} translocators ({(technicalProg.IsUnlocked ? "UNLOCKED" : "locked")})");

            var hardyHealthProg = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());
            sb.AppendLine($"Hardy Health: {(hardyHealthProg.IsUnlocked ? "UNLOCKED" : "locked")}");

            var bowyerProg = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());
            sb.AppendLine($"Bowyer: {(bowyerProg.IsUnlocked ? "UNLOCKED" : $"{bowyerProg.TotalBowDamage:F0} bow damage (locked)")}");

            var improviserProg = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());
            sb.AppendLine($"Improviser: {(improviserProg.IsUnlocked ? "UNLOCKED" : $"{improviserProg.TotalRockDamage:F0} rock damage (locked)")}");

            var tinkererProg = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());
            sb.AppendLine($"Tinkerer: {(tinkererProg.IsUnlocked ? "UNLOCKED" : "locked")}");

            var mercilessProg = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());
            sb.AppendLine($"Merciless: {(mercilessProg.IsUnlocked ? "UNLOCKED" : "locked")}");

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
        /// Handler for /trait setplayer command. Sets a trait level for a target player.
        /// Usage: /trait setplayer PlayerName trait level
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

            // Traits with per-tool support delegate to shared helpers
            switch (traitName)
            {
                case "mining":
                    return SetMiningLevelForPlayer(targetPlayer, level, toolName);
                case "melee":
                    return SetMeleeLevelForPlayer(targetPlayer, level, toolName);
                case "ranged":
                    return SetRangedLevelForPlayer(targetPlayer, level, toolName);
                case "precise":
                    return SetPreciseLevelForPlayer(targetPlayer, level, toolName);
                case "armor":
                    return SetArmorLevelForPlayer(targetPlayer, level, toolName);
            }

            // Traits without per-tool support — reject toolName if provided
            if (toolName != null)
                return TextCommandResult.Error($"The '{traitName}' trait does not support per-tool level setting.");

            string result;

            switch (traitName)
            {
                case "walking":
                {
                    return WalkingProgressData.SetLevel(targetPlayer, level);
                }
                case "hunger":
                {
                    if (level > MaxHungerReductionPercent) return TextCommandResult.Error($"Level cannot exceed max ({MaxHungerReductionPercent}).");
                    var progress = HungerProgress.GetOrAdd(targetUid, _ => new HungerProgressData { CurrentIncrementSize = BaseSecondsPerIncrement });
                    progress.TotalCredits = level;
                    pendingHungerProgressSave = true;
                    ApplyHungerBonusStatic(targetPlayer, level);
                    UpdateSkillActivityDay(targetUid, "hunger");
                    result = $"Hunger level set to {level} (-{level}% hunger rate) for {targetPlayer.PlayerName}.";
                    break;
                }
                case "mender":
                {
                    if (level > MaxMenderPercent) return TextCommandResult.Error($"Level cannot exceed max ({MaxMenderPercent}).");
                    var progress = MenderProgress.GetOrAdd(targetUid, _ => new MenderProgressData { CurrentIncrementSize = BaseMenderRepairsPerIncrement });
                    progress.TotalCredits = level;
                    pendingMenderProgressSave = true;
                    ApplyMenderBonusStatic(targetPlayer, level);
                    UpdateSkillActivityDay(targetUid, "mender");
                    result = $"Mender level set to {level} (+{level}% repair) for {targetPlayer.PlayerName}.";
                    break;
                }
                case "pilferer":
                {
                    if (level > MaxPilfererPercent) return TextCommandResult.Error($"Level cannot exceed max ({MaxPilfererPercent}).");
                    var progress = PilfererProgress.GetOrAdd(targetUid, _ => new PilfererProgressData { CurrentIncrementSize = BasePilfererPointsPerIncrement });
                    progress.TotalCredits = level;
                    pendingPilfererProgressSave = true;
                    ApplyPilfererBonusStatic(targetPlayer, level);
                    UpdateSkillActivityDay(targetUid, "pilferer");
                    result = $"Pilferer level set to {level} for {targetPlayer.PlayerName}.";
                    break;
                }
                case "resourceful":
                {
                    if (level > MaxResourcefulLootPercent) return TextCommandResult.Error($"Level cannot exceed max ({MaxResourcefulLootPercent}).");
                    var progress = ResourcefulProgress.GetOrAdd(targetUid, _ => new ResourcefulProgressData { CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement });
                    progress.TotalCredits = level;
                    pendingResourcefulProgressSave = true;
                    ApplyResourcefulBonusStatic(targetPlayer, level);
                    UpdateSkillActivityDay(targetUid, "resourceful");
                    result = $"Resourceful level set to {level} for {targetPlayer.PlayerName}.";
                    break;
                }
                case "forager":
                {
                    if (level > MaxForagerLootPercent) return TextCommandResult.Error($"Level cannot exceed max ({MaxForagerLootPercent}).");
                    var progress = ForagerProgress.GetOrAdd(targetUid, _ => new ForagerProgressData { CurrentIncrementSize = BaseForagerCropsPerIncrement });
                    progress.TotalCredits = level;
                    pendingForagerProgressSave = true;
                    ApplyForagerBonusStatic(targetPlayer, level);
                    UpdateSkillActivityDay(targetUid, "forager");
                    result = $"Forager level set to {level} for {targetPlayer.PlayerName}.";
                    break;
                }
                case "furtive":
                {
                    if (level > MaxFurtivePercent) return TextCommandResult.Error($"Level cannot exceed max ({MaxFurtivePercent}).");
                    var progress = FurtiveProgress.GetOrAdd(targetUid, _ => new FurtiveProgressData { CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement });
                    progress.TotalCredits = level;
                    pendingFurtiveProgressSave = true;
                    ApplyFurtiveBonusStatic(targetPlayer, level);
                    UpdateSkillActivityDay(targetUid, "furtive");
                    result = $"Furtive level set to {level} (-{level}% detection) for {targetPlayer.PlayerName}.";
                    break;
                }
                default:
                    return TextCommandResult.Error($"Unknown trait '{traitName}'. Valid traits: mining, melee, ranged, walking, hunger, armor, mender, pilferer, resourceful, forager, furtive, precise");
            }

            return TextCommandResult.Success(result);
        }

        /// <summary>
        /// Handler for /trait mining command.
        /// </summary>
        private TextCommandResult OnTraitMiningCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());

            int currentCredits = progress.TotalCredits;
            int bonusPercent = CalculateMiningBonusPercent(currentCredits);
            int maxCredits = GetMaxMiningCredits(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Mining progression: {currentCredits}% / {maxCredits}%");
            sb.AppendLine($"Current bonus: +{bonusPercent}% mining speed");

            if (progress.PickaxeProgress.Count > 0)
            {
                sb.AppendLine("\nPer-pickaxe progress:");
                foreach (var kvp in progress.PickaxeProgress.OrderBy(p => p.Value.CurrentIncrementSize))
                {
                    string pickaxeName = kvp.Key;
                    // Simplify the display name (remove "game:" prefix if present)
                    if (pickaxeName.StartsWith("game:"))
                        pickaxeName = pickaxeName.Substring(5);

                    var pickProgress = kvp.Value;
                    sb.AppendLine($"  {pickaxeName}: {pickProgress.BlocksInIncrement}/{pickProgress.CurrentIncrementSize} points");
                }
            }
            else
            {
                sb.AppendLine("\nNo pickaxe progress yet. Mine stone or ore with a pickaxe to start!");
            }

            if (currentCredits >= maxCredits)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait miningbase command.
        /// Sets the base points needed for the first 1% increment.
        /// </summary>
        private TextCommandResult OnTraitMiningBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base blocks per increment must be at least 1");
                }

                BaseBlocksPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base blocks per increment set to {BaseBlocksPerIncrement}. New pickaxes will require this many points for first 1%.");
            }
            else
            {
                return TextCommandResult.Success($"Current base blocks per increment: {BaseBlocksPerIncrement}\nIncrement step: +{IncrementStep} per credit");
            }
        }

        /// <summary>
        /// Handler for /trait miningincrement command.
        /// Sets how many additional points are required for each subsequent credit.
        /// </summary>
        private TextCommandResult OnTraitMiningIncrementCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 0)
                {
                    return TextCommandResult.Error("Increment step cannot be negative");
                }

                IncrementStep = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Increment step set to +{IncrementStep} per credit.\nProgression: {BaseBlocksPerIncrement}, {BaseBlocksPerIncrement + IncrementStep}, {BaseBlocksPerIncrement + IncrementStep * 2}...");
            }
            else
            {
                return TextCommandResult.Success($"Current increment step: +{IncrementStep} per credit\nProgression: {BaseBlocksPerIncrement}, {BaseBlocksPerIncrement + IncrementStep}, {BaseBlocksPerIncrement + IncrementStep * 2}...");
            }
        }

        /// <summary>
        /// Calculates credits earned by a tool from its CurrentIncrementSize.
        /// Credits = (currentIncrementSize - baseIncrement) / incrementStep
        /// </summary>
        private static int CalculateToolCredits(int currentIncrementSize, int baseIncrement, int incrementStep)
        {
            if (incrementStep <= 0) return 0;
            int credits = (currentIncrementSize - baseIncrement) / incrementStep;
            return Math.Max(0, credits);
        }

        /// <summary>
        /// Recalculates TotalCredits by summing credits from all per-tool entries.
        /// </summary>
        private static int RecalculateTotalCreditsFromTools<T>(
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
        /// Sets per-tool credits for mining. Returns the result or null if the caller should proceed with total-level setting.
        /// </summary>
        private TextCommandResult SetMiningLevelForPlayer(IServerPlayer player, int level, string toolName)
        {
            string playerUid = player.PlayerUID;
            int maxCredits = GetMaxMiningCredits(player.Entity);
            var progress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());

            if (level < 0)
                return TextCommandResult.Error("Credits cannot be negative.");

            if (toolName != null)
            {
                // Per-tool mode: set credits on a specific pickaxe without clearing others
                int oldToolCredits = 0;
                if (progress.PickaxeProgress.TryGetValue(toolName, out var existingTool))
                    oldToolCredits = CalculateToolCredits(existingTool.CurrentIncrementSize, BaseBlocksPerIncrement, IncrementStep);

                int projectedTotal = progress.TotalCredits - oldToolCredits + level;
                if (projectedTotal > maxCredits)
                    return TextCommandResult.Error($"Setting {level} credits on {toolName} would result in {projectedTotal} total credits, exceeding max ({maxCredits}).");

                if (level == 0)
                {
                    progress.PickaxeProgress.Remove(toolName);
                }
                else
                {
                    var pickaxeProgress = progress.GetPickaxeProgress(toolName);
                    pickaxeProgress.CurrentIncrementSize = BaseBlocksPerIncrement + (level * IncrementStep);
                    pickaxeProgress.BlocksInIncrement = 0;
                }

                progress.TotalCredits = RecalculateTotalCreditsFromTools(
                    progress.PickaxeProgress, p => p.CurrentIncrementSize,
                    BaseBlocksPerIncrement, IncrementStep);

                pendingMiningProgressSave = true;
                int bonusPercent = ApplyMiningBonus(player, progress.TotalCredits);
                CheckHardyHealthUnlock(player);
                CheckClaustrophobicRemoval(player);
                UpdateSkillActivityDay(playerUid, "mining");

                return TextCommandResult.Success($"Set {level} credits on {toolName}. Total: {progress.TotalCredits}/{maxCredits} (+{bonusPercent}% mining speed).");
            }
            else
            {
                // Total mode: set TotalCredits directly and clear per-tool progress
                if (level > maxCredits)
                    return TextCommandResult.Error($"Credits cannot exceed max ({maxCredits}).");

                progress.TotalCredits = level;
                progress.PickaxeProgress.Clear();

                pendingMiningProgressSave = true;
                int bonusPercent = ApplyMiningBonus(player, level);
                CheckHardyHealthUnlock(player);
                CheckClaustrophobicRemoval(player);
                UpdateSkillActivityDay(playerUid, "mining");

                return TextCommandResult.Success($"Mining credits set to {level} (+{bonusPercent}% mining speed). Per-pickaxe progress reset.");
            }
        }

        /// <summary>
        /// Sets per-tool credits for melee.
        /// </summary>
        private TextCommandResult SetMeleeLevelForPlayer(IServerPlayer player, int level, string toolName)
        {
            string playerUid = player.PlayerUID;
            int maxCredits = GetMaxMeleeCredits(player.Entity);
            var progress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());

            if (level < 0)
                return TextCommandResult.Error("Credits cannot be negative.");

            if (toolName != null)
            {
                int oldToolCredits = 0;
                if (progress.WeaponProgress.TryGetValue(toolName, out var existingTool))
                    oldToolCredits = CalculateToolCredits(existingTool.CurrentIncrementSize, BaseDamagePerIncrement, MeleeIncrementStep);

                int projectedTotal = progress.TotalCredits - oldToolCredits + level;
                if (projectedTotal > maxCredits)
                    return TextCommandResult.Error($"Setting {level} credits on {toolName} would result in {projectedTotal} total credits, exceeding max ({maxCredits}).");

                if (level == 0)
                {
                    progress.WeaponProgress.Remove(toolName);
                }
                else
                {
                    var weaponProgress = progress.GetWeaponProgress(toolName);
                    weaponProgress.CurrentIncrementSize = BaseDamagePerIncrement + (level * MeleeIncrementStep);
                    weaponProgress.DamageInIncrement = 0;
                }

                progress.TotalCredits = RecalculateTotalCreditsFromTools(
                    progress.WeaponProgress, w => w.CurrentIncrementSize,
                    BaseDamagePerIncrement, MeleeIncrementStep);

                pendingMeleeProgressSave = true;
                int bonusPercent = ApplyMeleeBonusStatic(player, progress.TotalCredits);
                CheckMercilessUnlock(player);
                UpdateSkillActivityDay(playerUid, "melee");

                return TextCommandResult.Success($"Set {level} credits on {toolName}. Total: {progress.TotalCredits}/{maxCredits} (+{bonusPercent}% melee damage).");
            }
            else
            {
                if (level > maxCredits)
                    return TextCommandResult.Error($"Credits cannot exceed max ({maxCredits}).");

                progress.TotalCredits = level;
                progress.WeaponProgress.Clear();

                pendingMeleeProgressSave = true;
                int bonusPercent = ApplyMeleeBonusStatic(player, level);
                CheckMercilessUnlock(player);
                UpdateSkillActivityDay(playerUid, "melee");

                return TextCommandResult.Success($"Melee credits set to {level} (+{bonusPercent}% melee damage). Per-weapon progress reset.");
            }
        }

        /// <summary>
        /// Sets per-tool credits for ranged.
        /// </summary>
        private TextCommandResult SetRangedLevelForPlayer(IServerPlayer player, int level, string toolName)
        {
            string playerUid = player.PlayerUID;
            int maxCredits = GetMaxRangedCredits(player.Entity);
            var progress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());

            if (level < 0)
                return TextCommandResult.Error("Credits cannot be negative.");

            if (toolName != null)
            {
                int oldToolCredits = 0;
                if (progress.WeaponProgress.TryGetValue(toolName, out var existingTool))
                    oldToolCredits = CalculateToolCredits(existingTool.CurrentIncrementSize, BaseRangedDamagePerIncrement, RangedIncrementStep);

                int projectedTotal = progress.TotalCredits - oldToolCredits + level;
                if (projectedTotal > maxCredits)
                    return TextCommandResult.Error($"Setting {level} credits on {toolName} would result in {projectedTotal} total credits, exceeding max ({maxCredits}).");

                if (level == 0)
                {
                    progress.WeaponProgress.Remove(toolName);
                }
                else
                {
                    var weaponProgress = progress.GetWeaponProgress(toolName);
                    weaponProgress.CurrentIncrementSize = BaseRangedDamagePerIncrement + (level * RangedIncrementStep);
                    weaponProgress.DamageInIncrement = 0;
                }

                progress.TotalCredits = RecalculateTotalCreditsFromTools(
                    progress.WeaponProgress, w => w.CurrentIncrementSize,
                    BaseRangedDamagePerIncrement, RangedIncrementStep);

                pendingRangedProgressSave = true;
                var (dmg, acc, dist) = ApplyRangedBonusStatic(player, progress.TotalCredits);
                CheckBowyerUnlock(player);
                CheckImproviserUnlock(player);
                UpdateSkillActivityDay(playerUid, "ranged");

                return TextCommandResult.Success($"Set {level} credits on {toolName}. Total: {progress.TotalCredits}/{maxCredits} (+{dmg}% damage, +{acc}% accuracy, +{dist}% distance).");
            }
            else
            {
                if (level > maxCredits)
                    return TextCommandResult.Error($"Credits cannot exceed max ({maxCredits}).");

                progress.TotalCredits = level;
                progress.WeaponProgress.Clear();

                pendingRangedProgressSave = true;
                var (dmg, acc, dist) = ApplyRangedBonusStatic(player, level);
                CheckBowyerUnlock(player);
                CheckImproviserUnlock(player);
                UpdateSkillActivityDay(playerUid, "ranged");

                return TextCommandResult.Success($"Ranged credits set to {level} (+{dmg}% damage, +{acc}% accuracy, +{dist}% distance). Per-weapon progress reset.");
            }
        }

        /// <summary>
        /// Sets per-tool credits for precise.
        /// </summary>
        private TextCommandResult SetPreciseLevelForPlayer(IServerPlayer player, int level, string toolName)
        {
            string playerUid = player.PlayerUID;
            var progress = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());

            if (level < 0 || level > MaxPrecisePercent)
                return TextCommandResult.Error($"Level must be between 0 and {MaxPrecisePercent}.");

            if (toolName != null)
            {
                int oldToolCredits = 0;
                if (progress.WeaponProgress.TryGetValue(toolName, out var existingTool))
                    oldToolCredits = CalculateToolCredits(existingTool.CurrentIncrementSize, BasePreciseDamagePerIncrement, PreciseIncrementStep);

                int projectedTotal = progress.TotalCredits - oldToolCredits + level;
                if (projectedTotal > MaxPrecisePercent)
                    return TextCommandResult.Error($"Setting {level} credits on {toolName} would result in {projectedTotal} total credits, exceeding max ({MaxPrecisePercent}).");

                if (level == 0)
                {
                    progress.WeaponProgress.Remove(toolName);
                }
                else
                {
                    var weaponProgress = progress.GetWeaponProgress(toolName);
                    weaponProgress.CurrentIncrementSize = BasePreciseDamagePerIncrement + (level * PreciseIncrementStep);
                    weaponProgress.DamageInIncrement = 0;
                }

                progress.TotalCredits = RecalculateTotalCreditsFromTools(
                    progress.WeaponProgress, w => w.CurrentIncrementSize,
                    BasePreciseDamagePerIncrement, PreciseIncrementStep);

                pendingPreciseProgressSave = true;
                int bonusPercent = ApplyPreciseBonusStatic(player, progress.TotalCredits);
                CheckTinkererUnlock(player);
                UpdateSkillActivityDay(playerUid, "precise");

                return TextCommandResult.Success($"Set {level} credits on {toolName}. Total: {progress.TotalCredits}/{MaxPrecisePercent} (+{bonusPercent}% mechanical damage).");
            }
            else
            {
                progress.TotalCredits = level;
                progress.WeaponProgress.Clear();

                pendingPreciseProgressSave = true;
                int bonusPercent = ApplyPreciseBonusStatic(player, level);
                CheckTinkererUnlock(player);
                UpdateSkillActivityDay(playerUid, "precise");

                return TextCommandResult.Success($"Precise level set to {level} (+{bonusPercent}% mechanical damage).");
            }
        }

        /// <summary>
        /// Sets per-tool credits for armor durability.
        /// Armor has 3 credit streams (time, damage, repair) per piece, so per-piece setting
        /// distributes credits equally across all 3 streams for the specified armor piece.
        /// </summary>
        private TextCommandResult SetArmorLevelForPlayer(IServerPlayer player, int level, string toolName)
        {
            string playerUid = player.PlayerUID;
            var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            if (level < 0)
                return TextCommandResult.Error("Credits cannot be negative.");

            if (toolName != null)
            {
                // Per-piece mode: set durability credits on a specific armor piece.
                // Each credit stream (time, damage, repair) for this piece is set to the given level.
                // TotalDurabilityCredits = sum of all per-piece (time + damage + repair) credits.
                int oldPieceCredits = 0;
                if (progress.ArmorProgress.TryGetValue(toolName, out var existingPiece))
                {
                    oldPieceCredits += CalculateToolCredits(existingPiece.CurrentTimeIncrementSize, BaseSecondsInArmorPerIncrement, ArmorTimeIncrementStep);
                    oldPieceCredits += CalculateToolCredits(existingPiece.CurrentDamageIncrementSize, BaseDamageBlockedPerIncrement, ArmorDamageIncrementStep);
                    oldPieceCredits += CalculateToolCredits(existingPiece.CurrentRepairIncrementSize, BaseRepairsPerIncrement, ArmorRepairIncrementStep);
                }

                // Each of the 3 streams gets 'level' credits, so total piece contribution is level * 3
                int newPieceCredits = level * 3;
                int projectedTotal = progress.TotalDurabilityCredits - oldPieceCredits + newPieceCredits;
                if (projectedTotal > MaxArmorDurabilityPercent)
                    return TextCommandResult.Error($"Setting {level} credits per stream on {toolName} would result in {projectedTotal} total durability credits, exceeding max ({MaxArmorDurabilityPercent}).");

                if (level == 0)
                {
                    progress.ArmorProgress.TryRemove(toolName, out var _);
                }
                else
                {
                    var pieceProgress = progress.GetArmorProgress(toolName);
                    pieceProgress.CurrentTimeIncrementSize = BaseSecondsInArmorPerIncrement + (level * ArmorTimeIncrementStep);
                    pieceProgress.SecondsWornInIncrement = 0;
                    pieceProgress.TimeCredits = level;
                    pieceProgress.CurrentDamageIncrementSize = BaseDamageBlockedPerIncrement + (level * ArmorDamageIncrementStep);
                    pieceProgress.DamageBlockedInIncrement = 0;
                    pieceProgress.DamageCredits = level;
                    pieceProgress.CurrentRepairIncrementSize = BaseRepairsPerIncrement + (level * ArmorRepairIncrementStep);
                    pieceProgress.RepairsInIncrement = 0;
                    pieceProgress.RepairCredits = level;
                    pieceProgress.HasBeenEquipped = true;
                }

                // Recalculate total durability credits from all armor pieces
                int total = 0;
                foreach (var kvp in progress.ArmorProgress)
                {
                    total += CalculateToolCredits(kvp.Value.CurrentTimeIncrementSize, BaseSecondsInArmorPerIncrement, ArmorTimeIncrementStep);
                    total += CalculateToolCredits(kvp.Value.CurrentDamageIncrementSize, BaseDamageBlockedPerIncrement, ArmorDamageIncrementStep);
                    total += CalculateToolCredits(kvp.Value.CurrentRepairIncrementSize, BaseRepairsPerIncrement, ArmorRepairIncrementStep);
                }
                progress.TotalDurabilityCredits = total;

                pendingArmorProgressSave = true;
                ApplyArmorBonusesStatic(player, progress.TotalDurabilityCredits, progress.TotalWalkSpeedCredits);
                int bonusPercent = CalculateArmorDurabilityBonusPercent(progress.TotalDurabilityCredits, player.Entity);
                CheckHardyHealthUnlock(player);
                CheckMercilessUnlock(player);
                UpdateSkillActivityDay(playerUid, "armor");

                return TextCommandResult.Success($"Set {level} credits per stream on {toolName}. Total durability: {progress.TotalDurabilityCredits}/{MaxArmorDurabilityPercent} (+{bonusPercent}% durability).");
            }
            else
            {
                if (level > MaxArmorDurabilityPercent)
                    return TextCommandResult.Error($"Credits cannot exceed max ({MaxArmorDurabilityPercent}).");

                progress.TotalDurabilityCredits = level;
                pendingArmorProgressSave = true;
                ApplyArmorBonusesStatic(player, progress.TotalDurabilityCredits, progress.TotalWalkSpeedCredits);
                int bonusPercent = CalculateArmorDurabilityBonusPercent(level, player.Entity);
                CheckHardyHealthUnlock(player);
                CheckMercilessUnlock(player);
                UpdateSkillActivityDay(playerUid, "armor");

                return TextCommandResult.Success($"Armor durability credits set to {level} (+{bonusPercent}% durability).");
            }
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
        /// Handler for /trait mininglevel command.
        /// Gets or sets the player's mining credits (level) directly.
        /// Optionally specify a tool name to set credits on a specific pickaxe without clearing other progress.
        /// </summary>
        private TextCommandResult OnTraitMiningLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            int? newCredits = (int?)args[0];

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                int maxCredits = GetMaxMiningCredits(player.Entity);
                var progress = MiningProgress.GetOrAdd(player.PlayerUID, _ => new MiningProgressData());
                int currentBonus = CalculateMiningBonusPercent(progress.TotalCredits);
                return TextCommandResult.Success($"Current mining level: {progress.TotalCredits}/{maxCredits} (+{currentBonus}% mining speed)");
            }

            string toolName = (string)args[1];
            return SetMiningLevelForPlayer(player, newCredits.Value, toolName);
        }

        /// <summary>
        /// Gets the pickaxe code from the player's held item, or null if not holding a pickaxe.
        /// </summary>
        private string GetHeldPickaxeCode(IServerPlayer player)
        {
            if (player?.Entity == null) return null;

            var heldItem = player.Entity.RightHandItemSlot?.Itemstack?.Collectible;
            if (heldItem == null) return null;

            // Check if it's a pickaxe (Tool property = Pickaxe)
            if (heldItem.Tool != EnumTool.Pickaxe) return null;

            // Return the item code as the pickaxe identifier
            return heldItem.Code?.ToString();
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
        private int GetBlockPoints(int blockId)
        {
            if (ServerApi == null) return 0;

            var block = ServerApi.World.GetBlock(blockId);
            if (block == null) return 0;

            string blockCode = block.Code?.ToString() ?? "";

            // Remove "game:" prefix if present for consistent matching
            string codeToCheck = blockCode.StartsWith("game:") ? blockCode.Substring(5) : blockCode;

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
        /// Handler for /trait miningmax command.
        /// Gets or sets the maximum mining speed bonus percent.
        /// </summary>
        private TextCommandResult OnTraitMiningMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max mining speed percent must be at least 1");
                }

                MaxMiningSpeedPercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
                    ApplyMiningBonus(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Max mining speed bonus set to +{MaxMiningSpeedPercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max mining speed bonus: +{MaxMiningSpeedPercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait melee command.
        /// </summary>
        private TextCommandResult OnTraitMeleeCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());

            int currentCredits = progress.TotalCredits;
            int bonusPercent = CalculateMeleeBonusPercent(currentCredits);
            int maxCredits = GetMaxMeleeCredits(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Melee progression: {currentCredits}% / {maxCredits}%");
            sb.AppendLine($"Current bonus: +{bonusPercent}% melee damage");

            if (progress.WeaponProgress.Count > 0)
            {
                sb.AppendLine("\nPer-weapon progress:");
                foreach (var kvp in progress.WeaponProgress.OrderBy(p => p.Value.CurrentIncrementSize))
                {
                    string weaponName = kvp.Key;
                    // Simplify the display name (remove "game:" prefix if present)
                    if (weaponName.StartsWith("game:"))
                        weaponName = weaponName.Substring(5);

                    var weaponProgress = kvp.Value;
                    sb.AppendLine($"  {weaponName}: {weaponProgress.DamageInIncrement:F1}/{weaponProgress.CurrentIncrementSize} damage");
                }
            }
            else
            {
                sb.AppendLine("\nNo weapon progress yet. Deal damage with swords, falx, or spears to start!");
            }

            if (currentCredits >= maxCredits)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait meleebase command.
        /// Sets the base damage needed for the first 1% increment.
        /// </summary>
        private TextCommandResult OnTraitMeleeBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base damage per increment must be at least 1");
                }

                BaseDamagePerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base damage per increment set to {BaseDamagePerIncrement}. New weapons will require this much damage for first 1%.");
            }
            else
            {
                return TextCommandResult.Success($"Current base damage per increment: {BaseDamagePerIncrement}\nIncrement step: +{MeleeIncrementStep} per credit");
            }
        }

        /// <summary>
        /// Handler for /trait meleeincrement command.
        /// Sets how much additional damage is required for each subsequent credit.
        /// </summary>
        private TextCommandResult OnTraitMeleeIncrementCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 0)
                {
                    return TextCommandResult.Error("Increment step cannot be negative");
                }

                MeleeIncrementStep = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Melee increment step set to +{MeleeIncrementStep} per credit.\nProgression: {BaseDamagePerIncrement}, {BaseDamagePerIncrement + MeleeIncrementStep}, {BaseDamagePerIncrement + MeleeIncrementStep * 2}...");
            }
            else
            {
                return TextCommandResult.Success($"Current melee increment step: +{MeleeIncrementStep} per credit\nProgression: {BaseDamagePerIncrement}, {BaseDamagePerIncrement + MeleeIncrementStep}, {BaseDamagePerIncrement + MeleeIncrementStep * 2}...");
            }
        }

        /// <summary>
        /// Handler for /trait meleelevel command.
        /// Gets or sets the player's melee credits (level) directly.
        /// Note: Setting resets all per-weapon progress since we're setting credits directly.
        /// </summary>
        private TextCommandResult OnTraitMeleeLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            int? newCredits = (int?)args[0];

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                int maxCredits = GetMaxMeleeCredits(player.Entity);
                var progress = MeleeProgress.GetOrAdd(player.PlayerUID, _ => new MeleeProgressData());
                int currentBonus = CalculateMeleeBonusPercent(progress.TotalCredits);
                return TextCommandResult.Success($"Current melee level: {progress.TotalCredits}/{maxCredits} (+{currentBonus}% melee damage)");
            }

            string toolName = (string)args[1];
            return SetMeleeLevelForPlayer(player, newCredits.Value, toolName);
        }

        /// <summary>
        /// Handler for /trait meleemax command.
        /// Gets or sets the maximum melee damage bonus percent.
        /// </summary>
        private TextCommandResult OnTraitMeleeMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max melee damage percent must be at least 1");
                }

                MaxMeleeDamagePercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
                    ApplyMeleeBonusStatic(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Max melee damage bonus set to +{MaxMeleeDamagePercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max melee damage bonus: +{MaxMeleeDamagePercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait ranged command.
        /// </summary>
        private TextCommandResult OnTraitRangedCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());

            int currentCredits = progress.TotalCredits;
            var (damageBonus, accuracyBonus, distanceBonus) = CalculateRangedBonusPercents(currentCredits, player.Entity as EntityPlayer);
            int maxCredits = GetMaxRangedCredits(player.Entity as EntityPlayer);

            var sb = new StringBuilder();
            sb.AppendLine($"Ranged progression: {currentCredits} credits / {maxCredits} max");
            sb.AppendLine($"Current bonuses: +{damageBonus}% damage, +{accuracyBonus}% accuracy, +{distanceBonus}% distance");

            if (progress.WeaponProgress.Count > 0)
            {
                sb.AppendLine("\nPer-weapon progress:");
                foreach (var kvp in progress.WeaponProgress.OrderBy(p => p.Value.CurrentIncrementSize))
                {
                    string weaponName = kvp.Key;
                    // Simplify the display name (remove "game:" prefix if present)
                    weaponName = weaponName.Replace("game:", "");

                    var weaponProgress = kvp.Value;
                    sb.AppendLine($"  {weaponName}: {weaponProgress.DamageInIncrement:F1}/{weaponProgress.CurrentIncrementSize} damage");
                }
            }
            else
            {
                sb.AppendLine("\nNo weapon progress yet. Deal ranged damage with bows or slings to start!");
            }

            if (currentCredits >= maxCredits)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait rangedbase command.
        /// Sets the base damage needed for the first 1% increment.
        /// </summary>
        private TextCommandResult OnTraitRangedBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base damage per increment must be at least 1");
                }

                BaseRangedDamagePerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base ranged damage per increment set to {BaseRangedDamagePerIncrement}. New weapons will require this much damage for first 1%.");
            }
            else
            {
                return TextCommandResult.Success($"Current base ranged damage per increment: {BaseRangedDamagePerIncrement}\nIncrement step: +{RangedIncrementStep} per credit");
            }
        }

        /// <summary>
        /// Handler for /trait rangedincrement command.
        /// Sets how much additional damage is required for each subsequent credit.
        /// </summary>
        private TextCommandResult OnTraitRangedIncrementCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 0)
                {
                    return TextCommandResult.Error("Increment step cannot be negative");
                }

                RangedIncrementStep = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Ranged increment step set to +{RangedIncrementStep} per credit.\nProgression: {BaseRangedDamagePerIncrement}, {BaseRangedDamagePerIncrement + RangedIncrementStep}, {BaseRangedDamagePerIncrement + RangedIncrementStep * 2}...");
            }
            else
            {
                return TextCommandResult.Success($"Current ranged increment step: +{RangedIncrementStep} per credit\nProgression: {BaseRangedDamagePerIncrement}, {BaseRangedDamagePerIncrement + RangedIncrementStep}, {BaseRangedDamagePerIncrement + RangedIncrementStep * 2}...");
            }
        }

        /// <summary>
        /// Handler for /trait rangedlevel command.
        /// Gets or sets the player's ranged credits (level) directly.
        /// Note: Setting resets all per-weapon progress since we're setting credits directly.
        /// </summary>
        private TextCommandResult OnTraitRangedLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            int? newCredits = (int?)args[0];

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                int maxCredits = GetMaxRangedCredits(player.Entity);
                var progress = RangedProgress.GetOrAdd(player.PlayerUID, _ => new RangedProgressData());
                var (damageBonus, accuracyBonus, distanceBonus) = CalculateRangedBonusPercents(progress.TotalCredits, player.Entity);
                return TextCommandResult.Success($"Current ranged level: {progress.TotalCredits}/{maxCredits} (+{damageBonus}% damage, +{accuracyBonus}% accuracy, +{distanceBonus}% distance)");
            }

            string toolName = (string)args[1];
            return SetRangedLevelForPlayer(player, newCredits.Value, toolName);
        }

        /// <summary>
        /// Handler for /trait rangedmax command.
        /// Gets or sets the maximum ranged damage bonus percent.
        /// </summary>
        private TextCommandResult OnTraitRangedMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max ranged damage percent must be at least 1");
                }

                MaxRangedDamagePercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
                    ApplyRangedBonusStatic(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Max ranged damage bonus set to +{MaxRangedDamagePercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max ranged damage bonus: +{MaxRangedDamagePercent}%\nMax accuracy: +{MaxRangedAccuracyPercent}%\nMax distance: +{MaxRangedDistancePercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait rangedmaxacc command.
        /// Gets or sets the maximum ranged accuracy bonus percent.
        /// </summary>
        private TextCommandResult OnTraitRangedMaxAccuracyCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max ranged accuracy percent must be at least 1");
                }

                MaxRangedAccuracyPercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
                    ApplyRangedBonusStatic(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Max ranged accuracy bonus set to +{MaxRangedAccuracyPercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max ranged accuracy bonus: +{MaxRangedAccuracyPercent}%\nMax damage: +{MaxRangedDamagePercent}%\nMax distance: +{MaxRangedDistancePercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait rangedmaxdist command.
        /// Gets or sets the maximum ranged distance bonus percent.
        /// </summary>
        private TextCommandResult OnTraitRangedMaxDistanceCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max ranged distance percent must be at least 1");
                }

                MaxRangedDistancePercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
                    ApplyRangedBonusStatic(player, progress.TotalCredits);
                }

                return TextCommandResult.Success($"Max ranged distance bonus set to +{MaxRangedDistancePercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max ranged distance bonus: +{MaxRangedDistancePercent}%\nMax damage: +{MaxRangedDamagePercent}%\nMax accuracy: +{MaxRangedAccuracyPercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait walking command.
        /// </summary>
        private TextCommandResult OnTraitWalkingCommand(TextCommandCallingArgs args)
        {
            return WalkingProgressData.HandleTraitCommand(args);
        }

        /// <summary>
        /// Handler for /trait walkingbase command.
        /// Sets the base blocks needed for the first 1% increment.
        /// </summary>
        private TextCommandResult OnTraitWalkingBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base blocks per increment must be at least 1");
                }

                BaseBlocksWalkedPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base blocks per increment set to {BaseBlocksWalkedPerIncrement}. New progress will require this many blocks for first 1%.");
            }
            else
            {
                return TextCommandResult.Success($"Current base blocks per increment: {BaseBlocksWalkedPerIncrement}\nIncrement step: +{WalkingIncrementStep} per credit");
            }
        }

        /// <summary>
        /// Handler for /trait walkingincrement command.
        /// Sets how many additional blocks are required for each subsequent credit.
        /// </summary>
        private TextCommandResult OnTraitWalkingIncrementCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 0)
                {
                    return TextCommandResult.Error("Increment step cannot be negative");
                }

                WalkingIncrementStep = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Walking increment step set to +{WalkingIncrementStep} per credit.\nProgression: {BaseBlocksWalkedPerIncrement}, {BaseBlocksWalkedPerIncrement + WalkingIncrementStep}, {BaseBlocksWalkedPerIncrement + WalkingIncrementStep * 2}...");
            }
            else
            {
                return TextCommandResult.Success($"Current walking increment step: +{WalkingIncrementStep} per credit\nProgression: {BaseBlocksWalkedPerIncrement}, {BaseBlocksWalkedPerIncrement + WalkingIncrementStep}, {BaseBlocksWalkedPerIncrement + WalkingIncrementStep * 2}...");
            }
        }

        /// <summary>
        /// Handler for /trait hunger command.
        /// </summary>
        private TextCommandResult OnTraitHungerCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
            {
                CurrentIncrementSize = BaseSecondsPerIncrement
            });

            int currentCredits = progress.TotalCredits;
            int playerMaxCredits = CalculateMaxHungerCredits(player.Entity as EntityPlayer);
            int bonusPercent = CalculateHungerBonusPercent(currentCredits, player.Entity as EntityPlayer);
            bool hasRavenous = PlayerHasVanillaRavenousStatic(player.Entity as EntityPlayer);

            // Calculate target hunger rate (same for all classes)
            int targetHungerRate = 100 - MaxHungerReductionPercent;

            var sb = new StringBuilder();
            sb.AppendLine($"Hunger progression: {currentCredits} / {playerMaxCredits} credits");
            sb.AppendLine($"Current bonus: -{bonusPercent}% hunger rate");
            if (hasRavenous)
            {
                int currentRate = 130 - bonusPercent;
                sb.AppendLine($"Effective hunger rate: {currentRate}% (Ravenous: 130% base)");
            }
            else
            {
                int currentRate = 100 - bonusPercent;
                sb.AppendLine($"Effective hunger rate: {currentRate}%");
            }
            sb.AppendLine($"Target hunger rate: {targetHungerRate}%");
            sb.AppendLine($"\nProgress toward next credit:");
            sb.AppendLine($"  {progress.SecondsInIncrement:F0}/{progress.CurrentIncrementSize} seconds at full saturation");

            if (currentCredits >= playerMaxCredits)
            {
                sb.Insert(0, "=== MAXED OUT ===\n");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait hungerbase command.
        /// Sets the base seconds needed for the first 1% increment.
        /// </summary>
        private TextCommandResult OnTraitHungerBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base seconds per increment must be at least 1");
                }

                BaseSecondsPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base seconds per increment set to {BaseSecondsPerIncrement}. First 1% requires {BaseSecondsPerIncrement} seconds at full saturation.");
            }
            else
            {
                return TextCommandResult.Success($"Current base seconds per increment: {BaseSecondsPerIncrement}\nIncrement step: +{HungerIncrementStep} per credit");
            }
        }

        /// <summary>
        /// Handler for /trait hungerincrement command.
        /// Sets how many additional seconds are required for each subsequent credit.
        /// </summary>
        private TextCommandResult OnTraitHungerIncrementCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 0)
                {
                    return TextCommandResult.Error("Increment step cannot be negative");
                }

                HungerIncrementStep = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Hunger increment step set to +{HungerIncrementStep} per credit.\nProgression: {BaseSecondsPerIncrement}, {BaseSecondsPerIncrement + HungerIncrementStep}, {BaseSecondsPerIncrement + HungerIncrementStep * 2}...");
            }
            else
            {
                return TextCommandResult.Success($"Current hunger increment step: +{HungerIncrementStep} per credit\nProgression: {BaseSecondsPerIncrement}, {BaseSecondsPerIncrement + HungerIncrementStep}, {BaseSecondsPerIncrement + HungerIncrementStep * 2}...");
            }
        }

        /// <summary>
        /// Handler for /trait hungerlevel command.
        /// Gets or sets the player's hunger credits (level) directly.
        /// </summary>
        private TextCommandResult OnTraitHungerLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            // Calculate player-specific max credits
            int playerMaxCredits = CalculateMaxHungerCredits(player.Entity);
            string playerUid = player.PlayerUID;
            var progress = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
            {
                CurrentIncrementSize = BaseSecondsPerIncrement
            });

            int? newCredits = (int?)args[0];

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                bool hasRavenousCurrent = PlayerHasVanillaRavenousStatic(player.Entity);
                int currentEffectiveRate = hasRavenousCurrent ? (130 - progress.TotalCredits) : (100 - progress.TotalCredits);
                return TextCommandResult.Success($"Current hunger level: {progress.TotalCredits}/{playerMaxCredits} (-{progress.TotalCredits}% hunger rate, effective rate: {currentEffectiveRate}%)");
            }

            if (newCredits.Value < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits.Value > playerMaxCredits)
            {
                return TextCommandResult.Error($"Credits cannot exceed max for this player ({playerMaxCredits})");
            }

            // Set the player's progress
            progress.TotalCredits = newCredits.Value;
            progress.SecondsInIncrement = 0;
            // Calculate what the increment size should be at this level
            progress.CurrentIncrementSize = BaseSecondsPerIncrement + (newCredits.Value * HungerIncrementStep);

            pendingHungerProgressSave = true;

            // Apply the bonus
            int bonusPercent = ApplyHungerBonusStatic(player, newCredits.Value);

            bool hasRavenous = PlayerHasVanillaRavenousStatic(player.Entity);
            int effectiveRate = hasRavenous ? (130 - bonusPercent) : (100 - bonusPercent);

            UpdateSkillActivityDay(playerUid, "hunger");

            return TextCommandResult.Success($"Hunger credits set to {newCredits.Value}/{playerMaxCredits} (-{bonusPercent}% hunger rate, effective rate: {effectiveRate}%).");
        }

        /// <summary>
        /// Handler for /trait hungermax command.
        /// Gets or sets the maximum hunger rate reduction percent (for non-Ravenous players).
        /// This determines the target hunger rate for all classes.
        /// </summary>
        private TextCommandResult OnTraitHungerMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max hunger rate reduction percent must be at least 1");
                }

                MaxHungerReductionPercent = newValue.Value;
                pendingConfigSave = true;

                // Recalculate and reapply bonuses for all online players
                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
                    {
                        CurrentIncrementSize = BaseSecondsPerIncrement
                    });
                    ApplyHungerBonusStatic(player, progress.TotalCredits);
                }

                int targetRate = 100 - MaxHungerReductionPercent;
                return TextCommandResult.Success($"Target hunger rate set to {targetRate}% (non-Ravenous: {MaxHungerReductionPercent} credits, Ravenous: {MaxHungerReductionPercent + VANILLA_RAVENOUS_HUNGER_PENALTY} credits). All player bonuses recalculated.");
            }
            else
            {
                int targetRate = 100 - MaxHungerReductionPercent;
                return TextCommandResult.Success($"Target hunger rate: {targetRate}%\nNon-Ravenous players need {MaxHungerReductionPercent} credits\nRavenous players need {MaxHungerReductionPercent + VANILLA_RAVENOUS_HUNGER_PENALTY} credits");
            }
        }

        /// <summary>
        /// Handler for /trait armor command.
        /// </summary>
        private TextCommandResult OnTraitArmorCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            int durabilityBonus = CalculateArmorDurabilityBonusPercent(progress.TotalDurabilityCredits, player.Entity as EntityPlayer);
            int walkSpeedBonus = CalculateArmorWalkSpeedBonusPercent(progress.TotalWalkSpeedCredits, player.Entity as EntityPlayer);

            var sb = new StringBuilder();
            sb.AppendLine($"Armor progression:");
            sb.AppendLine($"  Durability: {progress.TotalDurabilityCredits} credits, +{durabilityBonus}% bonus (max {MaxArmorDurabilityPercent}%)");
            sb.AppendLine($"  Walk Speed Penalty Reduction: {progress.TotalWalkSpeedCredits} credits, -{walkSpeedBonus}% (max {MaxArmorWalkSpeedPercent}%)");

            if (progress.ArmorProgress.Count > 0)
            {
                sb.AppendLine("\nPer-armor progress:");
                foreach (var kvp in progress.ArmorProgress.OrderByDescending(p => p.Value.TimeCredits + p.Value.DamageCredits + p.Value.RepairCredits))
                {
                    string armorName = kvp.Key;
                    if (armorName.StartsWith("game:"))
                        armorName = armorName.Substring(5);

                    var armorProg = kvp.Value;
                    sb.AppendLine($"  {armorName}:");
                    sb.AppendLine($"    Time: {armorProg.TimeCredits} credits ({armorProg.SecondsWornInIncrement:F0}/{armorProg.CurrentTimeIncrementSize}s)");
                    sb.AppendLine($"    Damage: {armorProg.DamageCredits} credits ({armorProg.DamageBlockedInIncrement:F1}/{armorProg.CurrentDamageIncrementSize})");
                    sb.AppendLine($"    Repairs: {armorProg.RepairCredits} credits ({armorProg.RepairsInIncrement}/{armorProg.CurrentRepairIncrementSize})");
                }
            }
            else
            {
                sb.AppendLine("\nNo armor progress yet. Wear armor to start!");
            }

            return TextCommandResult.Success(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Handler for /trait armorlevel command.
        /// Gets or sets the player's armor durability credits (level) directly.
        /// Optionally specify an armor piece to set credits on that specific piece.
        /// </summary>
        private TextCommandResult OnTraitArmorLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            int? newCredits = (int?)args[0];

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                var progress = ArmorProgress.GetOrAdd(player.PlayerUID, _ => new ArmorProgressData());
                int currentBonus = CalculateArmorDurabilityBonusPercent(progress.TotalDurabilityCredits, player.Entity);
                return TextCommandResult.Success($"Current armor durability level: {progress.TotalDurabilityCredits}/{MaxArmorDurabilityPercent} (+{currentBonus}% durability)");
            }

            string toolName = (string)args[1];
            return SetArmorLevelForPlayer(player, newCredits.Value, toolName);
        }

        /// <summary>
        /// Handler for /trait armorwalkspeedlevel command.
        /// Gets or sets the player's armor walk speed penalty reduction credits (level) directly.
        /// </summary>
        private TextCommandResult OnTraitArmorWalkSpeedLevelCommand(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            string playerUid = player.PlayerUID;
            var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            int? newCredits = (int?)args[0];

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                int currentBonus = CalculateArmorWalkSpeedBonusPercent(progress.TotalWalkSpeedCredits, player.Entity);
                return TextCommandResult.Success($"Current armor walk speed penalty reduction level: {progress.TotalWalkSpeedCredits}/{MaxArmorWalkSpeedPercent} (-{currentBonus}% penalty)");
            }

            if (newCredits.Value < 0)
            {
                return TextCommandResult.Error("Credits cannot be negative");
            }

            if (newCredits.Value > MaxArmorWalkSpeedPercent)
            {
                return TextCommandResult.Error($"Credits cannot exceed max ({MaxArmorWalkSpeedPercent})");
            }

            progress.TotalWalkSpeedCredits = newCredits.Value;
            pendingArmorProgressSave = true;

            ApplyArmorBonusesStatic(player, progress.TotalDurabilityCredits, progress.TotalWalkSpeedCredits);

            int bonusPercent = CalculateArmorWalkSpeedBonusPercent(newCredits.Value, player.Entity);

            UpdateSkillActivityDay(playerUid, "armor");

            return TextCommandResult.Success($"Armor walk speed penalty reduction credits set to {newCredits.Value} (-{bonusPercent}% penalty).");
        }

        /// <summary>
        /// Handler for /trait armordurabilitymax command.
        /// </summary>
        private TextCommandResult OnTraitArmorDurabilityMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max armor durability percent must be at least 1");
                }

                MaxArmorDurabilityPercent = newValue.Value;
                pendingConfigSave = true;

                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
                    ApplyArmorBonusesStatic(player, progress.TotalDurabilityCredits, progress.TotalWalkSpeedCredits);
                }

                return TextCommandResult.Success($"Max armor durability bonus set to +{MaxArmorDurabilityPercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max armor durability bonus: +{MaxArmorDurabilityPercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait armorwalkspeedmax command.
        /// </summary>
        private TextCommandResult OnTraitArmorWalkSpeedMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Max armor walk speed penalty reduction percent must be at least 1");
                }

                MaxArmorWalkSpeedPercent = newValue.Value;
                pendingConfigSave = true;

                foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
                {
                    if (player?.Entity == null) continue;
                    string playerUid = player.PlayerUID;
                    var progress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
                    ApplyArmorBonusesStatic(player, progress.TotalDurabilityCredits, progress.TotalWalkSpeedCredits);
                }

                return TextCommandResult.Success($"Max armor walk speed penalty reduction set to -{MaxArmorWalkSpeedPercent}%. All player bonuses recalculated.");
            }
            else
            {
                return TextCommandResult.Success($"Current max armor walk speed penalty reduction: -{MaxArmorWalkSpeedPercent}%");
            }
        }

        /// <summary>
        /// Handler for /trait armortimebase command.
        /// </summary>
        private TextCommandResult OnTraitArmorTimeBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base seconds must be at least 1");
                }

                BaseSecondsInArmorPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base seconds in armor per increment set to {BaseSecondsInArmorPerIncrement} ({BaseSecondsInArmorPerIncrement / 3600f:F1} hours).");
            }
            else
            {
                return TextCommandResult.Success($"Current base seconds in armor: {BaseSecondsInArmorPerIncrement} ({BaseSecondsInArmorPerIncrement / 3600f:F1} hours)\nIncrement step: +{ArmorTimeIncrementStep} ({ArmorTimeIncrementStep / 3600f:F1} hours)");
            }
        }

        /// <summary>
        /// Handler for /trait armordamagebase command.
        /// </summary>
        private TextCommandResult OnTraitArmorDamageBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base damage must be at least 1");
                }

                BaseDamageBlockedPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base damage blocked per increment set to {BaseDamageBlockedPerIncrement}.");
            }
            else
            {
                return TextCommandResult.Success($"Current base damage blocked: {BaseDamageBlockedPerIncrement}\nIncrement step: +{ArmorDamageIncrementStep}");
            }
        }

        /// <summary>
        /// Handler for /trait armorrepairbase command.
        /// </summary>
        private TextCommandResult OnTraitArmorRepairBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error("Base repairs must be at least 1");
                }

                BaseRepairsPerIncrement = newValue.Value;
                pendingConfigSave = true;

                return TextCommandResult.Success($"Base repairs per increment set to {BaseRepairsPerIncrement}.");
            }
            else
            {
                return TextCommandResult.Success($"Current base repairs: {BaseRepairsPerIncrement}\nIncrement step: +{ArmorRepairIncrementStep}");
            }
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
        /// Calculate the maximum hunger credits a player can earn.
        /// Ravenous players need more credits to reach the same target hunger rate.
        /// Target is (100 - MaxHungerReductionPercent)% = 75% by default.
        /// Non-Ravenous: 100% - 75% = 25 credits needed
        /// Ravenous: 130% - 75% = 55 credits needed
        /// </summary>
        public static int CalculateMaxHungerCredits(EntityPlayer entity)
        {
            bool hasRavenous = entity != null && PlayerHasVanillaRavenousStatic(entity);
            int ravenousPenalty = hasRavenous ? VANILLA_RAVENOUS_HUNGER_PENALTY : 0;
            // MaxHungerReductionPercent represents how much a normal player needs to reduce
            // Ravenous players need that PLUS their penalty to reach the same target
            return MaxHungerReductionPercent + ravenousPenalty;
        }

        /// <summary>
        /// Calculate the hunger rate reduction bonus as an integer percentage.
        /// This is the actual reduction applied (1% per credit, up to player's max).
        /// </summary>
        public static int CalculateHungerBonusPercent(int credits, EntityPlayer entity)
        {
            int maxCredits = CalculateMaxHungerCredits(entity);
            return Math.Min(credits, maxCredits);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Ravenous trait.
        /// </summary>
        private static bool PlayerHasVanillaRavenousStatic(EntityPlayer entity)
        {
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);

            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("ravenous", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // Fallback: check known classes that have Ravenous (Blackguard)
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("blackguard", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Apply hunger rate reduction to a player based on their level.
        /// Returns the actual applied bonus percentage.
        /// All classes can reach the same target hunger rate (75% by default).
        /// Ravenous players start at 130% and need 55 credits to reach 75%.
        /// Non-Ravenous players start at 100% and need 25 credits to reach 75%.
        /// </summary>
        public static int ApplyHungerBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaRavenous = cache?.HasRavenous ?? PlayerHasVanillaRavenousStatic(player.Entity);

            // Calculate max credits this player can earn
            int maxCredits = CalculateMaxHungerCredits(player.Entity);

            // Calculate bonus from level (1% per level, capped at player's max)
            int cappedLevel = Math.Min(level, maxCredits);
            float bonus = cappedLevel * 0.01f;
            int bonusPercent = (int)(bonus * 100);

            // Calculate remaining Ravenous penalty (0 when fully cancelled at level 30)
            int ravenousRemaining = hasVanillaRavenous ? CalculateRemainingPenalty(VANILLA_RAVENOUS_HUNGER_PENALTY, level) : 0;

            // Always apply stats (they're not persistent)
            // Set the hunger rate stat - this value is ADDED to the base (1.0)
            // We want to REDUCE hunger rate, so we use a negative value
            player.Entity.Stats.Set("hungerrate", HUNGER_STAT_CODE, -bonus, false);

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_HUNGER_LEVEL, -1);
            int oldBonus = watchedAttrs.GetInt(WATCHED_HUNGER_BONUS, -1);

            bool valuesChanged = (oldLevel != level) || (oldBonus != bonusPercent);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_HUNGER_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_HUNGER_BONUS, bonusPercent);
                watchedAttrs.SetBool("sitHasVanillaRavenous", hasVanillaRavenous);
                watchedAttrs.SetInt("sitMaxHungerCredits", maxCredits);
                watchedAttrs.SetInt(WATCHED_RAVENOUS_REMAINING, ravenousRemaining);

                // Add our trait to extraTraits (hunger mastery is unique, doesn't replace a vanilla trait)
                UpdateExtraTraitStatic(player.Entity, HUNGER_TRAIT_CODE, level > 0);

                // Only call MarkPathDirty once (batched update)
                watchedAttrs.MarkPathDirty(WATCHED_HUNGER_LEVEL);
            }

            return bonusPercent;
        }

        /// <summary>
        /// Calculate the walking speed bonus as an integer percentage.
        /// Accounts for vanilla Fleetfooted trait (+10% walk speed).
        /// </summary>
        public static int CalculateWalkingBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasFleetfooted = entity != null && PlayerHasVanillaFleetfootedStatic(entity);
            int vanillaBonus = hasFleetfooted ? VANILLA_FLEETFOOTED_WALK_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxWalkingSpeedPercent - vanillaBonus);
            return Math.Min(credits, earnableBonus);
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
        /// Calculate ranged bonuses as percentages, accounting for vanilla Focused trait.
        /// Returns (damageBonus, accuracyBonus, distanceBonus) as integers.
        /// </summary>
        public static (int damage, int accuracy, int distance) CalculateRangedBonusPercents(int credits, EntityPlayer entity)
        {
            bool hasFocused = entity != null && PlayerHasVanillaFocusedStatic(entity);
            int vanillaDamage = hasFocused ? VANILLA_FOCUSED_DAMAGE_BONUS : 0;
            int vanillaAccuracy = hasFocused ? VANILLA_FOCUSED_ACCURACY_BONUS : 0;
            int vanillaDistance = hasFocused ? VANILLA_FOCUSED_DISTANCE_BONUS : 0;

            // Each stat is capped individually
            int earnableDamage = Math.Max(0, MaxRangedDamagePercent - vanillaDamage);
            int earnableAccuracy = Math.Max(0, MaxRangedAccuracyPercent - vanillaAccuracy);
            int earnableDistance = Math.Max(0, MaxRangedDistancePercent - vanillaDistance);

            int damageBonus = Math.Min(credits, earnableDamage);
            int accuracyBonus = Math.Min(credits, earnableAccuracy);
            int distanceBonus = Math.Min(credits, earnableDistance);

            return (damageBonus, accuracyBonus, distanceBonus);
        }

        /// <summary>
        /// Get the maximum ranged credits a player can earn based on their traits.
        /// Players with Nearsighted or Frail traits can earn extra credits
        /// to compensate for the penalty before gaining positive bonuses.
        /// </summary>
        public static int GetMaxRangedCredits(EntityPlayer entity)
        {
            if (entity == null) return MaxRangedDamagePercent;

            bool hasNearsighted = PlayerHasVanillaNearsighted(entity);
            bool hasFrail = PlayerHasVanillaFrail(entity);

            // Use the larger penalty to determine max credits
            int extraCredits = 0;

            // Nearsighted penalty is 15% ranged damage, need 15 extra levels to cancel it
            if (hasNearsighted)
            {
                extraCredits = Math.Max(extraCredits, VANILLA_NEARSIGHTED_RANGED_PENALTY);
            }

            // Frail penalty is 25% ranged distance, need 25 extra levels to cancel it
            if (hasFrail)
            {
                extraCredits = Math.Max(extraCredits, VANILLA_FRAIL_DISTANCE_PENALTY);
            }

            return MaxRangedDamagePercent + extraCredits;
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Focused trait.
        /// </summary>
        private static bool PlayerHasVanillaFocusedStatic(EntityPlayer entity)
        {
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);

            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("focused", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // Fallback: check known classes that have Focused (Hunter)
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("hunter", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Fleetfooted trait.
        /// </summary>
        public static bool PlayerHasVanillaFleetfootedStatic(EntityPlayer entity)
        {
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);

            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("fleetfooted", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // Fallback: check known classes that have Fleetfooted (Hunter, Clockmaker)
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("hunter", StringComparison.OrdinalIgnoreCase) ||
                   characterClass.Equals("clockmaker", StringComparison.OrdinalIgnoreCase);
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
        /// Checks if the player's class has the vanilla Nearsighted trait (Blackguard).
        /// </summary>
        public static bool PlayerHasVanillaNearsighted(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("nearsighted", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("blackguard", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Frail trait (Malefactor, Clockmaker).
        /// </summary>
        public static bool PlayerHasVanillaFrail(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("frail", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("malefactor", StringComparison.OrdinalIgnoreCase) ||
                   characterClass.Equals("clockmaker", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Civil trait (Tailor).
        /// </summary>
        public static bool PlayerHasVanillaCivil(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("civil", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("tailor", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Weak trait (Tailor).
        /// </summary>
        public static bool PlayerHasVanillaWeak(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("weak", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("tailor", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Kind trait (Tailor).
        /// </summary>
        public static bool PlayerHasVanillaKind(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("kind", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("tailor", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Heavyhanded trait (Blackguard).
        /// </summary>
        public static bool PlayerHasVanillaHeavyhanded(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("heavyhanded", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("blackguard", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Claustrophobic trait (Hunter).
        /// </summary>
        public static bool PlayerHasVanillaClaustrophobic(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("claustrophobic", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("hunter", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the player has the SacredLib HeavyFooted trait
        /// </summary>
        public static bool PlayerHasSLHeavyFooted(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("heavyfooted", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
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
        /// Apply armor bonuses to a player.
        /// Stats are always applied (they're not persistent). WatchedAttributes only sync when values change.
        /// </summary>
        public static void ApplyArmorBonusesStatic(IServerPlayer player, int durabilityCredits, int walkSpeedCredits)
        {
            if (player?.Entity == null) return;

            // Get the full armor progress data for optional features
            string playerUid = player.PlayerUID;
            ArmorProgress.TryGetValue(playerUid, out var armorProgressData);

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(playerUid);
            bool hasVanillaSoldier = cache?.HasSoldier ?? PlayerHasVanillaSoldierForArmor(player.Entity);

            // Calculate durability bonus (reduces armor damage taken)
            int durabilityBonus = CalculateArmorDurabilityBonusPercent(durabilityCredits, player.Entity);
            // Calculate walk speed penalty reduction
            int walkSpeedBonus = CalculateArmorWalkSpeedBonusPercent(walkSpeedCredits, player.Entity);

            // Always apply stats (they're not persistent and need to be set on every join)
            // armorDurabilityLoss blends as WeightedSum over a base of 1, so this is
            // a delta and not a multiplier: vanilla Soldier stores -0.15 and vanilla
            // Mender -0.25. A negative value means less durability lost.
            float durabilityReduction = -(durabilityBonus * 0.01f);
            player.Entity.Stats.Set("armorDurabilityLoss", ARMOR_DURABILITY_STAT_CODE, durabilityReduction, false);

            // Reduce armor walk speed penalty using armorWalkSpeedAffectedness
            // Negative values reduce the penalty (e.g., -0.25 = 25% less armor penalty)
            // Base value is 1.0, so setting -0.5 gives 1.0 + (-0.5) = 0.5 (50% of penalty applied)
            float armorWalkSpeedReduction = -(walkSpeedBonus * 0.01f);
            player.Entity.Stats["armorWalkSpeedAffectedness"].Set(ARMOR_WALKSPEED_STAT_CODE, armorWalkSpeedReduction);

            // Apply optional armor features if enabled
            if (EnableArmorHungerReduction && armorProgressData != null)
            {
                int hungerReductionCredits = armorProgressData.TotalHungerReductionCredits;
                int hungerReductionBonus = Math.Min(hungerReductionCredits, MaxArmorHungerReductionPercent);
                // hungerrate is a multiplier: lower = slower hunger drain
                float hungerMultiplier = -(hungerReductionBonus * 0.01f);
                player.Entity.Stats.Set("hungerrate", "sitArmorHunger", hungerMultiplier, false);
                player.Entity.WatchedAttributes.SetInt(WATCHED_ARMOR_HUNGER_REDUCTION, hungerReductionBonus);
            }

            if (EnableArmorHealingBonus && armorProgressData != null)
            {
                int healingCredits = armorProgressData.TotalHealingCredits;
                int healingBonus = Math.Min(healingCredits, MaxArmorHealingPercent);
                // The stat EntityPlayer registers is "healingeffectivness", missing an
                // e. Poultices, healing items and the character sheet all read that
                // spelling, so anything written to "healingeffectivenesstypical" is
                // simply never looked at.
                float healingModifier = healingBonus * 0.01f;
                player.Entity.Stats.Set("healingeffectivness", "sitArmorHealing", healingModifier, false);
                player.Entity.WatchedAttributes.SetInt(WATCHED_ARMOR_HEALING_BONUS, healingBonus);
            }

            // Debug: Log the stat values
            float blendedValue = player.Entity.Stats.GetBlended("armorWalkSpeedAffectedness");
            ServerApi.Logger.Debug($"[SeraphLeveling] armorWalkSpeedAffectedness: set modifier {armorWalkSpeedReduction:F2}, blended value {blendedValue:F2}");

            // Force WearableStats to recalculate by triggering slot modified
            var charInv = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (charInv != null)
            {
                foreach (var slot in charInv)
                {
                    if (slot?.Itemstack != null)
                    {
                        slot.MarkDirty();
                        break;
                    }
                }
            }

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldDurabilityLevel = watchedAttrs.GetInt(WATCHED_ARMOR_DURABILITY_LEVEL, -1);
            int oldWalkSpeedLevel = watchedAttrs.GetInt(WATCHED_ARMOR_WALKSPEED_LEVEL, -1);

            bool valuesChanged = (oldDurabilityLevel != durabilityCredits) || (oldWalkSpeedLevel != walkSpeedCredits);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_ARMOR_DURABILITY_LEVEL, durabilityCredits);
                watchedAttrs.SetInt(WATCHED_ARMOR_DURABILITY_BONUS, durabilityBonus);
                watchedAttrs.SetInt(WATCHED_ARMOR_WALKSPEED_LEVEL, walkSpeedCredits);
                watchedAttrs.SetInt(WATCHED_ARMOR_WALKSPEED_BONUS, walkSpeedBonus);
                watchedAttrs.SetBool("sitHasVanillaSoldierArmor", hasVanillaSoldier);

                // Add our trait to extraTraits only if player doesn't already have Soldier
                UpdateExtraTraitStatic(player.Entity, ARMOR_TRAIT_CODE, (durabilityCredits > 0 || walkSpeedCredits > 0) && !hasVanillaSoldier);

                watchedAttrs.MarkPathDirty(WATCHED_ARMOR_DURABILITY_LEVEL);
            }

            // Apply CO-specific bonuses if enabled (Big Head/Thick Skull, Leg Day)
            if (IsCOCompatEnabled)
            {
                ApplyCOBigHeadThickSkull(player, durabilityCredits);
                ApplyCOLegDay(player, durabilityCredits);
            }
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
                            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
                            var pieceProgress = armorProgress.GetArmorProgress(itemCode);

                            if (!pieceProgress.HasBeenEquipped)
                            {
                                pieceProgress.HasBeenEquipped = true;
                                int firstEquipBonus = GetFirstEquipBonus(armorType);
                                armorProgress.TotalDurabilityCredits += firstEquipBonus;
                                pendingArmorProgressSave = true;

                                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} first-time equipped {itemCode}, +{firstEquipBonus}% durability bonus");

                                ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                                // Check for trait unlocks that depend on armor durability
                                CheckHardyHealthUnlock(player);
                                CheckMercilessUnlock(player);
                            }
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

            // Skip armor progression if disabled
            if (IsSkillDisabled("armor")) return;

            foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;
                if (!player.Entity.Alive) continue;

                string playerUid = player.PlayerUID;
                var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
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
                    var pieceProgress = armorProgress.GetArmorProgress(itemCode);

                    // Check if this is new armor in this slot
                    if (!previousArmor.TryGetValue(slotId, out string prevArmor) || prevArmor != itemCode)
                    {
                        // New armor equipped - check for first-time bonus
                        if (!pieceProgress.HasBeenEquipped)
                        {
                            pieceProgress.HasBeenEquipped = true;
                            string armorType = GetArmorType(itemCode);

                            // Grant durability bonus
                            int firstEquipBonus = GetFirstEquipBonus(armorType);
                            int oldDurability = armorProgress.TotalDurabilityCredits;
                            armorProgress.TotalDurabilityCredits = Math.Min(armorProgress.TotalDurabilityCredits + firstEquipBonus, MaxArmorDurabilityPercent);
                            int actualDurabilityBonus = armorProgress.TotalDurabilityCredits - oldDurability;

                            // Grant walk speed penalty reduction bonus (same values as durability)
                            int walkSpeedEquipBonus = GetFirstEquipWalkSpeedBonus(armorType);
                            int oldWalkSpeed = armorProgress.TotalWalkSpeedCredits;
                            armorProgress.TotalWalkSpeedCredits = Math.Min(armorProgress.TotalWalkSpeedCredits + walkSpeedEquipBonus, MaxArmorWalkSpeedPercent);
                            int actualWalkSpeedBonus = armorProgress.TotalWalkSpeedCredits - oldWalkSpeed;

                            pendingArmorProgressSave = true;
                            UpdateSkillActivityDay(playerUid, "armor");

                            ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                            // Send message with both bonuses
                            if (actualDurabilityBonus > 0 || actualWalkSpeedBonus > 0)
                            {
                                NotifyLevelUp(player,
                                    Lang.Get("seraphleveling:message-armor-first-equip-both", actualDurabilityBonus, actualWalkSpeedBonus));
                            }

                            // Check for trait unlocks that depend on armor durability
                            CheckHardyHealthUnlock(player);
                            CheckMercilessUnlock(player);
                        }
                    }

                    // Track time worn for walk speed credits (only if not at max for any time-based stat)
                    bool hasRoomForWalkSpeed = armorProgress.TotalWalkSpeedCredits < MaxArmorWalkSpeedPercent;
                    bool hasRoomForHunger = EnableArmorHungerReduction && armorProgress.TotalHungerReductionCredits < MaxArmorHungerReductionPercent;
                    bool hasRoomForHealing = EnableArmorHealingBonus && armorProgress.TotalHealingCredits < MaxArmorHealingPercent;

                    if (hasRoomForWalkSpeed || hasRoomForHunger || hasRoomForHealing)
                    {
                        int oldWalkSpeedCredits = armorProgress.TotalWalkSpeedCredits;
                        int oldHungerCredits = armorProgress.TotalHungerReductionCredits;
                        int oldHealingCredits = armorProgress.TotalHealingCredits;

                        // Add 1 second (tick interval) to this armor piece's time
                        // Apply sleep buff multiplier if active
                        float modifiedTime = ApplyXPMultiplier(playerUid, 1f);
                        pieceProgress.SecondsWornInIncrement += modifiedTime;

                        // Check if we've earned any new time credits
                        while (pieceProgress.SecondsWornInIncrement >= pieceProgress.CurrentTimeIncrementSize)
                        {
                            bool anyEarned = false;

                            // Award walk speed credit if not at max
                            if (armorProgress.TotalWalkSpeedCredits < MaxArmorWalkSpeedPercent)
                            {
                                pieceProgress.TimeCredits++;
                                armorProgress.TotalWalkSpeedCredits++;
                                anyEarned = true;
                            }

                            // Award hunger reduction credit if feature enabled and not at max
                            if (EnableArmorHungerReduction && armorProgress.TotalHungerReductionCredits < MaxArmorHungerReductionPercent)
                            {
                                armorProgress.TotalHungerReductionCredits++;
                                anyEarned = true;
                            }

                            // Award healing credit if feature enabled and not at max
                            if (EnableArmorHealingBonus && armorProgress.TotalHealingCredits < MaxArmorHealingPercent)
                            {
                                armorProgress.TotalHealingCredits++;
                                anyEarned = true;
                            }

                            if (!anyEarned) break; // All time-based stats are at max

                            pieceProgress.SecondsWornInIncrement -= pieceProgress.CurrentTimeIncrementSize;
                            pieceProgress.CurrentTimeIncrementSize += ArmorTimeIncrementStep;

                            ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned time credit {pieceProgress.TimeCredits} with {itemCode}");
                        }

                        bool creditsChanged = (armorProgress.TotalWalkSpeedCredits > oldWalkSpeedCredits) ||
                                               (armorProgress.TotalHungerReductionCredits > oldHungerCredits) ||
                                               (armorProgress.TotalHealingCredits > oldHealingCredits);

                        if (creditsChanged)
                        {
                            pendingArmorProgressSave = true;
                            UpdateSkillActivityDay(playerUid, "armor");
                            ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                            // Notify player of level up (only for walk speed, as it's the primary stat)
                            if (armorProgress.TotalWalkSpeedCredits > oldWalkSpeedCredits)
                            {
                                NotifyLevelUp(player,
                                    Lang.Get("seraphleveling:message-armor-time-level-up", armorProgress.TotalWalkSpeedCredits));
                            }
                        }
                    }
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
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            // Skip if already at max durability
            if (armorProgress.TotalDurabilityCredits >= MaxArmorDurabilityPercent) return;

            var pieceProgress = armorProgress.GetArmorProgress(armorCode);
            int oldDurabilityCredits = armorProgress.TotalDurabilityCredits;

            // Apply sleep buff multiplier if active
            float modifiedDamage = ApplyXPMultiplier(playerUid, damageBlocked);
            pieceProgress.DamageBlockedInIncrement += modifiedDamage;

            // Check if we've earned any new damage credits
            while (pieceProgress.DamageBlockedInIncrement >= pieceProgress.CurrentDamageIncrementSize &&
                   armorProgress.TotalDurabilityCredits < MaxArmorDurabilityPercent)
            {
                pieceProgress.DamageCredits++;
                armorProgress.TotalDurabilityCredits++;
                pieceProgress.DamageBlockedInIncrement -= pieceProgress.CurrentDamageIncrementSize;
                pieceProgress.CurrentDamageIncrementSize += ArmorDamageIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned damage credit {pieceProgress.DamageCredits} with {armorCode}");
            }

            pendingArmorProgressSave = true;

            if (armorProgress.TotalDurabilityCredits > oldDurabilityCredits)
            {
                UpdateSkillActivityDay(playerUid, "armor");
                ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                // Notify player of level up with raw improvement (shows progress even when capped)
                NotifyLevelUp(player,
                    Lang.Get("seraphleveling:message-armor-damage-level-up", armorProgress.TotalDurabilityCredits, armorProgress.TotalDurabilityCredits));

                // Check for trait unlocks that depend on armor durability
                CheckHardyHealthUnlock(player);
                CheckMercilessUnlock(player);
            }
        }

        /// <summary>
        /// Process armor repair. Called from Harmony patch when armor is repaired.
        /// </summary>
        public static void ProcessArmorRepair(IServerPlayer player, string armorCode)
        {
            if (player?.Entity == null || string.IsNullOrEmpty(armorCode)) return;

            string playerUid = player.PlayerUID;
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            // Skip if already at max durability
            if (armorProgress.TotalDurabilityCredits >= MaxArmorDurabilityPercent) return;

            var pieceProgress = armorProgress.GetArmorProgress(armorCode);
            int oldDurabilityCredits = armorProgress.TotalDurabilityCredits;

            pieceProgress.RepairsInIncrement++;

            // Check if we've earned a repair credit
            while (pieceProgress.RepairsInIncrement >= pieceProgress.CurrentRepairIncrementSize &&
                   armorProgress.TotalDurabilityCredits < MaxArmorDurabilityPercent)
            {
                pieceProgress.RepairCredits++;
                armorProgress.TotalDurabilityCredits++;
                pieceProgress.RepairsInIncrement -= pieceProgress.CurrentRepairIncrementSize;
                pieceProgress.CurrentRepairIncrementSize += ArmorRepairIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned repair credit {pieceProgress.RepairCredits} with {armorCode}");
            }

            pendingArmorProgressSave = true;

            if (armorProgress.TotalDurabilityCredits > oldDurabilityCredits)
            {
                UpdateSkillActivityDay(playerUid, "armor");
                ApplyArmorBonusesStatic(player, armorProgress.TotalDurabilityCredits, armorProgress.TotalWalkSpeedCredits);

                // Notify player of level up with raw improvement (shows progress even when capped)
                NotifyLevelUp(player,
                    Lang.Get("seraphleveling:message-armor-repair-level-up", armorProgress.TotalDurabilityCredits, armorProgress.TotalDurabilityCredits));

                // Check for trait unlocks that depend on armor durability
                CheckHardyHealthUnlock(player);
                CheckMercilessUnlock(player);
            }
        }

        /// <summary>
        /// Apply walking speed bonus to a player based on their level.
        /// Returns the actual applied bonus percentage.
        /// Stats are always applied (they're not persistent). WatchedAttributes only sync when values change.
        /// </summary>
        public static int ApplyWalkingBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaFleetfooted = cache?.HasFleetfooted ?? PlayerHasVanillaFleetfootedStatic(player.Entity);
            int vanillaFleetfootedBonus = hasVanillaFleetfooted ? VANILLA_FLEETFOOTED_WALK_BONUS : 0;

            // Calculate raw bonus from level (1% per level)
            float rawBonus = level * 0.01f;

            // Cap earned bonus so total (vanilla + earned) doesn't exceed MaxWalkingSpeedPercent
            float maxEarnableBonus = (MaxWalkingSpeedPercent - vanillaFleetfootedBonus) / 100f;
            float bonus = Math.Min(rawBonus, Math.Max(0, maxEarnableBonus));
            int bonusPercent = (int)(bonus * 100);

            // Always apply stats (they're not persistent)
            player.Entity.Stats.Set("walkspeed", WALKING_STAT_CODE, bonus, false);

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_WALKING_LEVEL, -1);
            int oldBonus = watchedAttrs.GetInt(WATCHED_WALKING_BONUS, -1);

            bool valuesChanged = (oldLevel != level) || (oldBonus != bonusPercent);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_WALKING_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_WALKING_BONUS, bonusPercent);
                watchedAttrs.SetBool("sitHasVanillaFleetfooted", hasVanillaFleetfooted);

                // Add our trait to extraTraits only if player doesn't already have Fleetfooted
                UpdateExtraTraitStatic(player.Entity, WALKING_TRAIT_CODE, level > 0 && !hasVanillaFleetfooted);

                watchedAttrs.MarkPathDirty(WATCHED_WALKING_LEVEL);
            }

            return bonusPercent;
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
            if (!IsSkillDisabled("forager") && IsWildCropBlock(oldblockId, blockSel?.Position))
            {
                ProcessWildCropBroken(byPlayer);
            }

            // Check for Pilferer progression (cracked vessels only - they can't be re-placed)
            if (!IsSkillDisabled("pilferer") && IsCrackedVesselBlock(oldblockId))
            {
                ProcessVesselBreak(byPlayer);
            }

            // Skip mining progression if disabled
            if (IsSkillDisabled("mining")) return;

            // Check if player is using a pickaxe for mining progression
            string pickaxeCode = GetHeldPickaxeCode(byPlayer);
            if (pickaxeCode == null) return; // Not using a pickaxe, skip mining

            // Check block type and get points
            int points = GetBlockPoints(oldblockId);
            if (points <= 0) return; // Not a stone/ore block, skip

            string playerUid = byPlayer.PlayerUID;

            // Get or create player progress data
            var playerProgress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());

            // Get the player-specific max credits (accounts for Weak/Claustrophobic penalties)
            int maxCredits = GetMaxMiningCredits(byPlayer.Entity);

            // Skip all processing if already at max - completely invisible
            if (playerProgress.TotalCredits >= maxCredits) return;

            // Get or create progress for this specific pickaxe type
            var pickaxeProgress = playerProgress.GetPickaxeProgress(pickaxeCode);

            int oldCredits = playerProgress.TotalCredits;

            // Apply sleep buff multiplier to points
            int modifiedPoints = ApplyXPMultiplier(playerUid, points);

            // Add points to THIS pickaxe's progress
            pickaxeProgress.BlocksInIncrement += modifiedPoints;

            // Check if we've earned any new credits with this pickaxe
            while (pickaxeProgress.BlocksInIncrement >= pickaxeProgress.CurrentIncrementSize && playerProgress.TotalCredits < maxCredits)
            {
                // Earn a credit
                playerProgress.TotalCredits++;
                pickaxeProgress.BlocksInIncrement -= pickaxeProgress.CurrentIncrementSize;
                pickaxeProgress.CurrentIncrementSize += IncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {byPlayer.PlayerName} earned credit {playerProgress.TotalCredits} with {pickaxeCode}, next requires {pickaxeProgress.CurrentIncrementSize} points");
            }

            pendingMiningProgressSave = true;

            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "mining");

            // If credits increased, update the stat and notify player
            if (playerProgress.TotalCredits > oldCredits)
            {
                ApplyMiningBonus(byPlayer, playerProgress.TotalCredits);

                // Notify player of level up with the level as the bonus (the raw mining speed improvement)
                // This shows the true progress even when negative traits are still being cancelled
                NotifyLevelUp(byPlayer,
                    Lang.Get("seraphleveling:message-mining-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits));

                // Check for trait unlocks that depend on mining level
                CheckHardyHealthUnlock(byPlayer);
                CheckClaustrophobicRemoval(byPlayer);
            }
        }

        /// <summary>
        /// Called every 500ms to track walking distance for all online players.
        /// Calculates 2D horizontal distance moved (ignoring Y-axis for climbing/falling).
        /// </summary>
        private void OnWalkingTick(float dt)
        {
            // Skip walking progression if disabled
            if (IsSkillDisabled("walking")) return;

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
                var playerProgress = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData
                {
                    CurrentIncrementSize = BaseBlocksWalkedPerIncrement
                });

                playerProgress.DoEvent(player, distance);
            }
        }

        /// <summary>
        /// Called every 1000ms (1 second) to track time spent at full saturation for all online players.
        /// Players at maximum saturation (1500/1500) accumulate time toward hunger rate reduction.
        /// </summary>
        private void OnHungerTick(float dt)
        {
            // Skip hunger progression if disabled
            if (IsSkillDisabled("hunger")) return;

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

                // Get or create player progress data
                var playerProgress = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
                {
                    CurrentIncrementSize = BaseSecondsPerIncrement
                });

                // Calculate player-specific max credits (Ravenous players need more)
                int playerMaxCredits = CalculateMaxHungerCredits(player.Entity);

                // Skip all processing if already at max - completely invisible
                if (playerProgress.TotalCredits >= playerMaxCredits) continue;

                int oldCredits = playerProgress.TotalCredits;

                // Apply sleep buff multiplier to time (since tick is every 1000ms)
                float modifiedSeconds = ApplyXPMultiplier(playerUid, 1f);

                // Add time to progress
                playerProgress.SecondsInIncrement += modifiedSeconds;

                // Check if we've earned any new credits
                while (playerProgress.SecondsInIncrement >= playerProgress.CurrentIncrementSize && playerProgress.TotalCredits < playerMaxCredits)
                {
                    // Earn a credit
                    playerProgress.TotalCredits++;
                    playerProgress.SecondsInIncrement -= playerProgress.CurrentIncrementSize;
                    playerProgress.CurrentIncrementSize += HungerIncrementStep;

                    ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned hunger credit {playerProgress.TotalCredits}/{playerMaxCredits}, next requires {playerProgress.CurrentIncrementSize} seconds");
                }

                // Mark for saving if any progress was made
                if (playerProgress.SecondsInIncrement > 0 || playerProgress.TotalCredits > oldCredits)
                {
                    pendingHungerProgressSave = true;
                }

                // If credits increased, update the stat and notify player
                if (playerProgress.TotalCredits > oldCredits)
                {
                    UpdateSkillActivityDay(playerUid, "hunger");
                    ApplyHungerBonusStatic(player, playerProgress.TotalCredits);

                    // Notify player of level up with raw improvement (shows progress even when cancelling Ravenous)
                    NotifyLevelUp(player,
                        Lang.Get("seraphleveling:message-hunger-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits));
                }
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
        private static CachedVanillaTraits GetCachedTraits(string playerUid)
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

            // Apply mining bonus (Stats always applied, WatchedAttributes only sync if changed)
            var miningProg = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            int miningCredits = miningProg.TotalCredits;
            ApplyMiningBonus(byPlayer, miningCredits);
            if (miningCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied mining bonus {miningCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply melee bonus (Stats always applied, WatchedAttributes only sync if changed)
            var meleeProg = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
            int meleeCredits = meleeProg.TotalCredits;
            ApplyMeleeBonusStatic(byPlayer, meleeCredits);
            if (meleeCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied melee bonus {meleeCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply ranged bonus (Stats always applied, WatchedAttributes only sync if changed)
            var rangedProg = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
            int rangedCredits = rangedProg.TotalCredits;
            ApplyRangedBonusStatic(byPlayer, rangedCredits);
            if (rangedCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied ranged bonus {rangedCredits} credits to player {byPlayer.PlayerName}");
            }

            // Apply walking bonus (Stats always applied, WatchedAttributes only sync if changed)
            WalkingProgressData.HandleLogin(byPlayer);

            // Apply hunger bonus (Stats always applied, WatchedAttributes only sync if changed)
            var hungerProg = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData
            {
                CurrentIncrementSize = BaseSecondsPerIncrement
            });
            int hungerCredits = hungerProg.TotalCredits;
            ApplyHungerBonusStatic(byPlayer, hungerCredits);
            if (hungerCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied hunger bonus -{hungerCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply armor bonuses (Stats always applied, WatchedAttributes only sync if changed)
            var armorProg = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            ApplyArmorBonusesStatic(byPlayer, armorProg.TotalDurabilityCredits, armorProg.TotalWalkSpeedCredits);
            if (armorProg.TotalDurabilityCredits > 0 || armorProg.TotalWalkSpeedCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied armor bonuses: +{armorProg.TotalDurabilityCredits}% durability, -{armorProg.TotalWalkSpeedCredits}% walk speed penalty to player {byPlayer.PlayerName}");
            }

            // Apply clothier bonus
            var clothierProg = ClothierProgress.GetOrAdd(playerUid, _ => new ClothierProgressData());
            ApplyClothierBonusStatic(byPlayer, clothierProg);
            if (clothierProg.SewingKitUnlocked)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied clothier unlock to player {byPlayer.PlayerName}");
            }

            // Apply mender bonus
            var menderProg = MenderProgress.GetOrAdd(playerUid, _ => new MenderProgressData
            {
                CurrentIncrementSize = BaseMenderRepairsPerIncrement
            });
            int menderCredits = menderProg.TotalCredits;
            ApplyMenderBonusStatic(byPlayer, menderCredits);
            if (menderCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied mender bonus +{menderCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply pilferer bonus
            var pilfererProg = PilfererProgress.GetOrAdd(playerUid, _ => new PilfererProgressData
            {
                CurrentIncrementSize = BasePilfererPointsPerIncrement
            });
            int pilfererCredits = pilfererProg.TotalCredits;
            ApplyPilfererBonusStatic(byPlayer, pilfererCredits);
            if (pilfererCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied pilferer bonus +{pilfererCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply resourceful bonus
            var resourcefulProg = ResourcefulProgress.GetOrAdd(playerUid, _ => new ResourcefulProgressData
            {
                CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement
            });
            int resourcefulCredits = resourcefulProg.TotalCredits;
            ApplyResourcefulBonusStatic(byPlayer, resourcefulCredits);
            if (resourcefulCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied resourceful bonus +{resourcefulCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply forager bonus
            var foragerProg = ForagerProgress.GetOrAdd(playerUid, _ => new ForagerProgressData
            {
                CurrentIncrementSize = BaseForagerCropsPerIncrement
            });
            int foragerCredits = foragerProg.TotalCredits;
            ApplyForagerBonusStatic(byPlayer, foragerCredits);
            if (foragerCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied forager bonus +{foragerCredits}% to player {byPlayer.PlayerName}");
            }

            // Apply furtive bonus
            var furtiveProg = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData
            {
                CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement
            });
            int furtiveCredits = furtiveProg.TotalCredits;
            ApplyFurtiveBonusStatic(byPlayer, furtiveCredits);
            if (furtiveCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied furtive bonus -{furtiveCredits}% detection to player {byPlayer.PlayerName}");
            }

            // Apply precise bonus
            var preciseProg = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());
            int preciseCredits = preciseProg.TotalCredits;
            ApplyPreciseBonusStatic(byPlayer, preciseCredits);
            if (preciseCredits > 0)
            {
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied precise bonus +{preciseCredits}% mechanical damage to player {byPlayer.PlayerName}");
            }

            // Apply technical unlock
            var technicalProg = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            if (technicalProg.IsUnlocked)
            {
                ApplyTechnicalBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied technical unlock to player {byPlayer.PlayerName}");
            }

            // Apply hardy health unlock
            var hardyHealthProg = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());
            if (hardyHealthProg.IsUnlocked)
            {
                ApplyHardyHealthBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied hardy health +{HardyHealthBonus} HP to player {byPlayer.PlayerName}");
            }

            // Apply bowyer unlock
            var bowyerProg = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());
            if (bowyerProg.IsUnlocked)
            {
                ApplyBowyerBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied bowyer unlock to player {byPlayer.PlayerName}");
            }

            // Apply improviser unlock
            var improviserProg = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());
            if (improviserProg.IsUnlocked)
            {
                ApplyImproviserBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied improviser unlock to player {byPlayer.PlayerName}");
            }

            // Apply tinkerer unlock
            var tinkererProg = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());
            if (tinkererProg.IsUnlocked)
            {
                ApplyTinkererBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied tinkerer unlock to player {byPlayer.PlayerName}");
            }

            // Apply merciless unlock
            var mercilessProg = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());
            if (mercilessProg.IsUnlocked)
            {
                ApplyMercilessBonusStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied merciless unlock to player {byPlayer.PlayerName}");
            }

            // Apply claustrophobic removal
            var claustrophobicProg = ClaustrophobicRemovalProgress.GetOrAdd(playerUid, _ => new ClaustrophobicRemovalProgressData());
            if (claustrophobicProg.IsRemoved)
            {
                ApplyClaustrophobicRemovalStatic(byPlayer, true);
                ServerApi.Logger.Debug($"[SeraphLeveling] Applied claustrophobic removal to player {byPlayer.PlayerName}");
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

        /// <summary>
        /// Also handles Weak and Claustrophobic negative trait cancellation.
        /// Stats are always applied (they're not persistent). WatchedAttributes only sync when values change.
        /// Returns the actual applied bonus percentage (0-100 scale).
        /// </summary>
        private int ApplyMiningBonus(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaHardy = cache?.HasHardy ?? PlayerHasVanillaHardy(player.Entity);
            bool hasWeak = cache?.HasWeak ?? PlayerHasVanillaWeak(player.Entity);
            bool hasClaustrophobic = cache?.HasClaustrophobic ?? PlayerHasVanillaClaustrophobic(player.Entity);

            int vanillaHardyBonus = hasVanillaHardy ? VANILLA_HARDY_MINING_BONUS : 0;

            // Calculate remaining negative trait penalties
            int weakMiningRemaining = hasWeak ? CalculateRemainingPenalty(VANILLA_WEAK_MINING_PENALTY, level) : 0;
            // HP penalty is tied to mining penalty - when mining penalty is cancelled (at level 10), HP is also cancelled
            int weakHpRemaining = weakMiningRemaining > 0 ? VANILLA_WEAK_HP_PENALTY : 0;
            int claustrophobicMiningRemaining = hasClaustrophobic ? CalculateRemainingPenalty(VANILLA_CLAUSTROPHOBIC_MINING_PENALTY, level) : 0;
            // Ore penalty is tied to mining penalty - when mining penalty is cancelled (at level 10), ore is also cancelled
            int claustrophobicOreRemaining = claustrophobicMiningRemaining > 0 ? VANILLA_CLAUSTROPHOBIC_ORE_PENALTY : 0;

            // Calculate net bonus after cancelling negative traits
            // Negative trait penalty must be fully cancelled before bonus starts showing
            int totalNegativePenalty = 0;
            if (hasWeak) totalNegativePenalty += VANILLA_WEAK_MINING_PENALTY;
            if (hasClaustrophobic) totalNegativePenalty += VANILLA_CLAUSTROPHOBIC_MINING_PENALTY;

            int netLevel = Math.Max(0, level - totalNegativePenalty);

            // Cap earned bonus so total (vanilla + earned) doesn't exceed MaxMiningSpeedPercent
            int maxEarnableBonus = MaxMiningSpeedPercent - vanillaHardyBonus;
            int bonusPercent = Math.Min(netLevel, Math.Max(0, maxEarnableBonus));

            float bonus = bonusPercent * 0.01f;

            // Always apply stats (they're not persistent)
            // Set the mining speed stat
            player.Entity.Stats.Set("miningSpeedMul", MINING_STAT_CODE, bonus, false);

            // Counter-stats: when a vanilla negative trait's mining penalty is fully cancelled
            // (remaining == 0), apply a +penalty counter on the same stat so the ACTUAL applied
            // mining speed matches the displayed value. Without this, Hunter (Claustrophobic)
            // and Tailor (Weak) would land at a functional +40% mining at maxall (vanilla -10%
            // still applied, our +50% on top, net +40%) while their displayed +50% suggests
            // parity with other classes.
            if (hasClaustrophobic)
            {
                if (claustrophobicMiningRemaining == 0)
                {
                    // Negate the -10% mining speed penalty by applying +10%
                    player.Entity.Stats.Set("miningSpeedMul", "sitClaustrophobicMiningCancel", VANILLA_CLAUSTROPHOBIC_MINING_PENALTY * 0.01f, false);
                    // Negate the -15% ore drop penalty by applying +15%
                    player.Entity.Stats.Set("oreDropRate", "sitClaustrophobicOreCancel", VANILLA_CLAUSTROPHOBIC_ORE_PENALTY * 0.01f, false);
                }
                else
                {
                    player.Entity.Stats.Remove("miningSpeedMul", "sitClaustrophobicMiningCancel");
                    player.Entity.Stats.Remove("oreDropRate", "sitClaustrophobicOreCancel");
                }
            }

            // When Weak mining penalty is fully cancelled, also negate the HP penalty AND the mining speed penalty
            if (hasWeak)
            {
                if (weakMiningRemaining == 0)
                {
                    // Negate the -2 HP penalty by applying +2 HP
                    player.Entity.Stats.Set("maxhealthExtraPoints", WEAK_HP_CANCEL_STAT_CODE, VANILLA_WEAK_HP_PENALTY, false);
                    // Negate the -10% mining speed penalty by applying +10%
                    player.Entity.Stats.Set("miningSpeedMul", "sitWeakMiningCancel", VANILLA_WEAK_MINING_PENALTY * 0.01f, false);
                }
                else
                {
                    player.Entity.Stats.Remove("maxhealthExtraPoints", WEAK_HP_CANCEL_STAT_CODE);
                    player.Entity.Stats.Remove("miningSpeedMul", "sitWeakMiningCancel");
                }
            }

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_MINING_LEVEL, -1);
            int oldBonus = watchedAttrs.GetInt(WATCHED_MINING_BONUS, -1);
            int oldClaustoMining = watchedAttrs.GetInt(WATCHED_CLAUSTROPHOBIC_MINING_REMAINING, -1);

            bool valuesChanged = (oldLevel != level) || (oldBonus != bonusPercent) || (oldClaustoMining != claustrophobicMiningRemaining);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_MINING_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_MINING_BONUS, bonusPercent);
                watchedAttrs.SetBool("sitHasVanillaHardy", hasVanillaHardy);

                // Sync negative trait status
                watchedAttrs.SetBool("sitHasWeak", hasWeak);
                watchedAttrs.SetInt(WATCHED_WEAK_MINING_REMAINING, weakMiningRemaining);
                watchedAttrs.SetInt(WATCHED_WEAK_HP_REMAINING, weakHpRemaining);
                watchedAttrs.SetBool("sitHasClaustrophobic", hasClaustrophobic);
                watchedAttrs.SetInt(WATCHED_CLAUSTROPHOBIC_MINING_REMAINING, claustrophobicMiningRemaining);
                watchedAttrs.SetInt(WATCHED_CLAUSTROPHOBIC_ORE_REMAINING, claustrophobicOreRemaining);

                // Add our trait to extraTraits only if:
                // - Player doesn't already have Hardy AND
                // - All negative mining penalties are cancelled (bonusPercent > 0)
                UpdateExtraTrait(player.Entity, MINING_TRAIT_CODE, bonusPercent > 0 && !hasVanillaHardy);

                // Only call MarkPathDirty once at the end (batched update)
                watchedAttrs.MarkPathDirty(WATCHED_MINING_LEVEL);
            }

            return bonusPercent;
        }

        /// <summary>
        /// Checks if the player's class has the vanilla Hardy trait.
        /// </summary>
        private bool PlayerHasVanillaHardy(EntityPlayer entity)
        {
            // Get the player's class traits (not extraTraits which we manage)
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);

            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("hardy", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // Fallback: check known classes that have Hardy
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("blackguard", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds or removes a trait from the player's extraTraits array.
        /// </summary>
        private void UpdateExtraTrait(EntityPlayer entity, string traitCode, bool shouldHave)
        {
            // Get current extra traits
            string[] currentTraits = entity.WatchedAttributes.GetStringArray("extraTraits", null) ?? Array.Empty<string>();
            bool hasTrait = currentTraits.Contains(traitCode);

            if (shouldHave && !hasTrait)
            {
                // Add the trait
                var newTraits = currentTraits.Append(traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("extraTraits", newTraits);
                entity.WatchedAttributes.MarkPathDirty("extraTraits");
                ServerApi.Logger.Debug($"[SeraphLeveling] Added trait {traitCode} to player");
            }
            else if (!shouldHave && hasTrait)
            {
                // Remove the trait
                var newTraits = currentTraits.Where(t => t != traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("extraTraits", newTraits);
                entity.WatchedAttributes.MarkPathDirty("extraTraits");
                ServerApi.Logger.Debug($"[SeraphLeveling] Removed trait {traitCode} from player");
            }
        }

        /// <summary>
        /// Calculate the mining speed bonus as a float (0.0 to 1.5 for 0% to 150%).
        /// Each credit gives 1% bonus, capped at MaxMiningSpeedPercent.
        /// </summary>
        public static float CalculateMiningBonus(int credits)
        {
            float bonus = credits * 0.01f;
            return Math.Min(bonus, MaxMiningSpeedPercent / 100f);
        }

        /// <summary>
        /// Calculate the mining speed bonus as an integer percentage (0 to 150).
        /// Each credit gives 1% bonus, capped at MaxMiningSpeedPercent.
        /// </summary>
        public static int CalculateMiningBonusPercent(int credits)
        {
            return Math.Min(credits, MaxMiningSpeedPercent);
        }

        /// <summary>
        /// Calculate the maximum credits (level) based on the bonus cap.
        /// </summary>
        public static int CalculateMaxCredits()
        {
            return MaxMiningSpeedPercent;
        }

        /// <summary>
        /// Get the maximum mining credits a player can earn based on their traits.
        /// Players with Weak or Claustrophobic traits can earn extra credits
        /// to compensate for the penalty before gaining positive bonuses.
        /// </summary>
        public static int GetMaxMiningCredits(EntityPlayer entity)
        {
            if (entity == null) return MaxMiningSpeedPercent;

            bool hasWeak = PlayerHasVanillaWeak(entity);
            bool hasClaustrophobic = PlayerHasVanillaClaustrophobic(entity);

            // Weak penalty is 10% mining speed, need 10 extra levels to cancel it
            if (hasWeak)
            {
                return MaxMiningSpeedPercent + VANILLA_WEAK_MINING_PENALTY;
            }

            // Claustrophobic penalty is 10% mining speed, need 10 extra levels to cancel it
            if (hasClaustrophobic)
            {
                return MaxMiningSpeedPercent + VANILLA_CLAUSTROPHOBIC_MINING_PENALTY;
            }

            return MaxMiningSpeedPercent;
        }

        // Server-side Harmony instance for melee damage tracking
        private Harmony serverHarmony;

        /// <summary>
        /// Apply Harmony patches for server-side melee damage tracking.
        /// </summary>
        private void ApplyServerHarmonyPatches(ICoreServerAPI api)
        {
            serverHarmony = new Harmony("seraphleveling.server");

            try
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
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[SeraphLeveling] Failed to apply server Harmony patches: {ex.Message}");
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

            // Check if melee skill is disabled
            if (IsSkillDisabled("melee")) return;

            string playerUid = attackerPlayer.PlayerUID;

            // Get or create player progress data
            var playerProgress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());

            // Get the player-specific max credits (accounts for Farsighted/Nervous penalties)
            int maxCredits = GetMaxMeleeCredits(attackerPlayer.Entity);

            // Skip all processing if already at max - completely invisible
            if (playerProgress.TotalCredits >= maxCredits) return;

            // Get or create progress for this specific weapon type
            var weaponProgress = playerProgress.GetWeaponProgress(weaponType);

            int oldCredits = playerProgress.TotalCredits;

            // Apply sleep buff multiplier to damage
            float modifiedDamage = ApplyXPMultiplier(playerUid, damage);

            // Add damage to THIS weapon type's progress
            weaponProgress.DamageInIncrement += modifiedDamage;

            // Check if we've earned any new credits with this weapon type
            while (weaponProgress.DamageInIncrement >= weaponProgress.CurrentIncrementSize && playerProgress.TotalCredits < maxCredits)
            {
                // Earn a credit
                playerProgress.TotalCredits++;
                weaponProgress.DamageInIncrement -= weaponProgress.CurrentIncrementSize;
                weaponProgress.CurrentIncrementSize += MeleeIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {attackerPlayer.PlayerName} earned melee credit {playerProgress.TotalCredits} with {weaponType}, next requires {weaponProgress.CurrentIncrementSize} damage");
            }

            pendingMeleeProgressSave = true;

            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "melee");

            // If credits increased, update the stat and notify player
            if (playerProgress.TotalCredits > oldCredits)
            {
                ApplyMeleeBonusStatic(attackerPlayer, playerProgress.TotalCredits);

                // Notify player of level up with raw improvement (shows progress even when cancelling negative traits)
                NotifyLevelUp(attackerPlayer,
                    Lang.Get("seraphleveling:message-melee-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits));

                // Check for trait unlocks that depend on melee damage
                CheckMercilessUnlock(attackerPlayer);
            }
        }

        /// <summary>
        /// Static version of ApplyMeleeBonus for use from Harmony patches.
        /// Also handles Farsighted and Nervous negative trait cancellation.
        /// Stats are always applied (they're not persistent). WatchedAttributes only sync when values change.
        /// </summary>
        private static int ApplyMeleeBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaSoldier = cache?.HasSoldier ?? PlayerHasVanillaSoldierStatic(player.Entity);
            bool hasFarsighted = cache?.HasFarsighted ?? PlayerHasVanillaFarsighted(player.Entity);
            bool hasNervous = cache?.HasNervous ?? PlayerHasVanillaNervous(player.Entity);

            int vanillaSoldierBonus = hasVanillaSoldier ? VANILLA_SOLDIER_MELEE_BONUS : 0;

            // Calculate remaining negative trait penalties
            int farsightedRemaining = hasFarsighted ? CalculateRemainingPenalty(VANILLA_FARSIGHTED_MELEE_PENALTY, level) : 0;
            int nervousRemaining = hasNervous ? CalculateRemainingPenalty(VANILLA_NERVOUS_MELEE_PENALTY, level) : 0;

            // Calculate net bonus after cancelling negative traits
            int netBonusPercent = level;
            if (hasFarsighted)
            {
                netBonusPercent = Math.Max(0, level - VANILLA_FARSIGHTED_MELEE_PENALTY);
            }
            if (hasNervous)
            {
                netBonusPercent = Math.Max(0, level - VANILLA_NERVOUS_MELEE_PENALTY);
            }

            // Cap earned bonus so total (vanilla + earned) doesn't exceed MaxMeleeDamagePercent
            int maxEarnableBonus = MaxMeleeDamagePercent - vanillaSoldierBonus;
            netBonusPercent = Math.Min(netBonusPercent, Math.Max(0, maxEarnableBonus));

            float bonus = netBonusPercent * 0.01f;

            // Always apply stats (they're not persistent)
            player.Entity.Stats.Set("meleeWeaponsDamage", MELEE_STAT_CODE, bonus, false);

            // Counter-stats: when Farsighted/Nervous melee penalty is fully cancelled, apply a
            // +penalty counter so functional melee damage matches the displayed cap. Without
            // this, Hunter (Farsighted) and Malefactor/Clockmaker (Nervous) would land on a
            // functional +35% melee at maxall while their displayed +50% suggests parity.
            if (hasFarsighted)
            {
                if (farsightedRemaining == 0)
                    player.Entity.Stats.Set("meleeWeaponsDamage", "sitFarsightedMeleeCancel", VANILLA_FARSIGHTED_MELEE_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("meleeWeaponsDamage", "sitFarsightedMeleeCancel");
            }
            if (hasNervous)
            {
                if (nervousRemaining == 0)
                    player.Entity.Stats.Set("meleeWeaponsDamage", "sitNervousMeleeCancel", VANILLA_NERVOUS_MELEE_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("meleeWeaponsDamage", "sitNervousMeleeCancel");
            }

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_MELEE_LEVEL, -1);
            int oldBonus = watchedAttrs.GetInt(WATCHED_MELEE_BONUS, -1);

            bool valuesChanged = (oldLevel != level) || (oldBonus != netBonusPercent);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonus to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_MELEE_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_MELEE_BONUS, netBonusPercent);
                watchedAttrs.SetBool("sitHasVanillaSoldier", hasVanillaSoldier);

                // Sync negative trait status
                watchedAttrs.SetBool("sitHasFarsighted", hasFarsighted);
                watchedAttrs.SetInt(WATCHED_FARSIGHTED_REMAINING, farsightedRemaining);
                watchedAttrs.SetBool("sitHasNervous", hasNervous);
                watchedAttrs.SetInt(WATCHED_NERVOUS_REMAINING, nervousRemaining);

                // Add our trait to extraTraits only if player doesn't already have Soldier
                UpdateExtraTraitStatic(player.Entity, MELEE_TRAIT_CODE, level > 0 && !hasVanillaSoldier);

                watchedAttrs.MarkPathDirty(WATCHED_MELEE_LEVEL);
            }

            // Apply CO melee tier bonus if CO is enabled (Frightened of Melee / Melee Expert)
            if (IsCOCompatEnabled)
            {
                ApplyCOMeleeTier(player, level);
            }

            return netBonusPercent;
        }

        /// <summary>
        /// Static version of PlayerHasVanillaSoldier for use from Harmony patches.
        /// </summary>
        private static bool PlayerHasVanillaSoldierStatic(EntityPlayer entity)
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

            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("blackguard", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Static version of UpdateExtraTrait for use from Harmony patches.
        /// </summary>
        private static void UpdateExtraTraitStatic(EntityPlayer entity, string traitCode, bool shouldHave)
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
        /// Updates the characterTraits array to add or remove a trait.
        /// This is used for traits that unlock recipes (like Clothier).
        /// Unlike extraTraits which is only for UI display, characterTraits is
        /// what the game actually checks for recipe requirements.
        /// </summary>
        private static void UpdateCharacterTraitStatic(EntityPlayer entity, string traitCode, bool shouldHave)
        {
            string[] currentTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null) ?? Array.Empty<string>();
            bool hasTrait = currentTraits.Contains(traitCode);

            if (shouldHave && !hasTrait)
            {
                var newTraits = currentTraits.Append(traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("characterTraits", newTraits);
                entity.WatchedAttributes.MarkPathDirty("characterTraits");
            }
            else if (!shouldHave && hasTrait)
            {
                var newTraits = currentTraits.Where(t => t != traitCode).ToArray();
                entity.WatchedAttributes.SetStringArray("characterTraits", newTraits);
                entity.WatchedAttributes.MarkPathDirty("characterTraits");
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

            // Check if ranged skill is disabled
            if (IsSkillDisabled("ranged")) return;

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

            // Get or create player progress data
            var playerProgress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());

            // Get the player-specific max credits (accounts for Nearsighted/Frail penalties)
            int maxCredits = GetMaxRangedCredits(attackerPlayer.Entity);

            // Skip remaining credit processing if already at max - completely invisible
            if (playerProgress.TotalCredits >= maxCredits) return;

            // Get or create progress for this specific weapon combination
            var weaponProgress = playerProgress.GetWeaponProgress(weaponCombo);

            int oldCredits = playerProgress.TotalCredits;

            // Apply sleep buff multiplier to damage
            float modifiedDamage = ApplyXPMultiplier(playerUid, damage);

            // Add damage to THIS weapon combination's progress
            weaponProgress.DamageInIncrement += modifiedDamage;

            // Check if we've earned any new credits with this weapon combination
            while (weaponProgress.DamageInIncrement >= weaponProgress.CurrentIncrementSize && playerProgress.TotalCredits < maxCredits)
            {
                // Earn a credit
                playerProgress.TotalCredits++;
                weaponProgress.DamageInIncrement -= weaponProgress.CurrentIncrementSize;
                weaponProgress.CurrentIncrementSize += RangedIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {attackerPlayer.PlayerName} earned ranged credit {playerProgress.TotalCredits} with {weaponCombo}, next requires {weaponProgress.CurrentIncrementSize} damage");
            }

            pendingRangedProgressSave = true;

            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "ranged");

            // If credits increased, update the stat and notify player
            if (playerProgress.TotalCredits > oldCredits)
            {
                ApplyRangedBonusStatic(attackerPlayer, playerProgress.TotalCredits);

                // Notify player of level up with raw improvement (shows progress even when cancelling negative traits)
                NotifyLevelUp(attackerPlayer,
                    Lang.Get("seraphleveling:message-ranged-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits, playerProgress.TotalCredits, playerProgress.TotalCredits));

                // Check for trait unlocks that depend on ranged damage
                CheckBowyerUnlock(attackerPlayer);
            }
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

            string playerUid = player.PlayerUID;
            var progress = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Apply sleep buff multiplier if active
            float modifiedDamage = ApplyXPMultiplier(playerUid, damage);
            progress.TotalBowDamage += modifiedDamage;
            pendingBowyerProgressSave = true;

            player.Entity.WatchedAttributes.SetFloat(WATCHED_BOWYER_BOW_DAMAGE, progress.TotalBowDamage);

            // Check if unlock threshold is reached
            CheckBowyerUnlock(player);
        }

        /// <summary>
        /// Track thrown rock damage for Improviser unlock.
        /// </summary>
        private static void TrackImproviserRockDamage(IServerPlayer player, float damage)
        {
            if (player?.Entity == null || damage <= 0) return;

            string playerUid = player.PlayerUID;
            var progress = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Apply sleep buff multiplier if active
            float modifiedDamage = ApplyXPMultiplier(playerUid, damage);
            progress.TotalRockDamage += modifiedDamage;
            pendingImproviserProgressSave = true;

            player.Entity.WatchedAttributes.SetFloat(WATCHED_IMPROVISER_ROCK_DAMAGE, progress.TotalRockDamage);

            if (DebugLoggingEnabled)
            {
                ServerApi?.Logger?.Debug($"[SeraphLeveling] Improviser rock damage tracked for {player.PlayerName}: +{modifiedDamage:F1} (total {progress.TotalRockDamage:F1}/{ImproviserRockDamageThreshold})");
            }

            // Check if unlock threshold is reached
            CheckImproviserUnlock(player);
        }

        /// <summary>
        /// Static version of ApplyRangedBonus for use from Harmony patches.
        /// Also handles Nearsighted and Frail negative trait cancellation.
        /// Stats are always applied (they're not persistent). WatchedAttributes only sync when values change.
        /// Returns (damageBonus, accuracyBonus, distanceBonus) as percentages.
        /// </summary>
        public static (int damage, int accuracy, int distance) ApplyRangedBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return (0, 0, 0);

            // When Combat Overhaul is loaded, apply tier-based bonuses instead of percentages
            if (IsCOCompatEnabled)
            {
                return ApplyRangedBonusCO(player, level);
            }

            // Use cached vanilla traits if available, otherwise fall back to direct check
            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaFocused = cache?.HasFocused ?? PlayerHasVanillaFocusedStatic(player.Entity);
            bool hasNearsighted = cache?.HasNearsighted ?? PlayerHasVanillaNearsighted(player.Entity);
            bool hasFrail = cache?.HasFrail ?? PlayerHasVanillaFrail(player.Entity);

            int vanillaDamage = hasVanillaFocused ? VANILLA_FOCUSED_DAMAGE_BONUS : 0;
            int vanillaAccuracy = hasVanillaFocused ? VANILLA_FOCUSED_ACCURACY_BONUS : 0;
            int vanillaDistance = hasVanillaFocused ? VANILLA_FOCUSED_DISTANCE_BONUS : 0;

            // Calculate remaining negative trait penalties
            int nearsightedRemaining = hasNearsighted ? CalculateRemainingPenalty(VANILLA_NEARSIGHTED_RANGED_PENALTY, level) : 0;
            int frailDistanceRemaining = hasFrail ? CalculateRemainingPenalty(VANILLA_FRAIL_DISTANCE_PENALTY, level) : 0;
            // HP penalty is tied to distance penalty - when distance penalty is cancelled (at level 25), HP is also cancelled
            float frailHpRemaining = frailDistanceRemaining > 0 ? VANILLA_FRAIL_HP_PENALTY : 0f;

            // Calculate net bonus after cancelling negative traits
            int netDamageLevel = level;
            int netDistanceLevel = level;

            if (hasNearsighted)
            {
                netDamageLevel = Math.Max(0, level - VANILLA_NEARSIGHTED_RANGED_PENALTY);
            }
            if (hasFrail)
            {
                netDistanceLevel = Math.Max(0, level - VANILLA_FRAIL_DISTANCE_PENALTY);
            }

            // Calculate earnable bonuses (each stat capped individually)
            int earnableDamage = Math.Max(0, MaxRangedDamagePercent - vanillaDamage);
            int earnableAccuracy = Math.Max(0, MaxRangedAccuracyPercent - vanillaAccuracy);
            int earnableDistance = Math.Max(0, MaxRangedDistancePercent - vanillaDistance);

            // Calculate actual bonuses from level (using net level after penalty cancellation)
            int damagePct = Math.Min(netDamageLevel, earnableDamage);
            int accuracyPct = Math.Min(level, earnableAccuracy);
            int distancePct = Math.Min(netDistanceLevel, earnableDistance);

            float damageBonus = damagePct * 0.01f;
            float accuracyBonus = accuracyPct * 0.01f;
            float distanceBonus = distancePct * 0.01f;

            // Always apply stats (they're not persistent)
            player.Entity.Stats.Set("rangedWeaponsDamage", RANGED_DAMAGE_STAT_CODE, damageBonus, false);
            player.Entity.Stats.Set("rangedWeaponsAcc", RANGED_ACCURACY_STAT_CODE, accuracyBonus, false);
            player.Entity.Stats.Set("bowDrawingStrength", RANGED_DISTANCE_STAT_CODE, distanceBonus, false);

            // Counter-stats: when Nearsighted damage / Frail distance penalty is fully cancelled,
            // apply a +penalty counter so functional ranged stats match the displayed cap.
            if (hasNearsighted)
            {
                if (nearsightedRemaining == 0)
                    player.Entity.Stats.Set("rangedWeaponsDamage", "sitNearsightedRangedCancel", VANILLA_NEARSIGHTED_RANGED_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("rangedWeaponsDamage", "sitNearsightedRangedCancel");
            }

            // When Frail distance penalty is fully cancelled, also negate the HP penalty AND the distance penalty
            if (hasFrail)
            {
                if (frailDistanceRemaining == 0)
                {
                    player.Entity.Stats.Set("maxhealthExtraPoints", FRAIL_HP_CANCEL_STAT_CODE, VANILLA_FRAIL_HP_PENALTY, false);
                    player.Entity.Stats.Set("bowDrawingStrength", "sitFrailDistanceCancel", VANILLA_FRAIL_DISTANCE_PENALTY * 0.01f, false);
                }
                else
                {
                    player.Entity.Stats.Remove("maxhealthExtraPoints", FRAIL_HP_CANCEL_STAT_CODE);
                    player.Entity.Stats.Remove("bowDrawingStrength", "sitFrailDistanceCancel");
                }
            }

            // Check if any values have changed before updating WatchedAttributes
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_RANGED_LEVEL, -1);
            int oldDamageBonus = watchedAttrs.GetInt(WATCHED_RANGED_DAMAGE_BONUS, -1);

            bool valuesChanged = (oldLevel != level) || (oldDamageBonus != damagePct);

            // Only update WatchedAttributes if values changed
            if (valuesChanged)
            {
                // Sync level and bonuses to WatchedAttributes for client-side display
                watchedAttrs.SetInt(WATCHED_RANGED_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_RANGED_DAMAGE_BONUS, damagePct);
                watchedAttrs.SetInt(WATCHED_RANGED_ACCURACY_BONUS, accuracyPct);
                watchedAttrs.SetInt(WATCHED_RANGED_DISTANCE_BONUS, distancePct);
                watchedAttrs.SetBool("sitHasVanillaFocused", hasVanillaFocused);

                // Sync negative trait status
                watchedAttrs.SetBool("sitHasNearsighted", hasNearsighted);
                watchedAttrs.SetInt(WATCHED_NEARSIGHTED_REMAINING, nearsightedRemaining);
                watchedAttrs.SetBool("sitHasFrail", hasFrail);
                watchedAttrs.SetInt(WATCHED_FRAIL_DISTANCE_REMAINING, frailDistanceRemaining);
                watchedAttrs.SetFloat(WATCHED_FRAIL_HP_REMAINING, frailHpRemaining);

                // Add our trait to extraTraits only if player doesn't already have Focused
                UpdateExtraTraitStatic(player.Entity, RANGED_TRAIT_CODE, level > 0 && !hasVanillaFocused);

                watchedAttrs.MarkPathDirty(WATCHED_RANGED_LEVEL);
            }

            return (damagePct, accuracyPct, distancePct);
        }

        /// <summary>
        /// Apply ranged bonuses when Combat Overhaul is installed.
        /// Uses damage tier bonuses instead of percentage damage bonuses.
        /// CO Focused trait gives +1 ranged slashing tier (not % damage).
        /// </summary>
        private static (int damage, int accuracy, int distance) ApplyRangedBonusCO(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return (0, 0, 0);

            var cache = GetCachedTraits(player.PlayerUID);
            bool hasVanillaFocused = cache?.HasFocused ?? PlayerHasVanillaFocusedStatic(player.Entity);

            // In CO: Focused gives +1 ranged slashing tier (not %)
            // Hunter has Focused (+1 tier already from CO), others can earn up to +1 tier
            int vanillaTier = hasVanillaFocused ? 1 : 0;
            int maxEarnableTier = 1; // Cap at 1 tier total for all classes

            // 100 credits = 1 tier
            int earnedTier = level / 100;
            int totalTier = Math.Min(vanillaTier + earnedTier, maxEarnableTier);
            int netEarnedTier = Math.Max(0, totalTier - vanillaTier);

            // Apply tier stat (integer, stored as float for Stats.Set compatibility)
            player.Entity.Stats.Set(CO_RANGED_TIER_SLASHING, RANGED_DAMAGE_STAT_CODE, (float)netEarnedTier, false);

            // Sync for UI
            var watchedAttrs = player.Entity.WatchedAttributes;
            int oldLevel = watchedAttrs.GetInt(WATCHED_RANGED_LEVEL, -1);

            if (oldLevel != level)
            {
                watchedAttrs.SetInt(WATCHED_RANGED_LEVEL, level);
                watchedAttrs.SetInt(WATCHED_CO_RANGED_TIER_BONUS, netEarnedTier);
                watchedAttrs.SetBool("sitHasVanillaFocused", hasVanillaFocused);
                watchedAttrs.SetBool("sitCOEnabled", true);
                watchedAttrs.MarkPathDirty(WATCHED_RANGED_LEVEL);

                // Add our trait to extraTraits only if player doesn't already have Focused
                UpdateExtraTraitStatic(player.Entity, RANGED_TRAIT_CODE, level > 0 && !hasVanillaFocused);
            }

            // Return tier as the "damage" value for compatibility (accuracy and distance not used in CO tier system)
            return (netEarnedTier, 0, 0);
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
                if (pendingMiningProgressSave || !MiningProgress.IsEmpty)
                {
                    PersistMiningProgress();
                }
                if (pendingMeleeProgressSave || !MeleeProgress.IsEmpty)
                {
                    PersistMeleeProgress();
                }
                if (pendingRangedProgressSave || !RangedProgress.IsEmpty)
                {
                    PersistRangedProgress();
                }
                if (pendingWalkingProgressSave || !WalkingProgress.IsEmpty)
                {
                    PersistWalkingProgress();
                }
                if (pendingHungerProgressSave || !HungerProgress.IsEmpty)
                {
                    PersistHungerProgress();
                }
                if (pendingArmorProgressSave || !ArmorProgress.IsEmpty)
                {
                    PersistArmorProgress();
                }
                if (pendingClothierProgressSave || !ClothierProgress.IsEmpty)
                {
                    PersistClothierProgress();
                }
                if (pendingMenderProgressSave || !MenderProgress.IsEmpty)
                {
                    PersistMenderProgress();
                }
                if (pendingPilfererProgressSave || !PilfererProgress.IsEmpty)
                {
                    PersistPilfererProgress();
                }
                if (pendingResourcefulProgressSave || !ResourcefulProgress.IsEmpty)
                {
                    PersistResourcefulProgress();
                }
                if (pendingForagerProgressSave || !ForagerProgress.IsEmpty)
                {
                    PersistForagerProgress();
                }
                if (pendingFurtiveProgressSave || !FurtiveProgress.IsEmpty)
                {
                    PersistFurtiveProgress();
                }
                if (pendingPreciseProgressSave || !PreciseProgress.IsEmpty)
                {
                    PersistPreciseProgress();
                }
                if (pendingTechnicalProgressSave || !TechnicalProgress.IsEmpty)
                {
                    PersistTechnicalProgress();
                }
                if (pendingHardyHealthProgressSave || !HardyHealthProgress.IsEmpty)
                {
                    PersistHardyHealthProgress();
                }
                if (pendingBowyerProgressSave || !BowyerProgress.IsEmpty)
                {
                    PersistBowyerProgress();
                }
                if (pendingImproviserProgressSave || !ImproviserProgress.IsEmpty)
                {
                    PersistImproviserProgress();
                }
                if (pendingTinkererProgressSave || !TinkererProgress.IsEmpty)
                {
                    PersistTinkererProgress();
                }
                if (pendingMercilessProgressSave || !MercilessProgress.IsEmpty)
                {
                    PersistMercilessProgress();
                }
                if (pendingClaustrophobicRemovalProgressSave || !ClaustrophobicRemovalProgress.IsEmpty)
                {
                    PersistClaustrophobicRemovalProgress();
                }
                if (pendingHeavyFootedRemovalProgressSave || !HeavyFootedRemovalProgress.IsEmpty)
                {
                    PersistHeavyFootedRemovalProgress();
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
                ServerApi.Event.SaveGameLoaded -= LoadMiningProgress;
                ServerApi.Event.SaveGameLoaded -= LoadMeleeProgress;
                ServerApi.Event.SaveGameLoaded -= LoadRangedProgress;
                ServerApi.Event.SaveGameLoaded -= LoadWalkingProgress;
                ServerApi.Event.SaveGameLoaded -= LoadHungerProgress;
                ServerApi.Event.SaveGameLoaded -= LoadArmorProgress;
                ServerApi.Event.SaveGameLoaded -= LoadClothierProgress;
                ServerApi.Event.SaveGameLoaded -= LoadMenderProgress;
                ServerApi.Event.SaveGameLoaded -= LoadPilfererProgress;
                ServerApi.Event.SaveGameLoaded -= LoadResourcefulProgress;
                ServerApi.Event.SaveGameLoaded -= LoadForagerProgress;
                ServerApi.Event.SaveGameLoaded -= LoadFurtiveProgress;
                ServerApi.Event.SaveGameLoaded -= LoadPreciseProgress;
                ServerApi.Event.SaveGameLoaded -= LoadTechnicalProgress;
                ServerApi.Event.SaveGameLoaded -= LoadHardyHealthProgress;
                ServerApi.Event.SaveGameLoaded -= LoadBowyerProgress;
                ServerApi.Event.SaveGameLoaded -= LoadImproviserProgress;
                ServerApi.Event.SaveGameLoaded -= LoadTinkererProgress;
                ServerApi.Event.SaveGameLoaded -= LoadMercilessProgress;
                ServerApi.Event.SaveGameLoaded -= LoadClaustrophobicRemovalProgress;
                ServerApi.Event.SaveGameLoaded -= LoadHeavyFootedRemovalProgress;
                ServerApi.Event.SaveGameLoaded -= LoadCOProgress;
                ServerApi.Event.SaveGameLoaded -= LoadSleepBuffData;
            }

            // Mark as disposed BEFORE clearing dictionaries to prevent OnGameWorldSave
            // from persisting empty data if it fires during shutdown after Clear()
            isDisposed = true;

            // Unpatch server-side Harmony patches
            serverHarmony?.UnpatchAll("seraphleveling.server");

            MiningProgress.Clear();
            MeleeProgress.Clear();
            RangedProgress.Clear();
            WalkingProgress.Clear();
            HungerProgress.Clear();
            ArmorProgress.Clear();
            ClothierProgress.Clear();
            MenderProgress.Clear();
            PilfererProgress.Clear();
            ResourcefulProgress.Clear();
            ForagerProgress.Clear();
            FurtiveProgress.Clear();
            PreciseProgress.Clear();
            TechnicalProgress.Clear();
            HardyHealthProgress.Clear();
            BowyerProgress.Clear();
            ImproviserProgress.Clear();
            TinkererProgress.Clear();
            MercilessProgress.Clear();
            ClaustrophobicRemovalProgress.Clear();
            lastPlayerPositions.Clear();
            lastSneakingPositions.Clear();
            VanillaTraitsCache.Clear();
            LastDecayCheckDay.Clear();
            SleepBuffExpiration.Clear();
            SleepBuffMultiplier.Clear();
            LastSleepBuffApplyTick.Clear();
            pendingSleepBuffSave = false;
            pendingMiningProgressSave = false;
            pendingMeleeProgressSave = false;
            pendingRangedProgressSave = false;
            pendingWalkingProgressSave = false;
            pendingHungerProgressSave = false;
            pendingArmorProgressSave = false;
            pendingClothierProgressSave = false;
            pendingMenderProgressSave = false;
            pendingPilfererProgressSave = false;
            pendingResourcefulProgressSave = false;
            pendingForagerProgressSave = false;
            pendingFurtiveProgressSave = false;
            pendingPreciseProgressSave = false;
            pendingTechnicalProgressSave = false;
            pendingHardyHealthProgressSave = false;
            pendingBowyerProgressSave = false;
            pendingImproviserProgressSave = false;
            pendingTinkererProgressSave = false;
            pendingMercilessProgressSave = false;
            pendingClaustrophobicRemovalProgressSave = false;
            base.Dispose();
        }

        /// <summary>
        /// Called when the world is saved. Persist all progress and config to world save data.
        /// </summary>
        private void OnGameWorldSave()
        {
            // Guard against persisting empty data after Dispose() has cleared dictionaries
            if (isDisposed) return;

            if (pendingMiningProgressSave || !MiningProgress.IsEmpty)
            {
                PersistMiningProgress();
                pendingMiningProgressSave = false;
            }

            if (pendingMeleeProgressSave || !MeleeProgress.IsEmpty)
            {
                PersistMeleeProgress();
                pendingMeleeProgressSave = false;
            }

            if (pendingRangedProgressSave || !RangedProgress.IsEmpty)
            {
                PersistRangedProgress();
                pendingRangedProgressSave = false;
            }

            if (pendingWalkingProgressSave || !WalkingProgress.IsEmpty)
            {
                PersistWalkingProgress();
                pendingWalkingProgressSave = false;
            }

            if (pendingHungerProgressSave || !HungerProgress.IsEmpty)
            {
                PersistHungerProgress();
                pendingHungerProgressSave = false;
            }

            if (pendingArmorProgressSave || !ArmorProgress.IsEmpty)
            {
                PersistArmorProgress();
                pendingArmorProgressSave = false;
            }

            if (pendingClothierProgressSave || !ClothierProgress.IsEmpty)
            {
                PersistClothierProgress();
                pendingClothierProgressSave = false;
            }

            if (pendingMenderProgressSave || !MenderProgress.IsEmpty)
            {
                PersistMenderProgress();
                pendingMenderProgressSave = false;
            }

            if (pendingPilfererProgressSave || !PilfererProgress.IsEmpty)
            {
                PersistPilfererProgress();
                pendingPilfererProgressSave = false;
            }

            if (pendingResourcefulProgressSave || !ResourcefulProgress.IsEmpty)
            {
                PersistResourcefulProgress();
                pendingResourcefulProgressSave = false;
            }

            if (pendingForagerProgressSave || !ForagerProgress.IsEmpty)
            {
                PersistForagerProgress();
                pendingForagerProgressSave = false;
            }

            if (pendingFurtiveProgressSave || !FurtiveProgress.IsEmpty)
            {
                PersistFurtiveProgress();
                pendingFurtiveProgressSave = false;
            }

            if (pendingPreciseProgressSave || !PreciseProgress.IsEmpty)
            {
                PersistPreciseProgress();
                pendingPreciseProgressSave = false;
            }

            if (pendingTechnicalProgressSave || !TechnicalProgress.IsEmpty)
            {
                PersistTechnicalProgress();
                pendingTechnicalProgressSave = false;
            }

            if (pendingHardyHealthProgressSave || !HardyHealthProgress.IsEmpty)
            {
                PersistHardyHealthProgress();
                pendingHardyHealthProgressSave = false;
            }

            if (pendingBowyerProgressSave || !BowyerProgress.IsEmpty)
            {
                PersistBowyerProgress();
                pendingBowyerProgressSave = false;
            }

            if (pendingImproviserProgressSave || !ImproviserProgress.IsEmpty)
            {
                PersistImproviserProgress();
                pendingImproviserProgressSave = false;
            }

            if (pendingTinkererProgressSave || !TinkererProgress.IsEmpty)
            {
                PersistTinkererProgress();
                pendingTinkererProgressSave = false;
            }

            if (pendingMercilessProgressSave || !MercilessProgress.IsEmpty)
            {
                PersistMercilessProgress();
                pendingMercilessProgressSave = false;
            }

            if (pendingClaustrophobicRemovalProgressSave || !ClaustrophobicRemovalProgress.IsEmpty)
            {
                PersistClaustrophobicRemovalProgress();
                pendingClaustrophobicRemovalProgressSave = false;
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

        /// <summary>
        /// Persist mining progress to world save data.
        /// Version 3 format stores per-pickaxe progress dictionary.
        /// </summary>
        public static void PersistMiningProgress()
        {
            PersistProgress<MiningProgressData>();
        }

        /// <summary>
        /// Load mining progress from world save data.
        /// Supports versions 1 (legacy blocks), 2 (single pickaxe), and 3 (per-pickaxe).
        /// </summary>
        private void LoadMiningProgress()
        {
            LoadProgress<MiningProgressData>();
        }

        /// <summary>
        /// Persist melee progress to world save data.
        /// Version 1 format stores per-weapon progress dictionary.
        /// </summary>
        public static void PersistMeleeProgress()
        {
            PersistProgress<MeleeProgressData>();
        }

        /// <summary>
        /// Load melee progress from world save data.
        /// </summary>
        private void LoadMeleeProgress()
        {
            LoadProgress<MeleeProgressData>();
        }

        /// <summary>
        /// Persist ranged progress to world save data.
        /// Version 2 format stores per-weapon progress dictionary + LastActivityDay.
        /// </summary>
        public static void PersistRangedProgress()
        {
            PersistProgress<RangedProgressData>();
        }

        /// <summary>
        /// Load ranged progress from world save data.
        /// </summary>
        private void LoadRangedProgress()
        {
            LoadProgress<RangedProgressData>();
        }

        public static void PersistProgress<T>() where T:ProgressData<T>,IProgressDataContract<T>
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
        /// <summary>
        /// Persist walking progress to world save data.
        /// Version 1 format: simple progress tracking (no per-tool).
        /// </summary>
        public static void PersistWalkingProgress()
        {
            PersistProgress<WalkingProgressData>();
        }

        private void LoadProgress<T>() where T:ProgressData<T>,IProgressDataContract<T>
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
                    ServerApi.Logger.Debug("[SeraphLeveling] No {description} progress data found in world save");
                    return;
                }

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        if (!ProgressData<T>.ReadHeader(reader)) {
                            ServerApi.Logger.Warning("[SeraphLeveling] Invalid {description} progress data format");
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
                                ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{playerCount} in {description} data: {innerEx.Message}");
                                break;
                            }
                        }
                        if (version != T.GetVersion()) {
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
        /// Load walking progress from world save data.
        /// </summary>
        private void LoadWalkingProgress()
        {
            LoadProgress<WalkingProgressData>();
        }

        /// <summary>
        /// Persist hunger progress to world save data.
        /// Version 1 format: simple progress tracking (no per-tool).
        /// </summary>
        public static void PersistHungerProgress()
        {
            PersistProgress<HungerProgressData>();
        }

        /// <summary>
        /// Load hunger progress from world save data.
        /// </summary>
        private void LoadHungerProgress()
        {
            LoadProgress<HungerProgressData>();
        }

        /// <summary>
        /// Persist armor progress to world save data.
        /// Version 1 format stores durability credits, walk speed credits, and per-armor progress.
        /// </summary>
        public static void PersistArmorProgress()
        {
            PersistProgress<ArmorProgressData>();
        }

        /// <summary>
        /// Load armor progress from world save data.
        /// </summary>
        private void LoadArmorProgress()
        {
            LoadProgress<ArmorProgressData>();
        }

        /// <summary>
        /// Load configuration from ModConfig/SeraphLeveling.json.
        /// If the file doesn't exist, creates one with default values.
        /// These values are used as defaults for new worlds.
        /// </summary>
        private void LoadConfigFile(ICoreServerAPI api)
        {
            try
            {
                SeraphLevelingConfig config = api.LoadModConfig<SeraphLevelingConfig>(CONFIG_FILE_NAME);
                if (config == null)
                {
                    // A brand new install has no old world settings to fold in, so
                    // stamp it as already migrated.
                    initializeClothierBlacklistedItems(api);
                    config = new SeraphLevelingConfig { ConfigVersion = CURRENT_CONFIG_VERSION };
                    config.ClothierBlacklistedItems = ClothierBlacklistedItems;
                    api.StoreModConfig(config, CONFIG_FILE_NAME);
                    api.Logger.Notification("[SeraphLeveling] Created default config file: ModConfig/" + CONFIG_FILE_NAME);
                }

                LoadedConfigVersion = config.ConfigVersion;

                // Apply config values to static variables
                BaseBlocksPerIncrement = config.MiningBaseBlocksPerIncrement;
                IncrementStep = config.MiningIncrementStep;
                MaxMiningSpeedPercent = config.MiningMaxPercent;
                OreMultiplier = config.MiningOreMultiplier;

                BaseDamagePerIncrement = config.MeleeBaseDamagePerIncrement;
                MeleeIncrementStep = config.MeleeIncrementStep;
                MaxMeleeDamagePercent = config.MeleeMaxPercent;

                BaseRangedDamagePerIncrement = config.RangedBaseDamagePerIncrement;
                RangedIncrementStep = config.RangedIncrementStep;
                MaxRangedDamagePercent = config.RangedMaxDamagePercent;
                MaxRangedAccuracyPercent = config.RangedMaxAccuracyPercent;
                MaxRangedDistancePercent = config.RangedMaxDistancePercent;

                BaseBlocksWalkedPerIncrement = config.WalkingBaseBlocksPerIncrement;
                WalkingIncrementStep = config.WalkingIncrementStep;
                MaxWalkingSpeedPercent = config.WalkingMaxPercent;

                BaseSecondsPerIncrement = config.HungerBaseSecondsPerIncrement;
                HungerIncrementStep = config.HungerIncrementStep;
                MaxHungerReductionPercent = config.HungerMaxReductionPercent;

                BaseSecondsInArmorPerIncrement = config.ArmorBaseSecondsPerIncrement;
                ArmorTimeIncrementStep = config.ArmorTimeIncrementStep;
                BaseDamageBlockedPerIncrement = config.ArmorBaseDamageBlockedPerIncrement;
                ArmorDamageIncrementStep = config.ArmorDamageIncrementStep;
                BaseRepairsPerIncrement = config.ArmorBaseRepairsPerIncrement;
                ArmorRepairIncrementStep = config.ArmorRepairIncrementStep;
                MaxArmorDurabilityPercent = config.ArmorMaxDurabilityPercent;
                MaxArmorWalkSpeedPercent = config.ArmorMaxWalkSpeedPercent;

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
                MaxArmorHungerReductionPercent = config.ArmorMaxHungerReductionPercent;
                EnableArmorHealingBonus = config.EnableArmorHealingBonus;
                MaxArmorHealingPercent = config.ArmorMaxHealingPercent;

                ClothierRequiredUniqueClothes = config.ClothierRequiredUniqueClothes;
                if (config.ClothierBlacklistedItems != null)
                {
                    ClothierBlacklistedItems = config.ClothierBlacklistedItems;
                }

                BaseMenderRepairsPerIncrement = config.MenderBaseRepairsPerIncrement;
                MenderIncrementStep = config.MenderIncrementStep;
                MaxMenderPercent = config.MenderMaxPercent;

                BasePilfererPointsPerIncrement = config.PilfererBasePointsPerIncrement;
                PilfererIncrementStep = config.PilfererIncrementStep;
                MaxPilfererPercent = config.PilfererMaxPercent;

                BaseResourcefulAnimalsPerIncrement = config.ResourcefulBaseAnimalsPerIncrement;
                ResourcefulIncrementStep = config.ResourcefulIncrementStep;
                MaxResourcefulLootPercent = config.ResourcefulMaxLootPercent;
                MaxResourcefulSpeedPercent = config.ResourcefulMaxSpeedPercent;

                BaseForagerCropsPerIncrement = config.ForagerBaseCropsPerIncrement;
                ForagerIncrementStep = config.ForagerIncrementStep;
                MaxForagerLootPercent = config.ForagerMaxLootPercent;
                MaxForagerWildCropPercent = config.ForagerMaxWildCropPercent;

                BaseFurtiveSneakBlocksPerIncrement = config.FurtiveBaseSneakBlocksPerIncrement;
                FurtiveIncrementStep = config.FurtiveIncrementStep;
                MaxFurtivePercent = config.FurtiveMaxPercent;

                BasePreciseDamagePerIncrement = config.PreciseBaseDamagePerIncrement;
                PreciseIncrementStep = config.PreciseIncrementStep;
                MaxPrecisePercent = config.PreciseMaxPercent;

                TechnicalRequiredTranslocatorRepairs = config.TechnicalRequiredTranslocatorRepairs;

                HardyHealthMiningThreshold = config.HardyHealthMiningThreshold;
                HardyHealthArmorDurabilityThreshold = config.HardyHealthArmorDurabilityThreshold;
                HardyHealthBonus = config.HardyHealthBonus;

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

                config.MiningBaseBlocksPerIncrement = BaseBlocksPerIncrement;
                config.MiningIncrementStep = IncrementStep;
                config.MiningMaxPercent = MaxMiningSpeedPercent;
                config.MiningOreMultiplier = OreMultiplier;

                config.MeleeBaseDamagePerIncrement = BaseDamagePerIncrement;
                config.MeleeIncrementStep = MeleeIncrementStep;
                config.MeleeMaxPercent = MaxMeleeDamagePercent;

                config.RangedBaseDamagePerIncrement = BaseRangedDamagePerIncrement;
                config.RangedIncrementStep = RangedIncrementStep;
                config.RangedMaxDamagePercent = MaxRangedDamagePercent;
                config.RangedMaxAccuracyPercent = MaxRangedAccuracyPercent;
                config.RangedMaxDistancePercent = MaxRangedDistancePercent;

                config.WalkingBaseBlocksPerIncrement = BaseBlocksWalkedPerIncrement;
                config.WalkingIncrementStep = WalkingIncrementStep;
                config.WalkingMaxPercent = MaxWalkingSpeedPercent;

                config.HungerBaseSecondsPerIncrement = BaseSecondsPerIncrement;
                config.HungerIncrementStep = HungerIncrementStep;
                config.HungerMaxReductionPercent = MaxHungerReductionPercent;

                config.ArmorBaseSecondsPerIncrement = BaseSecondsInArmorPerIncrement;
                config.ArmorTimeIncrementStep = ArmorTimeIncrementStep;
                config.ArmorBaseDamageBlockedPerIncrement = BaseDamageBlockedPerIncrement;
                config.ArmorDamageIncrementStep = ArmorDamageIncrementStep;
                config.ArmorBaseRepairsPerIncrement = BaseRepairsPerIncrement;
                config.ArmorRepairIncrementStep = ArmorRepairIncrementStep;
                config.ArmorMaxDurabilityPercent = MaxArmorDurabilityPercent;
                config.ArmorMaxWalkSpeedPercent = MaxArmorWalkSpeedPercent;

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
                config.ArmorMaxHungerReductionPercent = MaxArmorHungerReductionPercent;
                config.EnableArmorHealingBonus = EnableArmorHealingBonus;
                config.ArmorMaxHealingPercent = MaxArmorHealingPercent;

                config.ClothierRequiredUniqueClothes = ClothierRequiredUniqueClothes;
                config.ClothierBlacklistedItems = ClothierBlacklistedItems;

                config.MenderBaseRepairsPerIncrement = BaseMenderRepairsPerIncrement;
                config.MenderIncrementStep = MenderIncrementStep;
                config.MenderMaxPercent = MaxMenderPercent;

                config.PilfererBasePointsPerIncrement = BasePilfererPointsPerIncrement;
                config.PilfererIncrementStep = PilfererIncrementStep;
                config.PilfererMaxPercent = MaxPilfererPercent;

                config.ResourcefulBaseAnimalsPerIncrement = BaseResourcefulAnimalsPerIncrement;
                config.ResourcefulIncrementStep = ResourcefulIncrementStep;
                config.ResourcefulMaxLootPercent = MaxResourcefulLootPercent;
                config.ResourcefulMaxSpeedPercent = MaxResourcefulSpeedPercent;

                config.ForagerBaseCropsPerIncrement = BaseForagerCropsPerIncrement;
                config.ForagerIncrementStep = ForagerIncrementStep;
                config.ForagerMaxLootPercent = MaxForagerLootPercent;
                config.ForagerMaxWildCropPercent = MaxForagerWildCropPercent;

                config.FurtiveBaseSneakBlocksPerIncrement = BaseFurtiveSneakBlocksPerIncrement;
                config.FurtiveIncrementStep = FurtiveIncrementStep;
                config.FurtiveMaxPercent = MaxFurtivePercent;

                config.PreciseBaseDamagePerIncrement = BasePreciseDamagePerIncrement;
                config.PreciseIncrementStep = PreciseIncrementStep;
                config.PreciseMaxPercent = MaxPrecisePercent;

                config.TechnicalRequiredTranslocatorRepairs = TechnicalRequiredTranslocatorRepairs;

                config.HardyHealthMiningThreshold = HardyHealthMiningThreshold;
                config.HardyHealthArmorDurabilityThreshold = HardyHealthArmorDurabilityThreshold;
                config.HardyHealthBonus = HardyHealthBonus;

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
        private static (double grace, int basePoints, int maxPoints) GetDecayParams(string skillKey)
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
        private static int CalculateDecayPoints(double lastActivityDay, double currentDay,
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

            // Mining
            if (!DecayExemptSkills.Contains("mining") && !DisabledSkills.Contains("mining"))
            {
                if (MiningProgress.TryGetValue(playerUid, out var mProg) && (mProg.TotalCredits > 0 || mProg.PickaxeProgress.Count > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("mining");
                    int decayCredits = CalculateDecayPoints(mProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = mProg.TotalCredits;
                        var toolEntries = mProg.PickaxeProgress.Select(kvp =>
                            (kvp.Key, (double)kvp.Value.BlocksInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                        if (toolEntries.Count > 0)
                        {
                            double rawPenalty = (double)decayCredits;

                            var (newCr, lost) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                                BaseBlocksPerIncrement, IncrementStep, oldCredits,
                                (k, a, s) => { if (mProg.PickaxeProgress.TryGetValue(k, out var p)) {
                                    p.BlocksInIncrement = (int)Math.Floor(a); p.CurrentIncrementSize = s; } },
                                k => mProg.PickaxeProgress.Remove(k), verboseSb, "Mining");
                            mProg.TotalCredits = newCr;
                            if (lost > 0) totalDecayApplied += lost;
                            sb.AppendLine($"  Mining: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts)");
                            foreach (var entry in toolEntries)
                            {
                                int oldToolCr = IncrementStep > 0 ? (entry.Item3 - BaseBlocksPerIncrement) / IncrementStep : 0;
                                if (mProg.PickaxeProgress.TryGetValue(entry.Item1, out var after))
                                {
                                    int newToolCr = IncrementStep > 0 ? (after.CurrentIncrementSize - BaseBlocksPerIncrement) / IncrementStep : 0;
                                    int toolLost = oldToolCr - newToolCr;
                                    sb.AppendLine($"    {entry.Item1}: {(int)entry.Item2}/{entry.Item3} \u2192 {after.BlocksInIncrement}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                                }
                                else
                                    sb.AppendLine($"    {entry.Item1}: {(int)entry.Item2}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                            }
                            pendingMiningProgressSave = true;
                        }
                        else
                        {
                            int lost = Math.Min(decayCredits, oldCredits);
                            mProg.TotalCredits -= lost;
                            if (lost > 0) { totalDecayApplied += lost; sb.AppendLine($"  Mining: {oldCredits} \u2192 {mProg.TotalCredits} (-{lost} credits)"); }
                            pendingMiningProgressSave = true;
                        }
                    }
                }
            }

            // Melee
            if (!DecayExemptSkills.Contains("melee") && !DisabledSkills.Contains("melee"))
            {
                if (MeleeProgress.TryGetValue(playerUid, out var mProg) && (mProg.TotalCredits > 0 || mProg.WeaponProgress.Count > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("melee");
                    int decayCredits = CalculateDecayPoints(mProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = mProg.TotalCredits;
                        var toolEntries = mProg.WeaponProgress.Select(kvp =>
                            (kvp.Key, (double)kvp.Value.DamageInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                        if (toolEntries.Count > 0)
                        {
                            double rawPenalty = (double)decayCredits;

                            var (newCr, lost) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                                BaseDamagePerIncrement, MeleeIncrementStep, oldCredits,
                                (k, a, s) => { if (mProg.WeaponProgress.TryGetValue(k, out var p)) {
                                    p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s; } },
                                k => mProg.WeaponProgress.Remove(k), verboseSb, "Melee");
                            mProg.TotalCredits = newCr;
                            if (lost > 0) totalDecayApplied += lost;
                            sb.AppendLine($"  Melee: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts)");
                            foreach (var entry in toolEntries)
                            {
                                int oldToolCr = MeleeIncrementStep > 0 ? (entry.Item3 - BaseDamagePerIncrement) / MeleeIncrementStep : 0;
                                if (mProg.WeaponProgress.TryGetValue(entry.Item1, out var after))
                                {
                                    int newToolCr = MeleeIncrementStep > 0 ? (after.CurrentIncrementSize - BaseDamagePerIncrement) / MeleeIncrementStep : 0;
                                    int toolLost = oldToolCr - newToolCr;
                                    sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 {after.DamageInIncrement:F0}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                                }
                                else
                                    sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                            }
                            pendingMeleeProgressSave = true;
                        }
                        else
                        {
                            int lost = Math.Min(decayCredits, oldCredits);
                            mProg.TotalCredits -= lost;
                            if (lost > 0) { totalDecayApplied += lost; sb.AppendLine($"  Melee: {oldCredits} \u2192 {mProg.TotalCredits} (-{lost} credits)"); }
                            pendingMeleeProgressSave = true;
                        }
                    }
                }
            }

            // Ranged
            if (!DecayExemptSkills.Contains("ranged") && !DisabledSkills.Contains("ranged"))
            {
                if (RangedProgress.TryGetValue(playerUid, out var rProg) && (rProg.TotalCredits > 0 || rProg.WeaponProgress.Count > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("ranged");
                    int decayCredits = CalculateDecayPoints(rProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = rProg.TotalCredits;
                        var toolEntries = rProg.WeaponProgress.Select(kvp =>
                            (kvp.Key, (double)kvp.Value.DamageInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                        if (toolEntries.Count > 0)
                        {
                            double rawPenalty = (double)decayCredits;

                            var (newCr, lost) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                                BaseRangedDamagePerIncrement, RangedIncrementStep, oldCredits,
                                (k, a, s) => { if (rProg.WeaponProgress.TryGetValue(k, out var p)) {
                                    p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s; } },
                                k => rProg.WeaponProgress.Remove(k), verboseSb, "Ranged");
                            rProg.TotalCredits = newCr;
                            if (lost > 0) totalDecayApplied += lost;
                            sb.AppendLine($"  Ranged: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts)");
                            foreach (var entry in toolEntries)
                            {
                                int oldToolCr = RangedIncrementStep > 0 ? (entry.Item3 - BaseRangedDamagePerIncrement) / RangedIncrementStep : 0;
                                if (rProg.WeaponProgress.TryGetValue(entry.Item1, out var after))
                                {
                                    int newToolCr = RangedIncrementStep > 0 ? (after.CurrentIncrementSize - BaseRangedDamagePerIncrement) / RangedIncrementStep : 0;
                                    int toolLost = oldToolCr - newToolCr;
                                    sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 {after.DamageInIncrement:F0}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                                }
                                else
                                    sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                            }
                            pendingRangedProgressSave = true;
                        }
                        else
                        {
                            int lost = Math.Min(decayCredits, oldCredits);
                            rProg.TotalCredits -= lost;
                            if (lost > 0) { totalDecayApplied += lost; sb.AppendLine($"  Ranged: {oldCredits} \u2192 {rProg.TotalCredits} (-{lost} credits)"); }
                            pendingRangedProgressSave = true;
                        }
                    }
                }
            }

            // Precise
            if (!DecayExemptSkills.Contains("precise") && !DisabledSkills.Contains("precise"))
            {
                if (PreciseProgress.TryGetValue(playerUid, out var pProg) && (pProg.TotalCredits > 0 || pProg.WeaponProgress.Count > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("precise");
                    int decayCredits = CalculateDecayPoints(pProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = pProg.TotalCredits;
                        var toolEntries = pProg.WeaponProgress.Select(kvp =>
                            (kvp.Key, (double)kvp.Value.DamageInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                        if (toolEntries.Count > 0)
                        {
                            double rawPenalty = (double)decayCredits;

                            var (newCr, lost) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                                BasePreciseDamagePerIncrement, PreciseIncrementStep, oldCredits,
                                (k, a, s) => { if (pProg.WeaponProgress.TryGetValue(k, out var p)) {
                                    p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s; } },
                                k => pProg.WeaponProgress.Remove(k), verboseSb, "Precise");
                            pProg.TotalCredits = newCr;
                            if (lost > 0) totalDecayApplied += lost;
                            sb.AppendLine($"  Precise: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts)");
                            foreach (var entry in toolEntries)
                            {
                                int oldToolCr = PreciseIncrementStep > 0 ? (entry.Item3 - BasePreciseDamagePerIncrement) / PreciseIncrementStep : 0;
                                if (pProg.WeaponProgress.TryGetValue(entry.Item1, out var after))
                                {
                                    int newToolCr = PreciseIncrementStep > 0 ? (after.CurrentIncrementSize - BasePreciseDamagePerIncrement) / PreciseIncrementStep : 0;
                                    int toolLost = oldToolCr - newToolCr;
                                    sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 {after.DamageInIncrement:F0}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                                }
                                else
                                    sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                            }
                            pendingPreciseProgressSave = true;
                        }
                        else
                        {
                            int lost = Math.Min(decayCredits, oldCredits);
                            pProg.TotalCredits -= lost;
                            if (lost > 0) { totalDecayApplied += lost; sb.AppendLine($"  Precise: {oldCredits} \u2192 {pProg.TotalCredits} (-{lost} credits)"); }
                            pendingPreciseProgressSave = true;
                        }
                    }
                }
            }

            // --- Single-accumulator skills ---

            // Walking
            if (!DecayExemptSkills.Contains("walking") && !DisabledSkills.Contains("walking"))
            {
                if (WalkingProgress.TryGetValue(playerUid, out var wProg) && (wProg.TotalCredits > 0 || wProg.PartialCredit > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("walking");
                    int decayCredits = CalculateDecayPoints(wProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = wProg.TotalCredits;
                        float oldAcc = wProg.PartialCredit; int oldInc = wProg.CurrentIncrementSize;
                        double rawPenalty = (double)decayCredits;
                        var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                            oldAcc, oldInc, oldCredits,
                            rawPenalty, BaseBlocksWalkedPerIncrement, WalkingIncrementStep, verboseSb, "Walking");
                        wProg.TotalCredits = newCr; wProg.PartialCredit = (float)newAcc; wProg.CurrentIncrementSize = newInc;
                        if (lost > 0) totalDecayApplied += lost;
                        sb.AppendLine($"  Walking: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc:F0}/{oldInc} \u2192 {(int)newAcc}/{newInc}");
                        pendingWalkingProgressSave = true;
                    }
                }
            }

            // Hunger
            if (!DecayExemptSkills.Contains("hunger") && !DisabledSkills.Contains("hunger"))
            {
                if (HungerProgress.TryGetValue(playerUid, out var hProg) && (hProg.TotalCredits > 0 || hProg.SecondsInIncrement > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("hunger");
                    int decayCredits = CalculateDecayPoints(hProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = hProg.TotalCredits;
                        float oldAcc = hProg.SecondsInIncrement; int oldInc = hProg.CurrentIncrementSize;
                        double rawPenalty = (double)decayCredits;
                        var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                            oldAcc, oldInc, oldCredits,
                            rawPenalty, BaseSecondsPerIncrement, HungerIncrementStep, verboseSb, "Hunger");
                        hProg.TotalCredits = newCr; hProg.SecondsInIncrement = (float)newAcc; hProg.CurrentIncrementSize = newInc;
                        if (lost > 0) totalDecayApplied += lost;
                        sb.AppendLine($"  Hunger: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc:F0}/{oldInc} \u2192 {(int)newAcc}/{newInc}");
                        pendingHungerProgressSave = true;
                    }
                }
            }

            // Armor is exempt from decay (leveled by wearing new pieces, not renewable)

            // Mender
            if (!DecayExemptSkills.Contains("mender") && !DisabledSkills.Contains("mender"))
            {
                if (MenderProgress.TryGetValue(playerUid, out var meProg) && (meProg.TotalCredits > 0 || meProg.RepairsInIncrement > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("mender");
                    int decayCredits = CalculateDecayPoints(meProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = meProg.TotalCredits;
                        int oldAcc = meProg.RepairsInIncrement; int oldInc = meProg.CurrentIncrementSize;
                        double rawPenalty = (double)decayCredits;
                        var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                            oldAcc, oldInc, oldCredits,
                            rawPenalty, BaseMenderRepairsPerIncrement, MenderIncrementStep, verboseSb, "Mender");
                        meProg.TotalCredits = newCr; meProg.RepairsInIncrement = (int)Math.Floor(newAcc); meProg.CurrentIncrementSize = newInc;
                        if (lost > 0) totalDecayApplied += lost;
                        sb.AppendLine($"  Mender: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc}/{oldInc} \u2192 {(int)Math.Floor(newAcc)}/{newInc}");
                        pendingMenderProgressSave = true;
                    }
                }
            }

            // Pilferer
            if (!DecayExemptSkills.Contains("pilferer") && !DisabledSkills.Contains("pilferer"))
            {
                if (PilfererProgress.TryGetValue(playerUid, out var piProg) && (piProg.TotalCredits > 0 || piProg.PointsInIncrement > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("pilferer");
                    int decayCredits = CalculateDecayPoints(piProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = piProg.TotalCredits;
                        int oldAcc = piProg.PointsInIncrement; int oldInc = piProg.CurrentIncrementSize;
                        double rawPenalty = (double)decayCredits;
                        var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                            oldAcc, oldInc, oldCredits,
                            rawPenalty, BasePilfererPointsPerIncrement, PilfererIncrementStep, verboseSb, "Pilferer");
                        piProg.TotalCredits = newCr; piProg.PointsInIncrement = (int)Math.Floor(newAcc); piProg.CurrentIncrementSize = newInc;
                        if (lost > 0) totalDecayApplied += lost;
                        sb.AppendLine($"  Pilferer: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc}/{oldInc} \u2192 {(int)Math.Floor(newAcc)}/{newInc}");
                        pendingPilfererProgressSave = true;
                    }
                }
            }

            // Resourceful
            if (!DecayExemptSkills.Contains("resourceful") && !DisabledSkills.Contains("resourceful"))
            {
                if (ResourcefulProgress.TryGetValue(playerUid, out var reProg) && (reProg.TotalCredits > 0 || reProg.AnimalsInIncrement > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("resourceful");
                    int decayCredits = CalculateDecayPoints(reProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = reProg.TotalCredits;
                        int oldAcc = reProg.AnimalsInIncrement; int oldInc = reProg.CurrentIncrementSize;
                        double rawPenalty = (double)decayCredits;
                        var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                            oldAcc, oldInc, oldCredits,
                            rawPenalty, BaseResourcefulAnimalsPerIncrement, ResourcefulIncrementStep, verboseSb, "Resourceful");
                        reProg.TotalCredits = newCr; reProg.AnimalsInIncrement = (int)Math.Floor(newAcc); reProg.CurrentIncrementSize = newInc;
                        if (lost > 0) totalDecayApplied += lost;
                        sb.AppendLine($"  Resourceful: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc}/{oldInc} \u2192 {(int)Math.Floor(newAcc)}/{newInc}");
                        pendingResourcefulProgressSave = true;
                    }
                }
            }

            // Forager
            if (!DecayExemptSkills.Contains("forager") && !DisabledSkills.Contains("forager"))
            {
                if (ForagerProgress.TryGetValue(playerUid, out var fProg) && (fProg.TotalCredits > 0 || fProg.CropsInIncrement > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("forager");
                    int decayCredits = CalculateDecayPoints(fProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = fProg.TotalCredits;
                        int oldAcc = fProg.CropsInIncrement; int oldInc = fProg.CurrentIncrementSize;
                        double rawPenalty = (double)decayCredits;
                        var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                            oldAcc, oldInc, oldCredits,
                            rawPenalty, BaseForagerCropsPerIncrement, ForagerIncrementStep, verboseSb, "Forager");
                        fProg.TotalCredits = newCr; fProg.CropsInIncrement = (int)Math.Floor(newAcc); fProg.CurrentIncrementSize = newInc;
                        if (lost > 0) totalDecayApplied += lost;
                        sb.AppendLine($"  Forager: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc}/{oldInc} \u2192 {(int)Math.Floor(newAcc)}/{newInc}");
                        pendingForagerProgressSave = true;
                    }
                }
            }

            // Furtive
            if (!DecayExemptSkills.Contains("furtive") && !DisabledSkills.Contains("furtive"))
            {
                if (FurtiveProgress.TryGetValue(playerUid, out var fuProg) && (fuProg.TotalCredits > 0 || fuProg.BlocksInIncrement > 0))
                {
                    var (grace, basePoints, maxPoints) = GetDecayParams("furtive");
                    int decayCredits = CalculateDecayPoints(fuProg.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        int oldCredits = fuProg.TotalCredits;
                        float oldAcc = fuProg.BlocksInIncrement; int oldInc = fuProg.CurrentIncrementSize;
                        double rawPenalty = (double)decayCredits;
                        var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                            oldAcc, oldInc, oldCredits,
                            rawPenalty, BaseFurtiveSneakBlocksPerIncrement, FurtiveIncrementStep, verboseSb, "Furtive");
                        fuProg.TotalCredits = newCr; fuProg.BlocksInIncrement = (float)newAcc; fuProg.CurrentIncrementSize = newInc;
                        if (lost > 0) totalDecayApplied += lost;
                        sb.AppendLine($"  Furtive: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc:F0}/{oldInc} \u2192 {(int)newAcc}/{newInc}");
                        pendingFurtiveProgressSave = true;
                    }
                }
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
                                        (k, a, s) => { if (profKvp.Value.WeaponProgress.TryGetValue(k, out var p)) {
                                            p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s; } },
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

            if (MiningProgress.TryGetValue(playerUid, out var miningProg))
                ApplyMiningBonus(player, miningProg.TotalCredits);
            if (MeleeProgress.TryGetValue(playerUid, out var meleeProg))
                ApplyMeleeBonusStatic(player, meleeProg.TotalCredits);
            if (RangedProgress.TryGetValue(playerUid, out var rangedProg))
                ApplyRangedBonusStatic(player, rangedProg.TotalCredits);
            if (WalkingProgress.TryGetValue(playerUid, out var walkingProg))
                ApplyWalkingBonusStatic(player, walkingProg.TotalCredits);
            if (HungerProgress.TryGetValue(playerUid, out var hungerProg))
                ApplyHungerBonusStatic(player, hungerProg.TotalCredits);
            if (ArmorProgress.TryGetValue(playerUid, out var armorProg))
                ApplyArmorBonusesStatic(player, armorProg.TotalDurabilityCredits, armorProg.TotalWalkSpeedCredits);
            if (MenderProgress.TryGetValue(playerUid, out var menderProg))
                ApplyMenderBonusStatic(player, menderProg.TotalCredits);
            if (PilfererProgress.TryGetValue(playerUid, out var pilfererProg))
                ApplyPilfererBonusStatic(player, pilfererProg.TotalCredits);
            if (ResourcefulProgress.TryGetValue(playerUid, out var resourcefulProg))
                ApplyResourcefulBonusStatic(player, resourcefulProg.TotalCredits);
            if (ForagerProgress.TryGetValue(playerUid, out var foragerProg))
                ApplyForagerBonusStatic(player, foragerProg.TotalCredits);
            if (FurtiveProgress.TryGetValue(playerUid, out var furtiveProg))
                ApplyFurtiveBonusStatic(player, furtiveProg.TotalCredits);
            if (PreciseProgress.TryGetValue(playerUid, out var preciseProg))
                ApplyPreciseBonusStatic(player, preciseProg.TotalCredits);
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
                case "mining":
                    if (MiningProgress.TryGetValue(playerUid, out var miningProg))
                        miningProg.LastActivityDay = currentDay;
                    break;
                case "melee":
                    if (MeleeProgress.TryGetValue(playerUid, out var meleeProg))
                        meleeProg.LastActivityDay = currentDay;
                    break;
                case "ranged":
                    if (RangedProgress.TryGetValue(playerUid, out var rangedProg))
                        rangedProg.LastActivityDay = currentDay;
                    break;
                case "walking":
                    if (WalkingProgress.TryGetValue(playerUid, out var walkingProg))
                        walkingProg.LastActivityDay = currentDay;
                    break;
                case "hunger":
                    if (HungerProgress.TryGetValue(playerUid, out var hungerProg))
                        hungerProg.LastActivityDay = currentDay;
                    break;
                case "armor":
                    if (ArmorProgress.TryGetValue(playerUid, out var armorProg))
                        armorProg.LastActivityDay = currentDay;
                    break;
                case "mender":
                    if (MenderProgress.TryGetValue(playerUid, out var menderProg))
                        menderProg.LastActivityDay = currentDay;
                    break;
                case "pilferer":
                    if (PilfererProgress.TryGetValue(playerUid, out var pilfererProg))
                        pilfererProg.LastActivityDay = currentDay;
                    break;
                case "resourceful":
                    if (ResourcefulProgress.TryGetValue(playerUid, out var resourcefulProg))
                        resourcefulProg.LastActivityDay = currentDay;
                    break;
                case "forager":
                    if (ForagerProgress.TryGetValue(playerUid, out var foragerProg))
                        foragerProg.LastActivityDay = currentDay;
                    break;
                case "furtive":
                    if (FurtiveProgress.TryGetValue(playerUid, out var furtiveProg))
                        furtiveProg.LastActivityDay = currentDay;
                    break;
                case "precise":
                    if (PreciseProgress.TryGetValue(playerUid, out var preciseProg))
                        preciseProg.LastActivityDay = currentDay;
                    break;
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
        private static double DrainAccumulatorsLeveling(List<(string key, double value)> accumulators, double penalty)
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
        private static double ToolToAbsolutePosition(double accumulator, int currentIncrementSize,
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
        private static (int credits, double accumulator, int incrementSize) AbsolutePositionToToolState(
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
        private static (int newTotalCredits, int creditsLost) ApplyAbsolutePositionDecay(
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
        private static (int newCredits, double newAccumulator, int newIncrementSize, int creditsLost)
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
        /// Binary-search for the minimum rawPenalty that, when water-leveled across per-tool
        /// absolute positions, reduces the total per-tool credits by at least targetLoss.
        /// Returns 0 if no drain is needed.
        /// </summary>
        private static double ComputeDeathPenaltyRawPenalty(
            List<(string key, double value)> absPositions,
            int targetLoss, int baseIncrement, int incrementStep)
        {
            if (absPositions.Count == 0 || targetLoss <= 0) return 0;

            double totalAbsPos = 0;
            int currentCredits = 0;
            foreach (var e in absPositions)
            {
                totalAbsPos += e.value;
                currentCredits += AbsolutePositionToToolState(e.value, baseIncrement, incrementStep).credits;
            }

            int targetCredits = Math.Max(0, currentCredits - targetLoss);
            if (currentCredits <= targetCredits) return 0;

            double lo = 0, hi = totalAbsPos + 1;
            for (int iter = 0; iter < 50; iter++)
            {
                double mid = (lo + hi) / 2;
                var test = absPositions.Select(e => (e.key, e.value)).ToList();
                DrainAccumulatorsLeveling(test, mid);
                int credits = 0;
                foreach (var e in test)
                    credits += AbsolutePositionToToolState(e.value, baseIncrement, incrementStep).credits;

                if (credits <= targetCredits)
                    hi = mid;
                else
                    lo = mid;
            }

            return hi;
        }

        /// <summary>
        /// Analytically compute the minimum rawPenalty needed to guarantee losing at least
        /// intendedLoss credits from a single-accumulator skill.
        /// </summary>
        private static double ComputeMinSingleAccumulatorPenalty(
            double currentAccumulator, int oldTotalCredits, int intendedLoss,
            int baseIncrement, int incrementStep)
        {
            if (intendedLoss <= 0 || oldTotalCredits <= 0) return 0;
            int targetCredits = Math.Max(0, oldTotalCredits - intendedLoss);
            double currentAbsPos = (double)oldTotalCredits * baseIncrement +
                (double)incrementStep * oldTotalCredits * (oldTotalCredits - 1) / 2.0 + Math.Max(0, currentAccumulator);
            // Max absolute position that still results in targetCredits (just below next credit boundary)
            double targetSum = (double)targetCredits * baseIncrement +
                (double)incrementStep * targetCredits * (targetCredits - 1) / 2.0;
            double nextCost = baseIncrement + targetCredits * incrementStep;
            double maxAllowed = targetSum + nextCost - 0.01;
            return Math.Max(0, currentAbsPos - maxAllowed);
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

            // --- Per-tool dictionary skills ---

            // Mining
            if (!DeathPenaltyExemptSkills.Contains("mining") && !DisabledSkills.Contains("mining"))
            {
                if (MiningProgress.TryGetValue(playerUid, out var miningProg) && (miningProg.TotalCredits > 0 || miningProg.PickaxeProgress.Count > 0))
                {
                    int oldCredits = miningProg.TotalCredits;

                    var toolEntries = miningProg.PickaxeProgress.Select(kvp =>
                        (kvp.Key, (double)kvp.Value.BlocksInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                    if (toolEntries.Count > 0)
                    {
                        double rawPenalty = BaseBlocksPerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                        var (newCr, _) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                            BaseBlocksPerIncrement, IncrementStep, oldCredits,
                            (k, a, s) => { if (miningProg.PickaxeProgress.TryGetValue(k, out var p)) {
                                p.BlocksInIncrement = (int)Math.Floor(a); p.CurrentIncrementSize = s; } },
                            k => miningProg.PickaxeProgress.Remove(k), null, "Mining");
                        miningProg.TotalCredits = newCr;
                        int actualLost = oldCredits - newCr;
                        if (actualLost > 0) totalCreditsLost += actualLost;
                        sb.AppendLine($"  Mining: {oldCredits} \u2192 {newCr} (-{actualLost} credits, {rawPenalty:F0} pts)");
                        foreach (var entry in toolEntries)
                        {
                            int oldToolCr = IncrementStep > 0 ? (entry.Item3 - BaseBlocksPerIncrement) / IncrementStep : 0;
                            if (miningProg.PickaxeProgress.TryGetValue(entry.Item1, out var after))
                            {
                                int newToolCr = IncrementStep > 0 ? (after.CurrentIncrementSize - BaseBlocksPerIncrement) / IncrementStep : 0;
                                int toolLost = oldToolCr - newToolCr;
                                sb.AppendLine($"    {entry.Item1}: {(int)entry.Item2}/{entry.Item3} \u2192 {after.BlocksInIncrement}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                            }
                            else
                                sb.AppendLine($"    {entry.Item1}: {(int)entry.Item2}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                        }
                    }
                    else if (oldCredits > 0)
                    {
                        int intendedLoss = (int)Math.Floor(DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits)));
                        intendedLoss = Math.Min(intendedLoss, oldCredits);
                        if (intendedLoss > 0)
                        {
                            miningProg.TotalCredits = Math.Max(0, oldCredits - intendedLoss);
                            int actualLost = oldCredits - miningProg.TotalCredits;
                            totalCreditsLost += actualLost;
                            sb.AppendLine($"  Mining: {oldCredits} \u2192 {miningProg.TotalCredits} (-{actualLost} credits)");
                        }
                    }
                    pendingMiningProgressSave = true;
                }
            }

            // Melee
            if (!DeathPenaltyExemptSkills.Contains("melee") && !DisabledSkills.Contains("melee"))
            {
                if (MeleeProgress.TryGetValue(playerUid, out var meleeProg) && (meleeProg.TotalCredits > 0 || meleeProg.WeaponProgress.Count > 0))
                {
                    int oldCredits = meleeProg.TotalCredits;

                    var toolEntries = meleeProg.WeaponProgress.Select(kvp =>
                        (kvp.Key, (double)kvp.Value.DamageInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                    if (toolEntries.Count > 0)
                    {
                        double rawPenalty = BaseDamagePerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                        var (newCr, _) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                            BaseDamagePerIncrement, MeleeIncrementStep, oldCredits,
                            (k, a, s) => { if (meleeProg.WeaponProgress.TryGetValue(k, out var p)) {
                                p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s; } },
                            k => meleeProg.WeaponProgress.Remove(k), null, "Melee");
                        meleeProg.TotalCredits = newCr;
                        int actualLost = oldCredits - newCr;
                        if (actualLost > 0) totalCreditsLost += actualLost;
                        sb.AppendLine($"  Melee: {oldCredits} \u2192 {newCr} (-{actualLost} credits, {rawPenalty:F0} pts)");
                        foreach (var entry in toolEntries)
                        {
                            int oldToolCr = MeleeIncrementStep > 0 ? (entry.Item3 - BaseDamagePerIncrement) / MeleeIncrementStep : 0;
                            if (meleeProg.WeaponProgress.TryGetValue(entry.Item1, out var after))
                            {
                                int newToolCr = MeleeIncrementStep > 0 ? (after.CurrentIncrementSize - BaseDamagePerIncrement) / MeleeIncrementStep : 0;
                                int toolLost = oldToolCr - newToolCr;
                                sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 {after.DamageInIncrement:F0}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                            }
                            else
                                sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                        }
                    }
                    else if (oldCredits > 0)
                    {
                        int intendedLoss = (int)Math.Floor(DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits)));
                        intendedLoss = Math.Min(intendedLoss, oldCredits);
                        if (intendedLoss > 0)
                        {
                            meleeProg.TotalCredits = Math.Max(0, oldCredits - intendedLoss);
                            int actualLost = oldCredits - meleeProg.TotalCredits;
                            totalCreditsLost += actualLost;
                            sb.AppendLine($"  Melee: {oldCredits} \u2192 {meleeProg.TotalCredits} (-{actualLost} credits)");
                        }
                    }
                    pendingMeleeProgressSave = true;
                }
            }

            // Ranged
            if (!DeathPenaltyExemptSkills.Contains("ranged") && !DisabledSkills.Contains("ranged"))
            {
                if (RangedProgress.TryGetValue(playerUid, out var rangedProg) && (rangedProg.TotalCredits > 0 || rangedProg.WeaponProgress.Count > 0))
                {
                    int oldCredits = rangedProg.TotalCredits;

                    var toolEntries = rangedProg.WeaponProgress.Select(kvp =>
                        (kvp.Key, (double)kvp.Value.DamageInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                    if (toolEntries.Count > 0)
                    {
                        double rawPenalty = BaseRangedDamagePerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                        var (newCr, _) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                            BaseRangedDamagePerIncrement, RangedIncrementStep, oldCredits,
                            (k, a, s) => { if (rangedProg.WeaponProgress.TryGetValue(k, out var p)) {
                                p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s; } },
                            k => rangedProg.WeaponProgress.Remove(k), null, "Ranged");
                        rangedProg.TotalCredits = newCr;
                        int actualLost = oldCredits - newCr;
                        if (actualLost > 0) totalCreditsLost += actualLost;
                        sb.AppendLine($"  Ranged: {oldCredits} \u2192 {newCr} (-{actualLost} credits, {rawPenalty:F0} pts)");
                        foreach (var entry in toolEntries)
                        {
                            int oldToolCr = RangedIncrementStep > 0 ? (entry.Item3 - BaseRangedDamagePerIncrement) / RangedIncrementStep : 0;
                            if (rangedProg.WeaponProgress.TryGetValue(entry.Item1, out var after))
                            {
                                int newToolCr = RangedIncrementStep > 0 ? (after.CurrentIncrementSize - BaseRangedDamagePerIncrement) / RangedIncrementStep : 0;
                                int toolLost = oldToolCr - newToolCr;
                                sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 {after.DamageInIncrement:F0}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                            }
                            else
                                sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                        }
                    }
                    else if (oldCredits > 0)
                    {
                        int intendedLoss = (int)Math.Floor(DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits)));
                        intendedLoss = Math.Min(intendedLoss, oldCredits);
                        if (intendedLoss > 0)
                        {
                            rangedProg.TotalCredits = Math.Max(0, oldCredits - intendedLoss);
                            int actualLost = oldCredits - rangedProg.TotalCredits;
                            totalCreditsLost += actualLost;
                            sb.AppendLine($"  Ranged: {oldCredits} \u2192 {rangedProg.TotalCredits} (-{actualLost} credits)");
                        }
                    }
                    pendingRangedProgressSave = true;
                }
            }

            // Precise
            if (!DeathPenaltyExemptSkills.Contains("precise") && !DisabledSkills.Contains("precise"))
            {
                if (PreciseProgress.TryGetValue(playerUid, out var preciseProg) && (preciseProg.TotalCredits > 0 || preciseProg.WeaponProgress.Count > 0))
                {
                    int oldCredits = preciseProg.TotalCredits;

                    var toolEntries = preciseProg.WeaponProgress.Select(kvp =>
                        (kvp.Key, (double)kvp.Value.DamageInIncrement, kvp.Value.CurrentIncrementSize)).ToList();

                    if (toolEntries.Count > 0)
                    {
                        double rawPenalty = BasePreciseDamagePerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                        var (newCr, _) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                            BasePreciseDamagePerIncrement, PreciseIncrementStep, oldCredits,
                            (k, a, s) => { if (preciseProg.WeaponProgress.TryGetValue(k, out var p)) {
                                p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s; } },
                            k => preciseProg.WeaponProgress.Remove(k), null, "Precise");
                        preciseProg.TotalCredits = newCr;
                        int actualLost = oldCredits - newCr;
                        if (actualLost > 0) totalCreditsLost += actualLost;
                        sb.AppendLine($"  Precise: {oldCredits} \u2192 {newCr} (-{actualLost} credits, {rawPenalty:F0} pts)");
                        foreach (var entry in toolEntries)
                        {
                            int oldToolCr = PreciseIncrementStep > 0 ? (entry.Item3 - BasePreciseDamagePerIncrement) / PreciseIncrementStep : 0;
                            if (preciseProg.WeaponProgress.TryGetValue(entry.Item1, out var after))
                            {
                                int newToolCr = PreciseIncrementStep > 0 ? (after.CurrentIncrementSize - BasePreciseDamagePerIncrement) / PreciseIncrementStep : 0;
                                int toolLost = oldToolCr - newToolCr;
                                sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 {after.DamageInIncrement:F0}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                            }
                            else
                                sb.AppendLine($"    {entry.Item1}: {entry.Item2:F0}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                        }
                    }
                    else if (oldCredits > 0)
                    {
                        int intendedLoss = (int)Math.Floor(DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits)));
                        intendedLoss = Math.Min(intendedLoss, oldCredits);
                        if (intendedLoss > 0)
                        {
                            preciseProg.TotalCredits = Math.Max(0, oldCredits - intendedLoss);
                            int actualLost = oldCredits - preciseProg.TotalCredits;
                            totalCreditsLost += actualLost;
                            sb.AppendLine($"  Precise: {oldCredits} \u2192 {preciseProg.TotalCredits} (-{actualLost} credits)");
                        }
                    }
                    pendingPreciseProgressSave = true;
                }
            }

            // --- Single accumulator skills ---

            // Walking
            if (!DeathPenaltyExemptSkills.Contains("walking") && !DisabledSkills.Contains("walking"))
            {
                if (WalkingProgress.TryGetValue(playerUid, out var walkingProg) && (walkingProg.TotalCredits > 0 || walkingProg.PartialCredit > 0))
                {
                    int oldCredits = walkingProg.TotalCredits;
                    float oldAcc = walkingProg.PartialCredit; int oldInc = walkingProg.CurrentIncrementSize;
                    double rawPenalty = BaseBlocksWalkedPerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                    var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                        oldAcc, oldInc, oldCredits, rawPenalty, BaseBlocksWalkedPerIncrement, WalkingIncrementStep, null, "Walking");
                    walkingProg.TotalCredits = newCr; walkingProg.PartialCredit = (float)newAcc; walkingProg.CurrentIncrementSize = newInc;
                    if (lost > 0) totalCreditsLost += lost;
                    pendingWalkingProgressSave = true;
                    sb.AppendLine($"  Walking: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc:F0}/{oldInc} \u2192 {(int)newAcc}/{newInc}");
                }
            }

            // Hunger
            if (!DeathPenaltyExemptSkills.Contains("hunger") && !DisabledSkills.Contains("hunger"))
            {
                if (HungerProgress.TryGetValue(playerUid, out var hungerProg) && (hungerProg.TotalCredits > 0 || hungerProg.SecondsInIncrement > 0))
                {
                    int oldCredits = hungerProg.TotalCredits;
                    float oldAcc = hungerProg.SecondsInIncrement; int oldInc = hungerProg.CurrentIncrementSize;
                    double rawPenalty = BaseSecondsPerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                    var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                        oldAcc, oldInc, oldCredits, rawPenalty, BaseSecondsPerIncrement, HungerIncrementStep, null, "Hunger");
                    hungerProg.TotalCredits = newCr; hungerProg.SecondsInIncrement = (float)newAcc; hungerProg.CurrentIncrementSize = newInc;
                    if (lost > 0) totalCreditsLost += lost;
                    pendingHungerProgressSave = true;
                    sb.AppendLine($"  Hunger: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc:F0}/{oldInc} \u2192 {(int)newAcc}/{newInc}");
                }
            }

            // Mender
            if (!DeathPenaltyExemptSkills.Contains("mender") && !DisabledSkills.Contains("mender"))
            {
                if (MenderProgress.TryGetValue(playerUid, out var menderProg) && (menderProg.TotalCredits > 0 || menderProg.RepairsInIncrement > 0))
                {
                    int oldCredits = menderProg.TotalCredits;
                    int oldAcc = menderProg.RepairsInIncrement; int oldInc = menderProg.CurrentIncrementSize;
                    double rawPenalty = BaseMenderRepairsPerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                    var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                        oldAcc, oldInc, oldCredits, rawPenalty, BaseMenderRepairsPerIncrement, MenderIncrementStep, null, "Mender");
                    menderProg.TotalCredits = newCr; menderProg.RepairsInIncrement = (int)Math.Floor(newAcc); menderProg.CurrentIncrementSize = newInc;
                    if (lost > 0) totalCreditsLost += lost;
                    pendingMenderProgressSave = true;
                    sb.AppendLine($"  Mender: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc}/{oldInc} \u2192 {(int)Math.Floor(newAcc)}/{newInc}");
                }
            }

            // Pilferer
            if (!DeathPenaltyExemptSkills.Contains("pilferer") && !DisabledSkills.Contains("pilferer"))
            {
                if (PilfererProgress.TryGetValue(playerUid, out var pilfererProg) && (pilfererProg.TotalCredits > 0 || pilfererProg.PointsInIncrement > 0))
                {
                    int oldCredits = pilfererProg.TotalCredits;
                    int oldAcc = pilfererProg.PointsInIncrement; int oldInc = pilfererProg.CurrentIncrementSize;
                    double rawPenalty = BasePilfererPointsPerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                    var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                        oldAcc, oldInc, oldCredits, rawPenalty, BasePilfererPointsPerIncrement, PilfererIncrementStep, null, "Pilferer");
                    pilfererProg.TotalCredits = newCr; pilfererProg.PointsInIncrement = (int)Math.Floor(newAcc); pilfererProg.CurrentIncrementSize = newInc;
                    if (lost > 0) totalCreditsLost += lost;
                    pendingPilfererProgressSave = true;
                    sb.AppendLine($"  Pilferer: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc}/{oldInc} \u2192 {(int)Math.Floor(newAcc)}/{newInc}");
                }
            }

            // Resourceful
            if (!DeathPenaltyExemptSkills.Contains("resourceful") && !DisabledSkills.Contains("resourceful"))
            {
                if (ResourcefulProgress.TryGetValue(playerUid, out var resourcefulProg) && (resourcefulProg.TotalCredits > 0 || resourcefulProg.AnimalsInIncrement > 0))
                {
                    int oldCredits = resourcefulProg.TotalCredits;
                    int oldAcc = resourcefulProg.AnimalsInIncrement; int oldInc = resourcefulProg.CurrentIncrementSize;
                    double rawPenalty = BaseResourcefulAnimalsPerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                    var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                        oldAcc, oldInc, oldCredits, rawPenalty, BaseResourcefulAnimalsPerIncrement, ResourcefulIncrementStep, null, "Resourceful");
                    resourcefulProg.TotalCredits = newCr; resourcefulProg.AnimalsInIncrement = (int)Math.Floor(newAcc); resourcefulProg.CurrentIncrementSize = newInc;
                    if (lost > 0) totalCreditsLost += lost;
                    pendingResourcefulProgressSave = true;
                    sb.AppendLine($"  Resourceful: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc}/{oldInc} \u2192 {(int)Math.Floor(newAcc)}/{newInc}");
                }
            }

            // Forager
            if (!DeathPenaltyExemptSkills.Contains("forager") && !DisabledSkills.Contains("forager"))
            {
                if (ForagerProgress.TryGetValue(playerUid, out var foragerProg) && (foragerProg.TotalCredits > 0 || foragerProg.CropsInIncrement > 0))
                {
                    int oldCredits = foragerProg.TotalCredits;
                    int oldAcc = foragerProg.CropsInIncrement; int oldInc = foragerProg.CurrentIncrementSize;
                    double rawPenalty = BaseForagerCropsPerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                    var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                        oldAcc, oldInc, oldCredits, rawPenalty, BaseForagerCropsPerIncrement, ForagerIncrementStep, null, "Forager");
                    foragerProg.TotalCredits = newCr; foragerProg.CropsInIncrement = (int)Math.Floor(newAcc); foragerProg.CurrentIncrementSize = newInc;
                    if (lost > 0) totalCreditsLost += lost;
                    pendingForagerProgressSave = true;
                    sb.AppendLine($"  Forager: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc}/{oldInc} \u2192 {(int)Math.Floor(newAcc)}/{newInc}");
                }
            }

            // Furtive
            if (!DeathPenaltyExemptSkills.Contains("furtive") && !DisabledSkills.Contains("furtive"))
            {
                if (FurtiveProgress.TryGetValue(playerUid, out var furtiveProg) && (furtiveProg.TotalCredits > 0 || furtiveProg.BlocksInIncrement > 0))
                {
                    int oldCredits = furtiveProg.TotalCredits;
                    float oldAcc = furtiveProg.BlocksInIncrement; int oldInc = furtiveProg.CurrentIncrementSize;
                    double rawPenalty = BaseFurtiveSneakBlocksPerIncrement * DeathPenaltyFraction * Math.Sqrt(Math.Max(1, oldCredits));
                    var (newCr, newAcc, newInc, lost) = ApplySingleAccumulatorDecay(
                        oldAcc, oldInc, oldCredits, rawPenalty, BaseFurtiveSneakBlocksPerIncrement, FurtiveIncrementStep, null, "Furtive");
                    furtiveProg.TotalCredits = newCr; furtiveProg.BlocksInIncrement = (float)newAcc; furtiveProg.CurrentIncrementSize = newInc;
                    if (lost > 0) totalCreditsLost += lost;
                    pendingFurtiveProgressSave = true;
                    sb.AppendLine($"  Furtive: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc:F0}/{oldInc} \u2192 {(int)newAcc}/{newInc}");
                }
            }

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
                                    (k, a, s) => { if (profKvp.Value.WeaponProgress.TryGetValue(k, out var p)) {
                                        p.DamageInIncrement = (float)a; p.CurrentIncrementSize = s; } },
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
                () => MiningProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Melee", "melee", playerUid, currentDay,
                () => MeleeProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Ranged", "ranged", playerUid, currentDay,
                () => RangedProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Precise", "precise", playerUid, currentDay,
                () => PreciseProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));

            // Movement/Survival skills
            sb.AppendLine("--- Movement/Survival ---");
            AppendDecayStatus(sb, "Walking", "walking", playerUid, currentDay,
                () => WalkingProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Hunger", "hunger", playerUid, currentDay,
                () => HungerProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Furtive", "furtive", playerUid, currentDay,
                () => FurtiveProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));

            // Utility skills
            sb.AppendLine("--- Utility ---");
            AppendDecayStatus(sb, "Mender", "mender", playerUid, currentDay,
                () => MenderProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Pilferer", "pilferer", playerUid, currentDay,
                () => PilfererProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Resourceful", "resourceful", playerUid, currentDay,
                () => ResourcefulProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));
            AppendDecayStatus(sb, "Forager", "forager", playerUid, currentDay,
                () => ForagerProgress.TryGetValue(playerUid, out var p) ? (p.LastActivityDay, p.TotalCredits) : (0, 0));

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
        private void DetectCombatOverhaul(ICoreServerAPI api)
        {
            // Accept the original Combat Overhaul AND the 1.22 fork
            // ("combatoverhaulfork"). The fork keeps CO's stat/trait names, so
            // detecting it re-enables proficiency progression, the bonus stat
            // application, and the /trait co* commands.
            IsCombatOverhaulLoaded = DetectAnyCombatOverhaul(api.ModLoader);
            IsCombatOverhaulForkLoaded = api.ModLoader.IsModEnabled("combatoverhaulfork");

            if (IsCombatOverhaulLoaded)
            {
                string which = IsCombatOverhaulForkLoaded
                    ? "Combat Overhaul (1.22 fork)" : "Combat Overhaul";
                if (COEnableCompat)
                {
                    api.Logger.Notification($"[SeraphLeveling] {which} detected - proficiency progression enabled");
                }
                else
                {
                    api.Logger.Notification($"[SeraphLeveling] {which} detected but compatibility disabled in config");
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
        /// Apply Big Head / Thick Skull handling for Combat Overhaul.
        /// Clockmaker has Big Head (+50% head/face damage), cancelled by armor credits.
        /// Malefactor has Thick Skull (-50% head/face damage) from start.
        /// Other classes can earn Thick Skull via armor credits.
        /// </summary>
        private static void ApplyCOBigHeadThickSkull(IServerPlayer player, int armorCredits)
        {
            if (!IsCOCompatEnabled) return;
            if (player?.Entity == null) return;

            var cache = GetCachedTraits(player.PlayerUID);
            bool hasBigHead = cache?.HasCOBigHead ?? false;     // Clockmaker
            bool hasThickSkull = cache?.HasCOThickSkull ?? false; // Malefactor

            float headFactor = 0f;
            float faceFactor = 0f;
            float remainingPenalty = 0f;

            if (hasBigHead)
            {
                // Big Head: +0.5 head/face damage, cancelled by 50 armor credits
                // After cancellation, can earn Thick Skull bonus (up to -0.5)
                remainingPenalty = Math.Max(0, CO_BIG_HEAD_PENALTY - armorCredits * 0.01f);

                if (armorCredits >= 50)
                {
                    // Penalty cancelled, now earning bonus
                    int bonusCredits = armorCredits - 50;
                    float bonus = Math.Min(bonusCredits * 0.01f, CO_THICK_SKULL_BONUS);
                    headFactor = -bonus;
                    faceFactor = -bonus;
                }
                else
                {
                    headFactor = remainingPenalty;
                    faceFactor = remainingPenalty;
                }
            }
            else if (hasThickSkull)
            {
                // Malefactor already has Thick Skull (-0.5), can't earn more
                headFactor = -CO_THICK_SKULL_BONUS;
                faceFactor = -CO_THICK_SKULL_BONUS;
            }
            else
            {
                // Other classes: can earn Thick Skull (up to -0.5)
                float bonus = Math.Min(armorCredits * 0.01f, CO_THICK_SKULL_BONUS);
                headFactor = -bonus;
                faceFactor = -bonus;
            }

            // headFactor and faceFactor above are the totals we want the player to end up
            // with. Big Head and Thick Skull have already put their own value into these
            // stats under the code "trait", and stat values sum, so write the difference.
            // See TraitStatValue.
            player.Entity.Stats.Set(CO_HEAD_DAMAGE_FACTOR, CO_STAT_PREFIX + "headDamage",
                headFactor - TraitStatValue(player.Entity, CO_HEAD_DAMAGE_FACTOR), false);
            player.Entity.Stats.Set(CO_FACE_DAMAGE_FACTOR, CO_STAT_PREFIX + "faceDamage",
                faceFactor - TraitStatValue(player.Entity, CO_FACE_DAMAGE_FACTOR), false);

            // Sync for UI
            player.Entity.WatchedAttributes.SetFloat(WATCHED_CO_BIG_HEAD_REMAINING, remainingPenalty);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_BIG_HEAD_REMAINING);
        }

        /// <summary>
        /// Apply Frightened of Melee / Melee Expert handling for Combat Overhaul.
        /// Clockmaker has Frightened of Melee (-1 melee slashing tier), cancelled by melee credits.
        /// Blackguard has Melee Expert (+1 melee slashing tier) from start.
        /// Other classes can earn up to +1 melee slashing tier via melee credits.
        /// </summary>
        private static void ApplyCOMeleeTier(IServerPlayer player, int meleeCredits)
        {
            if (!IsCOCompatEnabled) return;
            if (player?.Entity == null) return;

            var cache = GetCachedTraits(player.PlayerUID);
            bool hasFrightenedOfMelee = cache?.HasCOFearOfMelee ?? false; // Clockmaker
            bool hasMeleeExpert = cache?.HasCOMeleeExpert ?? false;        // Blackguard

            int netTier = 0;
            int remainingPenalty = 0;

            if (hasFrightenedOfMelee)
            {
                // -1 tier penalty, 100 credits to cancel
                remainingPenalty = Math.Max(0, CO_FRIGHTENED_TIER_PENALTY - meleeCredits / 100);

                if (meleeCredits >= 100)
                {
                    // Penalty cancelled, can earn positive tier
                    int bonusCredits = meleeCredits - 100;
                    int earnedTier = Math.Min(bonusCredits / 100, 1);
                    netTier = earnedTier;
                }
                else
                {
                    netTier = -remainingPenalty;
                }
            }
            else if (hasMeleeExpert)
            {
                // Blackguard has +1 tier from Melee Expert, can't earn more
                netTier = CO_MELEE_EXPERT_TIER_BONUS;
            }
            else
            {
                // Other classes: can earn up to +1 tier
                netTier = Math.Min(meleeCredits / 100, 1);
            }

            // netTier is the tier bonus we want in total. Melee Expert and Frightened of
            // Melee have already put theirs into this stat under the code "trait", so
            // subtract whatever is actually there. See TraitStatValue.
            player.Entity.Stats.Set(CO_MELEE_TIER_SLASHING, CO_STAT_PREFIX + "meleeTierSlashing",
                netTier - TraitStatValue(player.Entity, CO_MELEE_TIER_SLASHING), false);

            // Sync for UI
            player.Entity.WatchedAttributes.SetInt(WATCHED_CO_FRIGHTENED_REMAINING, remainingPenalty);
            player.Entity.WatchedAttributes.SetInt(WATCHED_CO_MELEE_TIER_BONUS, netTier);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_FRIGHTENED_REMAINING);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_MELEE_TIER_BONUS);
        }

        /// <summary>
        /// Apply Leg Day handling for Combat Overhaul.
        /// Blackguard has Leg Day (+100% leg/feet damage, +25% jump height).
        /// The leg damage penalty can be reduced by armor credits (100 credits = cancel).
        /// Jump height bonus is always applied (it's a benefit).
        /// </summary>
        private static void ApplyCOLegDay(IServerPlayer player, int armorCredits)
        {
            if (!IsCOCompatEnabled) return;
            if (player?.Entity == null) return;

            var cache = GetCachedTraits(player.PlayerUID);
            bool hasLegDay = cache?.HasCOLegDay ?? false; // Blackguard

            if (!hasLegDay) return;

            // Leg Day: +1.0 leg/feet damage, +0.25 jump height
            // Leg damage penalty reduced by armor credits (100 credits = cancel)
            float remainingPenalty = Math.Max(0, CO_LEG_DAY_PENALTY - armorCredits * 0.01f);

            // The Leg Day trait has already applied its own values under the stat code
            // "trait", so write the difference between what we want and what it gave.
            // See TraitStatValue.
            player.Entity.Stats.Set(CO_LEGS_DAMAGE_FACTOR, CO_STAT_PREFIX + "legsDamage",
                remainingPenalty - TraitStatValue(player.Entity, CO_LEGS_DAMAGE_FACTOR), false);
            player.Entity.Stats.Set(CO_FEET_DAMAGE_FACTOR, CO_STAT_PREFIX + "feetDamage",
                remainingPenalty - TraitStatValue(player.Entity, CO_FEET_DAMAGE_FACTOR), false);
            // The jump bonus is a benefit the player keeps in full, so we only top up
            // whatever the trait did not already provide.
            player.Entity.Stats.Set(CO_JUMP_HEIGHT, CO_STAT_PREFIX + "jumpHeight",
                CO_LEG_DAY_JUMP_BONUS - TraitStatValue(player.Entity, CO_JUMP_HEIGHT), false);

            // Sync for UI
            player.Entity.WatchedAttributes.SetFloat(WATCHED_CO_LEG_DAY_REMAINING, remainingPenalty);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CO_LEG_DAY_REMAINING);
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
            ServerApi.Logger.Debug($"[SeraphLeveling] Config saved to ModConfig/{CONFIG_FILE_NAME} (Mining: Base={BaseBlocksPerIncrement}, Max={MaxMiningSpeedPercent}% | Melee: Base={BaseDamagePerIncrement}, Max={MaxMeleeDamagePercent}% | CO: {COProficiencyBaseOverrides.Count} base overrides, {COProficiencyIncrementOverrides.Count} increment overrides)");
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

                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        byte version = reader.ReadByte();

                        if (version <= 2)
                        {
                            // Legacy format: just had BaseBlocksPerLevel (now BaseBlocksPerIncrement)
                            int legacyBase = reader.ReadInt32();
                            BaseBlocksPerIncrement = legacyBase;
                            IncrementStep = legacyBase; // Match old behavior

                            if (version >= 2)
                            {
                                MaxMiningSpeedPercent = reader.ReadInt32();
                            }
                            // OreMultiplier uses default (5)
                            // Melee, Ranged, Walking, and Hunger use defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 3)
                        {
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            // Melee, Ranged, Walking, and Hunger use defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 4)
                        {
                            // Version 4: has melee config but not ranged, walking, or hunger
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            BaseDamagePerIncrement = reader.ReadInt32();
                            MeleeIncrementStep = reader.ReadInt32();
                            MaxMeleeDamagePercent = reader.ReadInt32();
                            // Ranged, Walking, and Hunger use defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 5)
                        {
                            // Version 5: has ranged config but not walking or hunger
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            BaseDamagePerIncrement = reader.ReadInt32();
                            MeleeIncrementStep = reader.ReadInt32();
                            MaxMeleeDamagePercent = reader.ReadInt32();
                            BaseRangedDamagePerIncrement = reader.ReadInt32();
                            RangedIncrementStep = reader.ReadInt32();
                            MaxRangedDamagePercent = reader.ReadInt32();
                            MaxRangedAccuracyPercent = reader.ReadInt32();
                            MaxRangedDistancePercent = reader.ReadInt32();
                            // Walking and Hunger use defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 6)
                        {
                            // Version 6: has walking config but not hunger
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            BaseDamagePerIncrement = reader.ReadInt32();
                            MeleeIncrementStep = reader.ReadInt32();
                            MaxMeleeDamagePercent = reader.ReadInt32();
                            BaseRangedDamagePerIncrement = reader.ReadInt32();
                            RangedIncrementStep = reader.ReadInt32();
                            MaxRangedDamagePercent = reader.ReadInt32();
                            MaxRangedAccuracyPercent = reader.ReadInt32();
                            MaxRangedDistancePercent = reader.ReadInt32();
                            BaseBlocksWalkedPerIncrement = reader.ReadInt32();
                            WalkingIncrementStep = reader.ReadInt32();
                            MaxWalkingSpeedPercent = reader.ReadInt32();
                            // Hunger uses defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 7)
                        {
                            // Version 7: has hunger config but not armor
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            BaseDamagePerIncrement = reader.ReadInt32();
                            MeleeIncrementStep = reader.ReadInt32();
                            MaxMeleeDamagePercent = reader.ReadInt32();
                            BaseRangedDamagePerIncrement = reader.ReadInt32();
                            RangedIncrementStep = reader.ReadInt32();
                            MaxRangedDamagePercent = reader.ReadInt32();
                            MaxRangedAccuracyPercent = reader.ReadInt32();
                            MaxRangedDistancePercent = reader.ReadInt32();
                            BaseBlocksWalkedPerIncrement = reader.ReadInt32();
                            WalkingIncrementStep = reader.ReadInt32();
                            MaxWalkingSpeedPercent = reader.ReadInt32();
                            BaseSecondsPerIncrement = reader.ReadInt32();
                            HungerIncrementStep = reader.ReadInt32();
                            MaxHungerReductionPercent = reader.ReadInt32();
                            // Armor uses defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 8)
                        {
                            // Version 8: has armor config but not CO config
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            BaseDamagePerIncrement = reader.ReadInt32();
                            MeleeIncrementStep = reader.ReadInt32();
                            MaxMeleeDamagePercent = reader.ReadInt32();
                            BaseRangedDamagePerIncrement = reader.ReadInt32();
                            RangedIncrementStep = reader.ReadInt32();
                            MaxRangedDamagePercent = reader.ReadInt32();
                            MaxRangedAccuracyPercent = reader.ReadInt32();
                            MaxRangedDistancePercent = reader.ReadInt32();
                            BaseBlocksWalkedPerIncrement = reader.ReadInt32();
                            WalkingIncrementStep = reader.ReadInt32();
                            MaxWalkingSpeedPercent = reader.ReadInt32();
                            BaseSecondsPerIncrement = reader.ReadInt32();
                            HungerIncrementStep = reader.ReadInt32();
                            MaxHungerReductionPercent = reader.ReadInt32();
                            BaseSecondsInArmorPerIncrement = reader.ReadInt32();
                            ArmorTimeIncrementStep = reader.ReadInt32();
                            BaseDamageBlockedPerIncrement = reader.ReadInt32();
                            ArmorDamageIncrementStep = reader.ReadInt32();
                            BaseRepairsPerIncrement = reader.ReadInt32();
                            ArmorRepairIncrementStep = reader.ReadInt32();
                            MaxArmorDurabilityPercent = reader.ReadInt32();
                            MaxArmorWalkSpeedPercent = reader.ReadInt32();
                            // CO config uses defaults

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 9)
                        {
                            // Version 9: has global CO config but no per-proficiency overrides
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            BaseDamagePerIncrement = reader.ReadInt32();
                            MeleeIncrementStep = reader.ReadInt32();
                            MaxMeleeDamagePercent = reader.ReadInt32();
                            BaseRangedDamagePerIncrement = reader.ReadInt32();
                            RangedIncrementStep = reader.ReadInt32();
                            MaxRangedDamagePercent = reader.ReadInt32();
                            MaxRangedAccuracyPercent = reader.ReadInt32();
                            MaxRangedDistancePercent = reader.ReadInt32();
                            BaseBlocksWalkedPerIncrement = reader.ReadInt32();
                            WalkingIncrementStep = reader.ReadInt32();
                            MaxWalkingSpeedPercent = reader.ReadInt32();
                            BaseSecondsPerIncrement = reader.ReadInt32();
                            HungerIncrementStep = reader.ReadInt32();
                            MaxHungerReductionPercent = reader.ReadInt32();
                            BaseSecondsInArmorPerIncrement = reader.ReadInt32();
                            ArmorTimeIncrementStep = reader.ReadInt32();
                            BaseDamageBlockedPerIncrement = reader.ReadInt32();
                            ArmorDamageIncrementStep = reader.ReadInt32();
                            BaseRepairsPerIncrement = reader.ReadInt32();
                            ArmorRepairIncrementStep = reader.ReadInt32();
                            MaxArmorDurabilityPercent = reader.ReadInt32();
                            MaxArmorWalkSpeedPercent = reader.ReadInt32();
                            COBaseDamagePerIncrement = reader.ReadInt32();
                            COIncrementStep = reader.ReadInt32();
                            // Per-proficiency overrides use defaults (empty)

                            // Mark for re-save in new format
                            pendingConfigSave = true;
                        }
                        else if (version == 10)
                        {
                            // Current format with per-proficiency CO config
                            BaseBlocksPerIncrement = reader.ReadInt32();
                            IncrementStep = reader.ReadInt32();
                            MaxMiningSpeedPercent = reader.ReadInt32();
                            OreMultiplier = reader.ReadInt32();
                            BaseDamagePerIncrement = reader.ReadInt32();
                            MeleeIncrementStep = reader.ReadInt32();
                            MaxMeleeDamagePercent = reader.ReadInt32();
                            BaseRangedDamagePerIncrement = reader.ReadInt32();
                            RangedIncrementStep = reader.ReadInt32();
                            MaxRangedDamagePercent = reader.ReadInt32();
                            MaxRangedAccuracyPercent = reader.ReadInt32();
                            MaxRangedDistancePercent = reader.ReadInt32();
                            BaseBlocksWalkedPerIncrement = reader.ReadInt32();
                            WalkingIncrementStep = reader.ReadInt32();
                            MaxWalkingSpeedPercent = reader.ReadInt32();
                            BaseSecondsPerIncrement = reader.ReadInt32();
                            HungerIncrementStep = reader.ReadInt32();
                            MaxHungerReductionPercent = reader.ReadInt32();
                            BaseSecondsInArmorPerIncrement = reader.ReadInt32();
                            ArmorTimeIncrementStep = reader.ReadInt32();
                            BaseDamageBlockedPerIncrement = reader.ReadInt32();
                            ArmorDamageIncrementStep = reader.ReadInt32();
                            BaseRepairsPerIncrement = reader.ReadInt32();
                            ArmorRepairIncrementStep = reader.ReadInt32();
                            MaxArmorDurabilityPercent = reader.ReadInt32();
                            MaxArmorWalkSpeedPercent = reader.ReadInt32();
                            COBaseDamagePerIncrement = reader.ReadInt32();
                            COIncrementStep = reader.ReadInt32();
                            // Per-proficiency base overrides
                            int baseCount = reader.ReadInt32();
                            COProficiencyBaseOverrides.Clear();
                            for (int i = 0; i < baseCount; i++)
                            {
                                string key = reader.ReadString();
                                int val = reader.ReadInt32();
                                COProficiencyBaseOverrides[key] = val;
                            }
                            // Per-proficiency increment overrides
                            int incCount = reader.ReadInt32();
                            COProficiencyIncrementOverrides.Clear();
                            for (int i = 0; i < incCount; i++)
                            {
                                string key = reader.ReadString();
                                int val = reader.ReadInt32();
                                COProficiencyIncrementOverrides[key] = val;
                            }
                        }
                    }
                }

                ServerApi.Logger.Notification($"[SeraphLeveling] Config loaded (Mining: Base={BaseBlocksPerIncrement}, Max={MaxMiningSpeedPercent}% | Melee: Base={BaseDamagePerIncrement}, Max={MaxMeleeDamagePercent}% | Ranged: Base={BaseRangedDamagePerIncrement}, MaxDmg={MaxRangedDamagePercent}% | Walking: Base={BaseBlocksWalkedPerIncrement}, Max={MaxWalkingSpeedPercent}% | Hunger: Base={BaseSecondsPerIncrement}, Max={MaxHungerReductionPercent}% | Armor: MaxDur={MaxArmorDurabilityPercent}%, MaxWalk={MaxArmorWalkSpeedPercent}%)");

                // Fold the world's values into the config file and drop the blob, so
                // this world never reads from the save game again. An empty array is
                // what GetData returns for a missing key, so older builds of the mod
                // would also treat this as "nothing stored" if the world is opened by
                // one of them again.
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
        /// Handler for /trait clothier command.
        /// </summary>
        private TextCommandResult OnTraitClothierCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = ClothierProgress.GetOrAdd(player.PlayerUID, _ => new ClothierProgressData());
            int uniqueCount = progress.UniqueClothesWorn.Count;
            bool unlocked = progress.SewingKitUnlocked;

            var sb = new StringBuilder();
            sb.AppendLine($"Clothier progression: {uniqueCount} / {ClothierRequiredUniqueClothes} unique clothes worn");
            if (unlocked)
            {
                sb.AppendLine("Status: Sewing kit crafting UNLOCKED!");
            }
            else
            {
                sb.AppendLine($"Status: Wear {ClothierRequiredUniqueClothes - uniqueCount} more unique clothes to unlock sewing kit");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait clothierrequired command.
        /// </summary>
        private TextCommandResult OnTraitClothierRequiredCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Required clothes must be at least 1.");
                ClothierRequiredUniqueClothes = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Clothier required unique clothes set to {ClothierRequiredUniqueClothes}.");
            }

            return TextCommandResult.Success($"Current clothier required: {ClothierRequiredUniqueClothes} unique clothes.");
        }

        /// <summary>
        /// Handler for /trait clothierlevel command.
        /// Gets or sets the player's clothier progress (unique clothes count).
        /// </summary>
        private TextCommandResult OnTraitClothierLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = ClothierProgress.GetOrAdd(player.PlayerUID, _ => new ClothierProgressData());

            int? newLevel = (int?)args[0];

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                int currentLevel = progress.UniqueClothesWorn.Count;
                string status = progress.SewingKitUnlocked ? "Sewing kit UNLOCKED!" : $"{ClothierRequiredUniqueClothes - currentLevel} more needed to unlock.";
                return TextCommandResult.Success($"Current clothier level: {currentLevel}/{ClothierRequiredUniqueClothes}. {status}");
            }

            if (newLevel.Value < 0)
                return TextCommandResult.Error("Level must be 0 or greater.");

            // Clear the existing clothes set
            progress.UniqueClothesWorn.Clear();

            // Add placeholder entries up to the desired level
            for (int i = 0; i < newLevel.Value; i++)
            {
                progress.UniqueClothesWorn.Add($"__placeholder_cloth_{i}");
            }

            // Set unlock status based on whether we've reached the required amount
            progress.SewingKitUnlocked = newLevel.Value >= ClothierRequiredUniqueClothes;

            pendingClothierProgressSave = true;

            // Apply the bonus (this updates WatchedAttributes and extraTraits)
            ApplyClothierBonusStatic(player, progress);

            string newStatus = progress.SewingKitUnlocked ? "Sewing kit UNLOCKED!" : $"{ClothierRequiredUniqueClothes - newLevel.Value} more needed to unlock.";
            return TextCommandResult.Success($"Clothier level set to {newLevel.Value}/{ClothierRequiredUniqueClothes}. {newStatus}");
        }

        /// <summary>
        /// Tick handler for clothing tracking.
        /// </summary>
        private void OnClothingTick(float dt)
        {
            if (ServerApi == null) return;

            // Skip clothier progression if disabled
            if (IsSkillDisabled("clothier")) return;

            foreach (IServerPlayer player in ServerApi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;
                if (!player.Entity.Alive) continue;

                string playerUid = player.PlayerUID;
                var clothierProgress = ClothierProgress.GetOrAdd(playerUid, _ => new ClothierProgressData());

                // Skip if already unlocked
                if (clothierProgress.SewingKitUnlocked) continue;

                // Get the player's currently equipped clothing using character inventory
                var characterInventory = player.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
                if (characterInventory != null)
                {
                    foreach (var slot in characterInventory)
                    {
                        if (slot?.Itemstack?.Collectible != null)
                        {
                            string itemCode = slot.Itemstack.Collectible.Code?.ToString();
                            if (IsClothingItem(itemCode))
                            {
                                if (clothierProgress.UniqueClothesWorn.Add(itemCode))
                                {
                                    // New unique clothing worn
                                    pendingClothierProgressSave = true;
                                    ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} wore new clothing: {itemCode} ({clothierProgress.UniqueClothesWorn.Count}/{ClothierRequiredUniqueClothes})");

                                    // Check if unlocked
                                    if (clothierProgress.UniqueClothesWorn.Count >= ClothierRequiredUniqueClothes && !clothierProgress.SewingKitUnlocked)
                                    {
                                        clothierProgress.SewingKitUnlocked = true;
                                        ApplyClothierBonusStatic(player, clothierProgress);
                                        NotifyLevelUp(player,
                                            Lang.Get("seraphleveling:message-clothier-unlocked"));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check if an item code represents clothing (not armor) and is not blacklisted.
        /// Starting class outfits are blacklisted by default to prevent easy Clothier progression.
        /// </summary>
        private static bool IsClothingItem(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode)) return false;
            string lowerCode = itemCode.ToLowerInvariant();

            // Check if item is blacklisted (starting class outfits)
            if (ClothierBlacklistedItems != null)
            {
                foreach (string pattern in ClothierBlacklistedItems)
                {
                    if (!string.IsNullOrEmpty(pattern) && lowerCode.Contains(pattern.ToLowerInvariant()))
                    {
                        return false;
                    }
                }
            }

            // Clothing items include clothes, not armor
            if (lowerCode.Contains("clothes-")) return true;
            if (lowerCode.Contains("shirt-")) return true;
            if (lowerCode.Contains("trousers-")) return true;
            if (lowerCode.Contains("dress-")) return true;
            if (lowerCode.Contains("hat-")) return true;
            if (lowerCode.Contains("cape-")) return true;
            if (lowerCode.Contains("cloak-")) return true;
            if (lowerCode.Contains("jacket-")) return true;
            if (lowerCode.Contains("vest-")) return true;
            if (lowerCode.Contains("skirt-")) return true;
            if (lowerCode.Contains("gloves-")) return true;
            if (lowerCode.Contains("boots-")) return true;
            if (lowerCode.Contains("shoes-")) return true;
            if (lowerCode.Contains("headband-")) return true;
            if (lowerCode.Contains("mask-")) return true;
            if (lowerCode.Contains("scarf-")) return true;

            return false;
        }

        /// <summary>
        /// Apply clothier bonus (update WatchedAttributes for client sync).
        /// Also adds "clothier" to extraTraits to unlock sewing kit recipes.
        /// </summary>
        private static void ApplyClothierBonusStatic(IServerPlayer player, ClothierProgressData progress)
        {
            if (player?.Entity == null) return;

            player.Entity.WatchedAttributes.SetInt(WATCHED_CLOTHIER_COUNT, progress.UniqueClothesWorn.Count);
            player.Entity.WatchedAttributes.SetBool(WATCHED_CLOTHIER_UNLOCKED, progress.SewingKitUnlocked);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_CLOTHIER_COUNT);

            // Update extraTraits to show Clothier trait if unlocked (for UI display)
            UpdateExtraTraitStatic(player.Entity, CLOTHIER_TRAIT_CODE, progress.SewingKitUnlocked);

            // IMPORTANT: Add "clothier" to extraTraits to unlock sewing kit recipes
            // The game's recipe system checks extraTraits for dynamically granted traits
            // that unlock recipes via requiresTrait (e.g., the sewing kit requires "clothier")
            UpdateExtraTraitStatic(player.Entity, "clothier", progress.SewingKitUnlocked);
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
            // Skip furtive progression if disabled
            if (IsSkillDisabled("furtive")) return;

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

                // Get or create player progress data
                var playerProgress = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData
                {
                    CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement
                });

                // Skip all processing if already at max
                if (playerProgress.TotalCredits >= MaxFurtivePercent) continue;

                int oldCredits = playerProgress.TotalCredits;

                // Apply sleep buff multiplier to distance
                float modifiedDistance = ApplyXPMultiplier(playerUid, distance);

                // Add distance to progress
                playerProgress.BlocksInIncrement += modifiedDistance;

                // Check if we've earned any new credits
                while (playerProgress.BlocksInIncrement >= playerProgress.CurrentIncrementSize && playerProgress.TotalCredits < MaxFurtivePercent)
                {
                    // Earn a credit
                    playerProgress.TotalCredits++;
                    playerProgress.BlocksInIncrement -= playerProgress.CurrentIncrementSize;
                    playerProgress.CurrentIncrementSize += FurtiveIncrementStep;

                    ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned furtive credit {playerProgress.TotalCredits}, next requires {playerProgress.CurrentIncrementSize} blocks");
                }

                // Mark for saving if any progress was made
                if (playerProgress.BlocksInIncrement > 0 || playerProgress.TotalCredits > oldCredits)
                {
                    pendingFurtiveProgressSave = true;
                }

                // If credits increased, update the stat and notify player
                if (playerProgress.TotalCredits > oldCredits)
                {
                    UpdateSkillActivityDay(playerUid, "furtive");
                    ApplyFurtiveBonusStatic(player, playerProgress.TotalCredits);

                    // Notify player of level up with raw improvement (shows progress even when capped)
                    NotifyLevelUp(player,
                        Lang.Get("seraphleveling:message-furtive-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits));
                }
            }
        }

        /// <summary>
        /// Apply the Furtive bonus to a player based on their earned credits.
        /// The bonus reduces animal detection range.
        /// </summary>
        private static int ApplyFurtiveBonusStatic(IServerPlayer player, int credits)
        {
            // Check if player has vanilla Furtive trait (Malefactor)
            bool hasVanillaFurtive = PlayerHasVanillaFurtiveStatic(player.Entity);

            // Calculate effective cap (vanilla trait already gives max, so no additional bonus possible)
            int effectiveMax = hasVanillaFurtive ? 0 : MaxFurtivePercent;

            // Clamp credits to effective max
            int effectiveCredits = Math.Min(credits, effectiveMax);

            // Calculate bonus percent (reduction in detection range)
            int bonusPercent = effectiveCredits;

            // animalSeekingRange blends as WeightedSum over a base of 1, so each
            // stat value is a delta, not a multiplier. Vanilla Furtive stores
            // -0.35 and ends up at 0.65, meaning animals see you at 65% of the
            // normal range. Writing 0.65 here would blend to 1.65 and let them
            // see you 65% further away, so the bonus has to be negative.
            if (bonusPercent > 0)
            {
                float statValue = -(bonusPercent / 100f);
                player.Entity.Stats.Set("animalSeekingRange", FURTIVE_STAT_CODE, statValue, false);
            }
            else
            {
                player.Entity.Stats.Remove("animalSeekingRange", FURTIVE_STAT_CODE);
            }

            // Update WatchedAttributes for client sync
            player.Entity.WatchedAttributes.SetInt(WATCHED_FURTIVE_LEVEL, credits);
            player.Entity.WatchedAttributes.SetInt(WATCHED_FURTIVE_BONUS, bonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaFurtive", hasVanillaFurtive);

            // Update extraTraits for character sheet display
            UpdateExtraTraitStatic(player.Entity, FURTIVE_TRAIT_CODE, credits > 0 && !hasVanillaFurtive);

            return bonusPercent;
        }

        /// <summary>
        /// Check if player has the vanilla Furtive trait (Malefactor).
        /// </summary>
        private static bool PlayerHasVanillaFurtiveStatic(EntityPlayer entity)
        {
            if (entity == null) return false;

            // Check if player class is Malefactor
            var classTree = entity.WatchedAttributes.GetTreeAttribute("charClass");
            if (classTree != null)
            {
                string classCode = classTree.GetString("code", "").ToLowerInvariant();
                return classCode == "malefactor";
            }

            return false;
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

            // Check if precise skill is disabled
            if (IsSkillDisabled("precise")) return;

            string playerUid = attackerPlayer.PlayerUID;

            // Get or create player progress data
            var playerProgress = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());

            // Check if already at max
            int effectiveMax = GetPreciseEffectiveMax(attackerPlayer.Entity);
            if (playerProgress.TotalCredits >= effectiveMax) return;

            int oldCredits = playerProgress.TotalCredits;

            // Get or create weapon progress
            var weaponProgress = playerProgress.GetWeaponProgress(weaponType);

            // Add damage to progress (apply sleep buff multiplier if active)
            float modifiedDamage = ApplyXPMultiplier(attackerPlayer.PlayerUID, damage);
            weaponProgress.DamageInIncrement += modifiedDamage;

            // Check if we've earned any new credits
            while (weaponProgress.DamageInIncrement >= weaponProgress.CurrentIncrementSize && playerProgress.TotalCredits < effectiveMax)
            {
                // Earn a credit
                playerProgress.TotalCredits++;
                weaponProgress.DamageInIncrement -= weaponProgress.CurrentIncrementSize;
                weaponProgress.CurrentIncrementSize += PreciseIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {attackerPlayer.PlayerName} earned precise credit {playerProgress.TotalCredits} with {weaponType}, next requires {weaponProgress.CurrentIncrementSize} damage");
            }

            // Mark for saving if any progress was made
            if (damage > 0)
            {
                pendingPreciseProgressSave = true;
            }

            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "precise");

            // If credits increased, update the stat and notify player
            if (playerProgress.TotalCredits > oldCredits)
            {
                ApplyPreciseBonusStatic(attackerPlayer, playerProgress.TotalCredits);

                // Notify player of level up with raw improvement (shows progress even when capped)
                NotifyLevelUp(attackerPlayer,
                    Lang.Get("seraphleveling:message-precise-level-up", playerProgress.TotalCredits, playerProgress.TotalCredits));

                // Check if Tinkerer should be unlocked
                CheckTinkererUnlock(attackerPlayer);
            }
        }

        /// <summary>
        /// Get the effective maximum for Precise based on player class.
        /// Clockmaker has vanilla +25%, so they can only earn 5 more levels.
        /// </summary>
        private static int GetPreciseEffectiveMax(EntityPlayer entity)
        {
            if (PlayerHasVanillaPreciseStatic(entity))
            {
                // Clockmaker already has +25%, cap at +5 more to reach 30% total
                return MaxPrecisePercent - VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS;
            }
            return MaxPrecisePercent;
        }

        /// <summary>
        /// Apply the Precise bonus to a player based on their earned credits.
        /// The bonus increases damage to mechanical creatures.
        /// </summary>
        private static int ApplyPreciseBonusStatic(IServerPlayer player, int credits)
        {
            // Check if player has vanilla Precise trait (Clockmaker)
            bool hasVanillaPrecise = PlayerHasVanillaPreciseStatic(player.Entity);

            // Calculate effective cap
            int effectiveMax = hasVanillaPrecise ? (MaxPrecisePercent - VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS) : MaxPrecisePercent;

            // Clamp credits to effective max
            int effectiveCredits = Math.Min(credits, effectiveMax);

            // Calculate bonus percent
            int bonusPercent = effectiveCredits;

            // mechanicalsDamage is a delta on a base of 1, the same as vanilla
            // Precise's 0.4705. Writing 1 + bonus added a whole extra 100% on top
            // of the bonus the character sheet advertises.
            if (bonusPercent > 0)
            {
                float statValue = bonusPercent / 100f;
                player.Entity.Stats.Set("mechanicalsDamage", PRECISE_STAT_CODE, statValue, false);
            }
            else
            {
                player.Entity.Stats.Remove("mechanicalsDamage", PRECISE_STAT_CODE);
            }

            // Update WatchedAttributes for client sync
            player.Entity.WatchedAttributes.SetInt(WATCHED_PRECISE_LEVEL, credits);
            player.Entity.WatchedAttributes.SetInt(WATCHED_PRECISE_BONUS, bonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaPrecise", hasVanillaPrecise);

            // Update extraTraits for character sheet display
            UpdateExtraTraitStatic(player.Entity, PRECISE_TRAIT_CODE, credits > 0 && !hasVanillaPrecise);

            return bonusPercent;
        }

        /// <summary>
        /// Check if player has the vanilla Precise trait (Clockmaker).
        /// </summary>
        private static bool PlayerHasVanillaPreciseStatic(EntityPlayer entity)
        {
            if (entity == null) return false;

            // Check if player class is Clockmaker
            var classTree = entity.WatchedAttributes.GetTreeAttribute("charClass");
            if (classTree != null)
            {
                string classCode = classTree.GetString("code", "").ToLowerInvariant();
                return classCode == "clockmaker";
            }

            return false;
        }

        // =========================================================================
        // UNLOCK CHECKING METHODS
        // =========================================================================

        /// <summary>
        /// Check and apply Hardy health unlock if thresholds are met.
        /// Requires 110% mining speed and 10% armor durability.
        /// </summary>
        private static void CheckHardyHealthUnlock(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            // Check if hardyhealth skill is disabled
            if (IsSkillDisabled("hardyhealth")) return;

            string playerUid = player.PlayerUID;
            var progress = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Check mining speed threshold
            var miningProgress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            if (miningProgress.TotalCredits < HardyHealthMiningThreshold) return;

            // Check armor durability threshold
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            if (armorProgress.TotalDurabilityCredits < HardyHealthArmorDurabilityThreshold) return;

            // Both thresholds met - unlock Hardy health!
            progress.IsUnlocked = true;
            pendingHardyHealthProgressSave = true;

            // Apply the health bonus
            ApplyHardyHealthBonusStatic(player, true);

            // Notify player
            NotifyLevelUp(player,
                Lang.Get("seraphleveling:message-hardy-health-unlock", HardyHealthBonus));
        }

        /// <summary>
        /// Apply Hardy health bonus (+5 HP).
        /// </summary>
        private static void ApplyHardyHealthBonusStatic(IServerPlayer player, bool unlocked)
        {
            if (unlocked)
            {
                player.Entity.Stats.Set("maxhealthExtraPoints", HARDY_HEALTH_STAT_CODE, HardyHealthBonus, false);
            }
            else
            {
                player.Entity.Stats.Remove("maxhealthExtraPoints", HARDY_HEALTH_STAT_CODE);
            }

            player.Entity.WatchedAttributes.SetBool(WATCHED_HARDY_HEALTH_UNLOCKED, unlocked);
            UpdateExtraTraitStatic(player.Entity, HARDY_HEALTH_TRAIT_CODE, unlocked);
        }

        /// <summary>
        /// Check and apply Tinkerer unlock if thresholds are met.
        /// Requires Technical trait AND 10% Precise damage bonus.
        /// </summary>
        private static void CheckTinkererUnlock(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Check Technical trait
            var technicalProgress = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            if (!technicalProgress.IsUnlocked) return;

            // Check Precise threshold
            var preciseProgress = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());
            if (preciseProgress.TotalCredits < TinkererPreciseThreshold) return;

            // Both conditions met - unlock Tinkerer!
            progress.IsUnlocked = true;
            pendingTinkererProgressSave = true;

            // Apply the trait
            ApplyTinkererBonusStatic(player, true);

            // Notify player
            NotifyLevelUp(player,
                Lang.Get("seraphleveling:message-tinkerer-unlock"));
        }

        /// <summary>
        /// Apply Technical trait (unlocks translocator gear cost reduction).
        /// Sets the temporalGearTLRepairCost stat to -1 when unlocked, reducing gear cost by 1.
        /// </summary>
        private static void ApplyTechnicalBonusStatic(IServerPlayer player, bool unlocked)
        {
            player.Entity.WatchedAttributes.SetBool(WATCHED_TECHNICAL_UNLOCKED, unlocked);
            UpdateExtraTraitStatic(player.Entity, TECHNICAL_TRAIT_CODE, unlocked);

            // Set the temporal gear repair cost reduction stat
            // -1 means one fewer temporal gear needed to repair translocators
            float gearCostReduction = unlocked ? -1f : 0f;
            player.Entity.Stats.Set("temporalGearTLRepairCost", TECHNICAL_STAT_CODE, gearCostReduction, false);
        }

        /// <summary>
        /// Apply Tinkerer trait (unlocks tuning spear crafting).
        /// Also adds "tinkerer" to extraTraits to unlock tuning spear recipes.
        /// </summary>
        private static void ApplyTinkererBonusStatic(IServerPlayer player, bool unlocked)
        {
            player.Entity.WatchedAttributes.SetBool(WATCHED_TINKERER_UNLOCKED, unlocked);

            // Update extraTraits to show Tinkerer trait if unlocked (for UI display)
            UpdateExtraTraitStatic(player.Entity, TINKERER_TRAIT_CODE, unlocked);

            // IMPORTANT: Add "tinkerer" to extraTraits to unlock tuning spear recipes
            // The game's recipe system checks extraTraits for dynamically granted traits
            // that unlock recipes via requiresTrait (e.g., the tuning spear requires "tinkerer")
            UpdateExtraTraitStatic(player.Entity, "tinkerer", unlocked);
        }

        /// <summary>
        /// Check and apply Merciless unlock if thresholds are met.
        /// Requires 10% armor durability AND 15% melee damage.
        /// </summary>
        private static void CheckMercilessUnlock(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Check armor durability threshold
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            if (armorProgress.TotalDurabilityCredits < MercilessArmorDurabilityThreshold) return;

            // Check melee damage threshold
            var meleeProgress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
            if (meleeProgress.TotalCredits < MercilessMeleeDamageThreshold) return;

            // Both thresholds met - unlock Merciless!
            progress.IsUnlocked = true;
            pendingMercilessProgressSave = true;

            // Apply the trait
            ApplyMercilessBonusStatic(player, true);

            // Notify player
            NotifyLevelUp(player,
                Lang.Get("seraphleveling:message-merciless-unlock"));
        }

        /// <summary>
        /// Apply Merciless trait (unlocks shortsword/shield crafting).
        /// </summary>
        private static void ApplyMercilessBonusStatic(IServerPlayer player, bool unlocked)
        {
            player.Entity.WatchedAttributes.SetBool(WATCHED_MERCILESS_UNLOCKED, unlocked);
            UpdateExtraTraitStatic(player.Entity, MERCILESS_TRAIT_CODE, unlocked);

            // IMPORTANT: Add "merciless" to extraTraits to unlock shortsword/shield recipes
            // The game's recipe system checks extraTraits for dynamically granted traits
            // that unlock recipes via requiresTrait (e.g., shortsword/shield require "merciless")
            UpdateExtraTraitStatic(player.Entity, "merciless", unlocked);
        }

        /// <summary>
        /// Check and apply Bowyer unlock if thresholds are met.
        /// Requires 10% ranged damage AND 300 damage with simple bow/longbow.
        /// </summary>
        private static void CheckBowyerUnlock(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Check ranged damage threshold
            var rangedProgress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
            if (rangedProgress.TotalCredits < BowyerRangedDamageThreshold) return;

            // Check bow damage threshold
            if (progress.TotalBowDamage < BowyerBowDamageThreshold) return;

            // Both thresholds met - unlock Bowyer!
            progress.IsUnlocked = true;
            pendingBowyerProgressSave = true;

            // Apply the trait
            ApplyBowyerBonusStatic(player, true);

            // Notify player
            NotifyLevelUp(player,
                Lang.Get("seraphleveling:message-bowyer-unlock"));
        }

        /// <summary>
        /// Apply Bowyer trait (unlocks crude bow/arrows crafting).
        /// Also adds "bowyer" to extraTraits to unlock crude bow/arrows recipes.
        /// </summary>
        private static void ApplyBowyerBonusStatic(IServerPlayer player, bool unlocked)
        {
            player.Entity.WatchedAttributes.SetBool(WATCHED_BOWYER_UNLOCKED, unlocked);

            // Update extraTraits to show Bowyer trait if unlocked (for UI display)
            UpdateExtraTraitStatic(player.Entity, BOWYER_TRAIT_CODE, unlocked);

            // IMPORTANT: Add "bowyer" to extraTraits to unlock crude bow/arrows recipes
            // The game's recipe system checks extraTraits for dynamically granted traits
            // that unlock recipes via requiresTrait (e.g., crude bow/arrows require "bowyer")
            UpdateExtraTraitStatic(player.Entity, "bowyer", unlocked);
        }

        /// <summary>
        /// Check and apply Improviser unlock if threshold is met.
        /// Requires 300 damage with thrown rocks.
        /// </summary>
        private static void CheckImproviserUnlock(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Check rock damage threshold
            if (progress.TotalRockDamage < ImproviserRockDamageThreshold) return;

            // Threshold met - unlock Improviser!
            progress.IsUnlocked = true;
            pendingImproviserProgressSave = true;

            // Apply the trait
            ApplyImproviserBonusStatic(player, true);

            // Notify player
            NotifyLevelUp(player,
                Lang.Get("seraphleveling:message-improviser-unlock"));
        }

        /// <summary>
        /// Apply Improviser trait (unlocks sling crafting).
        /// Also adds "improviser" to extraTraits to unlock sling recipes.
        /// </summary>
        private static void ApplyImproviserBonusStatic(IServerPlayer player, bool unlocked)
        {
            player.Entity.WatchedAttributes.SetBool(WATCHED_IMPROVISER_UNLOCKED, unlocked);

            // Update extraTraits to show Improviser trait if unlocked (for UI display)
            UpdateExtraTraitStatic(player.Entity, IMPROVISER_TRAIT_CODE, unlocked);

            // IMPORTANT: Add "improviser" to extraTraits to unlock sling recipes
            // The game's recipe system checks extraTraits for dynamically granted traits
            // that unlock recipes via requiresTrait (e.g., sling requires "improviser")
            UpdateExtraTraitStatic(player.Entity, "improviser", unlocked);
        }

        /// <summary>
        /// Check and apply Claustrophobic removal if threshold is met (Hunter only).
        /// Requires 100% mining speed.
        /// </summary>
        private static void CheckClaustrophobicRemoval(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            if (!PlayerHasVanillaClaustrophobic(player.Entity)) return; // Ensure vanilla Claustrophobic is present

            string playerUid = player.PlayerUID;
            var progress = ClaustrophobicRemovalProgress.GetOrAdd(playerUid, _ => new ClaustrophobicRemovalProgressData());

            // Already removed
            if (progress.IsRemoved) return;

            // Check mining speed threshold
            var miningProgress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            if (miningProgress.TotalCredits < ClaustrophobicRemovalMiningThreshold) return;

            // Threshold met - remove Claustrophobic!
            progress.IsRemoved = true;
            pendingClaustrophobicRemovalProgressSave = true;

            // Apply the removal (negate the Claustrophobic penalties)
            ApplyClaustrophobicRemovalStatic(player, true);

            // Notify player
            NotifyLevelUp(player,
                Lang.Get("seraphleveling:message-claustrophobic-removed"));
        }

        /// <summary>
        /// Apply Claustrophobic removal (negates ore drop and mining speed penalties).
        /// </summary>
        private static void ApplyClaustrophobicRemovalStatic(IServerPlayer player, bool removed)
        {
            if (removed)
            {
                // Negate Claustrophobic penalties: -15% ore drop, -10% mining speed
                // By adding positive stats to counteract them
                // Note: Stats use WeightedSum with base 1.0. Vanilla uses -0.15/-0.10, so we use +0.15/+0.10 to cancel
                player.Entity.Stats.Set("oreDropRate", "sitClaustrophobicRemoval", 0.15f, false); // +15% to negate -15%
                player.Entity.Stats.Set("miningSpeedMul", "sitClaustrophobicRemoval", 0.10f, false); // +10% to negate -10%
            }
            else
            {
                player.Entity.Stats.Remove("oreDropRate", "sitClaustrophobicRemoval");
                player.Entity.Stats.Remove("miningSpeedMul", "sitClaustrophobicRemoval");
            }

            player.Entity.WatchedAttributes.SetBool(WATCHED_CLAUSTROPHOBIC_REMOVED, removed);
            UpdateExtraTraitStatic(player.Entity, CLAUSTROPHOBIC_REMOVED_TRAIT_CODE, removed);
        }

        /// <summary>
        /// Apply HeavyFooted removal (negatives Furtive and walk speed penalties)
        /// </summary>
        private static void ApplyHeavyFootedRemovalStatic(IServerPlayer player, bool removed)
        {
            if (removed)
            {
                // Negate HeavyFooted penalties: -15% walk speed, -15% furtive
                // By adding positive stats to counteract them
                player.Entity.Stats.Set("walkSpeed", "sitHeavyFootedRemoval", 0.1f, false); // +10% to negate -10%
                player.Entity.Stats.Set("animalSeekingRange", "sitHeavyFootedRemoval", -0.5f, false); // -50% to negate +50%.
            }
            else
            {
                player.Entity.Stats.Remove("walkSpeed", "sitHeavyFootedRemoval");
                player.Entity.Stats.Remove("animalSeekingRange", "sitHeavyFootedRemoval");
            }

            player.Entity.WatchedAttributes.SetBool(WATCHED_HEAVYFOOTED_REMOVED, removed);
            UpdateExtraTraitStatic(player.Entity, HEAVYFOOTED_REMOVED_TRAIT_CODE, removed);
        }

        /// <summary>
        /// Check if player is the Hunter class.
        /// </summary>
        private static bool PlayerIsHunterStatic(EntityPlayer entity)
        {
            if (entity == null) return false;

            var classTree = entity.WatchedAttributes.GetTreeAttribute("charClass");
            if (classTree != null)
            {
                string classCode = classTree.GetString("code", "").ToLowerInvariant();
                return classCode == "hunter";
            }

            return false;
        }

        // =========================================================================
        // MENDER TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Handler for /trait mender command.
        /// </summary>
        private TextCommandResult OnTraitMenderCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = MenderProgress.GetOrAdd(player.PlayerUID, _ => new MenderProgressData());
            int bonusPercent = CalculateMenderBonusPercent(progress.TotalCredits, player.Entity);
            bool hasVanillaMender = PlayerHasVanillaMenderStatic(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Mender progression: Level {progress.TotalCredits} / {MaxMenderPercent}");
            sb.AppendLine($"Current bonus: +{bonusPercent}% armor and clothing durability");
            if (hasVanillaMender)
            {
                sb.AppendLine($"Combined with Mender trait: +{VANILLA_MENDER_ARMOR_DURABILITY_BONUS + bonusPercent}% total");
            }
            if (progress.TotalCredits < MaxMenderPercent)
            {
                int remaining = progress.CurrentIncrementSize - progress.RepairsInIncrement;
                sb.AppendLine($"Progress: {progress.RepairsInIncrement} / {progress.CurrentIncrementSize} repairs until next level");
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait menderbase command.
        /// </summary>
        private TextCommandResult OnTraitMenderBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Base repairs must be at least 1.");
                BaseMenderRepairsPerIncrement = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Mender base repairs set to {BaseMenderRepairsPerIncrement}.");
            }

            return TextCommandResult.Success($"Current mender base repairs: {BaseMenderRepairsPerIncrement}.");
        }

        /// <summary>
        /// Handler for /trait menderlevel command.
        /// Gets or sets the player's mender level.
        /// </summary>
        private TextCommandResult OnTraitMenderLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = MenderProgress.GetOrAdd(player.PlayerUID, _ => new MenderProgressData());

            int? newLevel = (int?)args[0];

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                int currentBonus = CalculateMenderBonusPercent(progress.TotalCredits, player.Entity);
                return TextCommandResult.Success($"Current mender level: {progress.TotalCredits}/{MaxMenderPercent} (+{currentBonus}% durability)");
            }

            if (newLevel.Value < 0 || newLevel.Value > MaxMenderPercent)
                return TextCommandResult.Error($"Level must be between 0 and {MaxMenderPercent}.");

            progress.TotalCredits = newLevel.Value;
            progress.RepairsInIncrement = 0;
            progress.CurrentIncrementSize = BaseMenderRepairsPerIncrement;

            // Recalculate increment size for this level
            for (int i = 0; i < newLevel.Value; i++)
            {
                progress.CurrentIncrementSize += MenderIncrementStep;
            }

            pendingMenderProgressSave = true;

            int bonusPercent = ApplyMenderBonusStatic(player, progress.TotalCredits);

            UpdateSkillActivityDay(player.PlayerUID, "mender");

            return TextCommandResult.Success($"Mender level set to {newLevel.Value} (+{bonusPercent}% durability).");
        }

        /// <summary>
        /// Handler for /trait mendermax command.
        /// </summary>
        private TextCommandResult OnTraitMenderMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Max percent must be at least 1.");
                MaxMenderPercent = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Mender max bonus set to {MaxMenderPercent}%.");
            }

            return TextCommandResult.Success($"Current mender max bonus: {MaxMenderPercent}%.");
        }

        /// <summary>
        /// Calculate the mender durability bonus as an integer percentage.
        /// </summary>
        public static int CalculateMenderBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasVanillaMender = entity != null && PlayerHasVanillaMenderStatic(entity);
            int vanillaBonus = hasVanillaMender ? VANILLA_MENDER_ARMOR_DURABILITY_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxMenderPercent - vanillaBonus);
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Check if player has vanilla Mender trait.
        /// </summary>
        private static bool PlayerHasVanillaMenderStatic(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("mender", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            // Class fallback for Tailor (vanilla Mender) — keeps server-side Apply consistent
            // with client-side ClientHasVanillaTrait when characterTraits isn't populated.
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("tailor", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Apply mender bonus.
        /// </summary>
        private static int ApplyMenderBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            bool hasVanillaMender = PlayerHasVanillaMenderStatic(player.Entity);
            int bonusPercent = CalculateMenderBonusPercent(level, player.Entity);
            float bonus = bonusPercent * 0.01f;

            // Apply to armor durability loss stat (reduces durability damage taken).
            // Negative delta, same as vanilla Mender's -0.25. See ApplyArmorBonusesStatic.
            player.Entity.Stats.Set("armorDurabilityLoss", MENDER_STAT_CODE, -bonus, false);

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_MENDER_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_MENDER_BONUS, bonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaMender", hasVanillaMender);
            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_MENDER_LEVEL);

            // Update extraTraits
            UpdateExtraTraitStatic(player.Entity, MENDER_TRAIT_CODE, level > 0 && !hasVanillaMender);

            return bonusPercent;
        }

        /// <summary>
        /// Process a sewing kit repair (called externally or via Harmony patch).
        /// </summary>
        public static void ProcessMenderRepair(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            // Check if mender skill is disabled
            if (IsSkillDisabled("mender")) return;

            string playerUid = player.PlayerUID;
            var progress = MenderProgress.GetOrAdd(playerUid, _ => new MenderProgressData());

            // Skip if at max
            if (progress.TotalCredits >= MaxMenderPercent) return;

            int oldCredits = progress.TotalCredits;
            // Apply sleep buff multiplier if active
            int modifiedRepairs = ApplyXPMultiplier(playerUid, 1);
            progress.RepairsInIncrement += modifiedRepairs;

            // Check if we've earned a credit
            while (progress.RepairsInIncrement >= progress.CurrentIncrementSize && progress.TotalCredits < MaxMenderPercent)
            {
                progress.TotalCredits++;
                progress.RepairsInIncrement -= progress.CurrentIncrementSize;
                progress.CurrentIncrementSize += MenderIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned mender credit {progress.TotalCredits}");
            }

            pendingMenderProgressSave = true;

            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "mender");

            if (progress.TotalCredits > oldCredits)
            {
                ApplyMenderBonusStatic(player, progress.TotalCredits);
                // Notify player of level up with raw improvement (shows progress even when capped)
                NotifyLevelUp(player,
                    Lang.Get("seraphleveling:message-mender-level-up", progress.TotalCredits, progress.TotalCredits));
            }
        }

        // =========================================================================
        // PILFERER TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Handler for /trait pilferer command.
        /// </summary>
        private TextCommandResult OnTraitPilfererCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = PilfererProgress.GetOrAdd(player.PlayerUID, _ => new PilfererProgressData());
            int bonusPercent = CalculatePilfererBonusPercent(progress.TotalCredits, player.Entity);
            bool hasVanillaPilferer = PlayerHasVanillaPilfererStatic(player.Entity);
            int maxCredits = GetMaxPilfererCredits(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Pilferer progression: Level {progress.TotalCredits} / {maxCredits}");
            sb.AppendLine($"Current bonus: +{bonusPercent}% rusty gear, vessel contents, and collection chance");
            if (hasVanillaPilferer)
            {
                sb.AppendLine($"(Has vanilla Pilferer trait)");
            }
            if (progress.TotalCredits < maxCredits)
            {
                int remaining = progress.CurrentIncrementSize - progress.PointsInIncrement;
                sb.AppendLine($"Progress: {progress.PointsInIncrement} / {progress.CurrentIncrementSize} points until next level");
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait pilfererbase command.
        /// </summary>
        private TextCommandResult OnTraitPilfererBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Base points must be at least 1.");
                BasePilfererPointsPerIncrement = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Pilferer base points set to {BasePilfererPointsPerIncrement}.");
            }

            return TextCommandResult.Success($"Current pilferer base points: {BasePilfererPointsPerIncrement}.");
        }

        /// <summary>
        /// Handler for /trait pilfererlevel command.
        /// Gets or sets the player's pilferer level.
        /// </summary>
        private TextCommandResult OnTraitPilfererLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            // Get the player-specific max credits (accounts for Heavyhanded penalty)
            int maxCredits = GetMaxPilfererCredits(player.Entity);

            var progress = PilfererProgress.GetOrAdd(player.PlayerUID, _ => new PilfererProgressData());

            int? newLevel = (int?)args[0];

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                int currentBonus = CalculatePilfererBonusPercent(progress.TotalCredits, player.Entity);
                return TextCommandResult.Success($"Current pilferer level: {progress.TotalCredits}/{maxCredits} (+{currentBonus}% bonuses)");
            }

            if (newLevel.Value < 0 || newLevel.Value > maxCredits)
                return TextCommandResult.Error($"Level must be between 0 and {maxCredits}.");

            progress.TotalCredits = newLevel.Value;
            progress.PointsInIncrement = 0;
            progress.CurrentIncrementSize = BasePilfererPointsPerIncrement;

            for (int i = 0; i < newLevel.Value; i++)
            {
                progress.CurrentIncrementSize += PilfererIncrementStep;
            }

            pendingPilfererProgressSave = true;

            int bonusPercent = ApplyPilfererBonusStatic(player, progress.TotalCredits);

            UpdateSkillActivityDay(player.PlayerUID, "pilferer");

            return TextCommandResult.Success($"Pilferer level set to {newLevel.Value} (+{bonusPercent}% bonuses).");
        }

        /// <summary>
        /// Handler for /trait pilferermax command.
        /// </summary>
        private TextCommandResult OnTraitPilfererMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Max percent must be at least 1.");
                MaxPilfererPercent = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Pilferer max bonus set to {MaxPilfererPercent}%.");
            }

            return TextCommandResult.Success($"Current pilferer max bonus: {MaxPilfererPercent}%.");
        }

        /// <summary>
        /// Calculate the pilferer bonus as an integer percentage.
        /// </summary>
        public static int CalculatePilfererBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasVanillaPilferer = entity != null && PlayerHasVanillaPilfererStatic(entity);
            int vanillaBonus = hasVanillaPilferer ? VANILLA_PILFERER_RUSTY_GEAR_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxPilfererPercent - vanillaBonus);
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Get the maximum pilferer credits a player can earn based on their traits.
        /// Players with Heavyhanded trait can earn extra credits to compensate for the penalty.
        /// </summary>
        public static int GetMaxPilfererCredits(EntityPlayer entity)
        {
            if (entity == null) return MaxPilfererPercent;

            bool hasHeavyhanded = PlayerHasVanillaHeavyhanded(entity);

            // Heavyhanded vessel penalty is 10%, need 10 extra levels to cancel it
            if (hasHeavyhanded)
            {
                return MaxPilfererPercent + VANILLA_HEAVYHANDED_VESSEL_PENALTY;
            }

            return MaxPilfererPercent;
        }

        /// <summary>
        /// Check if player has vanilla Pilferer trait.
        /// </summary>
        private static bool PlayerHasVanillaPilfererStatic(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("pilferer", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("malefactor", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Apply pilferer bonus.
        /// Also handles Heavyhanded vessel loot negative trait cancellation.
        /// </summary>
        private static int ApplyPilfererBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return 0;

            bool hasVanillaPilferer = PlayerHasVanillaPilfererStatic(player.Entity);
            bool hasHeavyhanded = PlayerHasVanillaHeavyhanded(player.Entity);

            // Calculate remaining Heavyhanded vessel penalty
            int heavyhandedVesselRemaining = hasHeavyhanded ? CalculateRemainingPenalty(VANILLA_HEAVYHANDED_VESSEL_PENALTY, level) : 0;

            // Calculate net level after cancelling Heavyhanded's vessel penalty (only affects vessel stat)
            int netLevel = level;
            if (hasHeavyhanded)
            {
                netLevel = Math.Max(0, level - VANILLA_HEAVYHANDED_VESSEL_PENALTY);
            }

            // Per-stat earned bonuses. Pilferer's three stats have different vanilla values,
            // so we compute the earnable cap per stat (MaxPilfererPercent - vanilla_for_that_stat).
            // Earlier code used a single shared `bonusPercent` based on the rusty-gear cap,
            // which let Malefactor's vessel total exceed +20% (15 vanilla + 10 earned = 25)
            // and left rusty/whole below the cap. Splitting per-stat keeps every class at
            // exactly +20% per stat at maxall.
            int vanillaVessel = hasVanillaPilferer ? VANILLA_PILFERER_VESSEL_CONTENTS_BONUS : 0;
            int vanillaRusty = hasVanillaPilferer ? VANILLA_PILFERER_RUSTY_GEAR_BONUS : 0;
            int vanillaWhole = hasVanillaPilferer ? VANILLA_PILFERER_WHOLE_VESSEL_BONUS : 0;

            int earnableVessel = Math.Max(0, MaxPilfererPercent - vanillaVessel);
            int earnableRusty = Math.Max(0, MaxPilfererPercent - vanillaRusty);
            int earnableWhole = Math.Max(0, MaxPilfererPercent - vanillaWhole);

            int vesselBonus = Math.Min(netLevel, earnableVessel);
            int rustyBonus = Math.Min(level, earnableRusty);  // rusty/whole aren't affected by Heavyhanded
            int wholeBonus = Math.Min(level, earnableWhole);

            // Apply per-stat. These additive stats use values like 0.1 for +10%; the game
            // applies (1 + blended) as the final multiplier.
            player.Entity.Stats.Set("rustyGearDropRate", PILFERER_RUSTY_GEAR_STAT_CODE, rustyBonus * 0.01f, false);
            player.Entity.Stats.Set("vesselContentsDropRate", PILFERER_VESSEL_CONTENTS_STAT_CODE, vesselBonus * 0.01f, false);
            player.Entity.Stats.Set("wholeVesselLootChance", PILFERER_WHOLE_VESSEL_STAT_CODE, wholeBonus * 0.01f, false);

            // Counter-stat: when Heavyhanded vessel penalty is fully cancelled, apply +10% to
            // negate the vanilla -10% so functional vessel drop rate hits the displayed cap.
            if (hasHeavyhanded)
            {
                if (heavyhandedVesselRemaining == 0)
                    player.Entity.Stats.Set("vesselContentsDropRate", "sitHeavyhandedVesselCancel", VANILLA_HEAVYHANDED_VESSEL_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("vesselContentsDropRate", "sitHeavyhandedVesselCancel");
            }

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_PILFERER_LEVEL, level);
            // Keep WATCHED_PILFERER_BONUS as the max of the three for any legacy readers
            // (display now uses the per-stat values instead).
            player.Entity.WatchedAttributes.SetInt(WATCHED_PILFERER_BONUS, Math.Max(vesselBonus, Math.Max(rustyBonus, wholeBonus)));
            player.Entity.WatchedAttributes.SetInt(WATCHED_PILFERER_VESSEL_BONUS, vesselBonus);
            player.Entity.WatchedAttributes.SetInt(WATCHED_PILFERER_RUSTY_BONUS, rustyBonus);
            player.Entity.WatchedAttributes.SetInt(WATCHED_PILFERER_WHOLE_BONUS, wholeBonus);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaPilferer", hasVanillaPilferer);

            // Sync negative trait status (Heavyhanded vessel part)
            player.Entity.WatchedAttributes.SetInt(WATCHED_HEAVYHANDED_VESSEL_REMAINING, heavyhandedVesselRemaining);

            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_PILFERER_LEVEL);

            // Update extraTraits
            UpdateExtraTraitStatic(player.Entity, PILFERER_TRAIT_CODE, level > 0 && !hasVanillaPilferer);

            // Return the largest earned bonus (caller is informational; all three may differ)
            return Math.Max(vesselBonus, Math.Max(rustyBonus, wholeBonus));
        }

        /// <summary>
        /// Process cracked vessel break (called from OnBlockBroken for cracked vessels).
        /// Only cracked vessels count - they can't be re-placed by players.
        /// </summary>
        public static void ProcessVesselBreak(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = PilfererProgress.GetOrAdd(playerUid, _ => new PilfererProgressData());

            // Get the player-specific max credits (accounts for Heavyhanded penalty)
            int maxCredits = GetMaxPilfererCredits(player.Entity);

            if (progress.TotalCredits >= maxCredits) return;

            int oldCredits = progress.TotalCredits;
            // Apply sleep buff multiplier if active
            int modifiedPoints = ApplyXPMultiplier(playerUid, PILFERER_VESSEL_POINTS);
            progress.PointsInIncrement += modifiedPoints;

            while (progress.PointsInIncrement >= progress.CurrentIncrementSize && progress.TotalCredits < maxCredits)
            {
                progress.TotalCredits++;
                progress.PointsInIncrement -= progress.CurrentIncrementSize;
                progress.CurrentIncrementSize += PilfererIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned pilferer credit {progress.TotalCredits} from cracked vessel");
            }

            pendingPilfererProgressSave = true;

            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "pilferer");

            if (progress.TotalCredits > oldCredits)
            {
                ApplyPilfererBonusStatic(player, progress.TotalCredits);
                // Notify player of level up with raw improvement (shows progress even when cancelling Heavyhanded)
                NotifyLevelUp(player,
                    Lang.Get("seraphleveling:message-pilferer-level-up", progress.TotalCredits, progress.TotalCredits));
            }
        }


        // =========================================================================
        // RESOURCEFUL TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Handler for /trait resourceful command.
        /// </summary>
        private TextCommandResult OnTraitResourcefulCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = ResourcefulProgress.GetOrAdd(player.PlayerUID, _ => new ResourcefulProgressData());
            int lootBonus = CalculateResourcefulLootBonusPercent(progress.TotalCredits, player.Entity);
            int speedBonus = CalculateResourcefulSpeedBonusPercent(progress.TotalCredits, player.Entity);
            bool hasVanillaResourceful = PlayerHasVanillaResourcefulStatic(player.Entity);
            int maxCredits = GetMaxResourcefulCredits(player.Entity);

            var sb = new StringBuilder();
            sb.AppendLine($"Resourceful progression: Level {progress.TotalCredits} / {maxCredits}");
            sb.AppendLine($"Current bonus: +{lootBonus}% animal loot, +{speedBonus}% harvesting speed");
            if (hasVanillaResourceful)
            {
                sb.AppendLine($"(Has vanilla Resourceful trait)");
            }
            if (progress.TotalCredits < maxCredits)
            {
                int remaining = progress.CurrentIncrementSize - progress.AnimalsInIncrement;
                sb.AppendLine($"Progress: {progress.AnimalsInIncrement} / {progress.CurrentIncrementSize} animals until next level");
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait resourcefulbase command.
        /// </summary>
        private TextCommandResult OnTraitResourcefulBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Base animals must be at least 1.");
                BaseResourcefulAnimalsPerIncrement = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Resourceful base animals set to {BaseResourcefulAnimalsPerIncrement}.");
            }

            return TextCommandResult.Success($"Current resourceful base animals: {BaseResourcefulAnimalsPerIncrement}.");
        }

        /// <summary>
        /// Handler for /trait resourcefullevel command.
        /// Gets or sets the player's resourceful level.
        /// </summary>
        private TextCommandResult OnTraitResourcefulLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            // Get the player-specific max credits (accounts for Kind penalty)
            int maxCredits = GetMaxResourcefulCredits(player.Entity);

            var progress = ResourcefulProgress.GetOrAdd(player.PlayerUID, _ => new ResourcefulProgressData());

            int? newLevel = (int?)args[0];

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                int lootBonus = CalculateResourcefulLootBonusPercent(progress.TotalCredits, player.Entity);
                return TextCommandResult.Success($"Current resourceful level: {progress.TotalCredits}/{maxCredits} (+{lootBonus}% loot)");
            }

            if (newLevel.Value < 0 || newLevel.Value > maxCredits)
                return TextCommandResult.Error($"Level must be between 0 and {maxCredits}.");

            progress.TotalCredits = newLevel.Value;
            progress.AnimalsInIncrement = 0;
            progress.CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement;

            for (int i = 0; i < newLevel.Value; i++)
            {
                progress.CurrentIncrementSize += ResourcefulIncrementStep;
            }

            pendingResourcefulProgressSave = true;

            ApplyResourcefulBonusStatic(player, progress.TotalCredits);
            int newLootBonus = CalculateResourcefulLootBonusPercent(progress.TotalCredits, player.Entity);

            UpdateSkillActivityDay(player.PlayerUID, "resourceful");

            return TextCommandResult.Success($"Resourceful level set to {newLevel.Value} (+{newLootBonus}% loot).");
        }

        /// <summary>
        /// Handler for /trait resourcefulmax command.
        /// </summary>
        private TextCommandResult OnTraitResourcefulMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Max percent must be at least 1.");
                MaxResourcefulLootPercent = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Resourceful max loot bonus set to {MaxResourcefulLootPercent}%.");
            }

            return TextCommandResult.Success($"Current resourceful max loot bonus: {MaxResourcefulLootPercent}%.");
        }

        /// <summary>
        /// Calculate the resourceful loot bonus as an integer percentage.
        /// </summary>
        public static int CalculateResourcefulLootBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasVanillaResourceful = entity != null && PlayerHasVanillaResourcefulStatic(entity);
            int vanillaBonus = hasVanillaResourceful ? VANILLA_RESOURCEFUL_LOOT_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxResourcefulLootPercent - vanillaBonus);
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Calculate the resourceful speed bonus as an integer percentage.
        /// Speed bonus scales indefinitely with level (1% per credit), no cap.
        /// </summary>
        public static int CalculateResourcefulSpeedBonusPercent(int credits, EntityPlayer entity)
        {
            // Speed bonus scales indefinitely - 1% per credit, no cap
            return credits;
        }

        /// <summary>
        /// Get the maximum resourceful credits a player can earn based on their traits.
        /// Players with Kind trait can earn extra credits to compensate for the penalty.
        /// </summary>
        public static int GetMaxResourcefulCredits(EntityPlayer entity)
        {
            // Resourceful covers two stats with different caps (loot 20%, speed 25%).
            // The credit count must be high enough to fill BOTH stats to their cap, so:
            //   base = max(MaxLoot, MaxSpeed)
            //   Kind classes need an extra buffer of max(KindLootPenalty, KindSpeedPenalty)
            //     so the larger penalty is fully cancelled before earnable bonus starts.
            // Earlier versions used `MaxLoot + KindSpeedPenalty` (20 + 25 = 45) which was
            // too low — Tailor would land at +20% speed instead of the +25% cap.
            int baseMax = Math.Max(MaxResourcefulLootPercent, MaxResourcefulSpeedPercent);
            if (entity == null) return baseMax;

            bool hasKind = PlayerHasVanillaKind(entity);
            if (hasKind)
            {
                int largestKindPenalty = Math.Max(VANILLA_KIND_LOOT_PENALTY, VANILLA_KIND_SPEED_PENALTY);
                return baseMax + largestKindPenalty;
            }

            return baseMax;
        }

        /// <summary>
        /// Check if player has vanilla Resourceful trait.
        /// </summary>
        private static bool PlayerHasVanillaResourcefulStatic(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("resourceful", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            // Fallback to characterClass — keeps server-side Apply consistent with the
            // client-side ClientHasVanillaTrait check (otherwise Apply computes a higher
            // earnable cap when characterTraits hasn't been populated, then the postfix
            // adds the vanilla bonus on top and the displayed value exceeds the actual cap).
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("hunter", StringComparison.OrdinalIgnoreCase) ||
                   characterClass.Equals("malefactor", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Apply resourceful bonus.
        /// Also handles Kind negative trait cancellation.
        /// </summary>
        private static void ApplyResourcefulBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return;

            // If resourceful skill is disabled, set bonus to 0 and return
            if (IsSkillDisabled("resourceful"))
            {
                player.Entity.Stats.Set("animalLootDropRate", RESOURCEFUL_LOOT_STAT_CODE, 0f, false);
                player.Entity.Stats.Set("animalHarvestingTime", RESOURCEFUL_SPEED_STAT_CODE, 0f, false);
                return;
            }

            bool hasVanillaResourceful = PlayerHasVanillaResourcefulStatic(player.Entity);
            bool hasKind = PlayerHasVanillaKind(player.Entity);

            // Calculate remaining Kind penalties
            int kindLootRemaining = hasKind ? CalculateRemainingPenalty(VANILLA_KIND_LOOT_PENALTY, level) : 0;
            int kindSpeedRemaining = hasKind ? CalculateRemainingPenalty(VANILLA_KIND_SPEED_PENALTY, level) : 0;

            // Calculate net bonus after cancelling negative traits
            int netLootLevel = level;
            int netSpeedLevel = level;

            if (hasKind)
            {
                // Kind penalties are cancelled first, then bonuses start
                netLootLevel = Math.Max(0, level - VANILLA_KIND_LOOT_PENALTY);
                netSpeedLevel = Math.Max(0, level - VANILLA_KIND_SPEED_PENALTY);
            }

            // Apply vanilla caps if player has Resourceful trait
            int vanillaLootBonus = hasVanillaResourceful ? VANILLA_RESOURCEFUL_LOOT_BONUS : 0;
            int vanillaSpeedBonus = hasVanillaResourceful ? VANILLA_RESOURCEFUL_SPEED_BONUS : 0;

            int maxEarnableLoot = Math.Max(0, MaxResourcefulLootPercent - vanillaLootBonus);
            int maxEarnableSpeed = Math.Max(0, MaxResourcefulSpeedPercent - vanillaSpeedBonus);

            int lootBonusPercent = Math.Min(netLootLevel, maxEarnableLoot);
            int speedBonusPercent = Math.Min(netSpeedLevel, maxEarnableSpeed);

            float lootBonus = lootBonusPercent * 0.01f;
            float speedBonus = speedBonusPercent * 0.01f;

            // Apply to resourceful-related stats
            // Note: Stats use WeightedSum blending with a base of 1.0. Vanilla traits set values
            // like 0.1 for +10%. We set just the bonus value, not 1 + bonus.
            player.Entity.Stats.Set("animalLootDropRate", RESOURCEFUL_LOOT_STAT_CODE, lootBonus, false);
            // The stat is animalHarvestingTime, which multiplies the harvest duration, so a
            // faster harvest is a negative value. Vanilla Resourceful stores -0.25 for it.
            // There is no stat called harvestingSpeedMul, so the old code went nowhere.
            player.Entity.Stats.Set("animalHarvestingTime", RESOURCEFUL_SPEED_STAT_CODE, -speedBonus, false);

            // Counter-stats for Kind (Tailor): when each penalty is fully cancelled, apply a
            // counter on the same stat vanilla uses so functional matches displayed cap.
            // Kind sets animalLootDropRate -0.10 and animalHarvestingTime +0.25.
            if (hasKind)
            {
                if (kindLootRemaining == 0)
                    player.Entity.Stats.Set("animalLootDropRate", "sitKindLootCancel", VANILLA_KIND_LOOT_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("animalLootDropRate", "sitKindLootCancel");

                if (kindSpeedRemaining == 0)
                    // Kind uses animalHarvestingTime where positive = slower; cancel with negative.
                    player.Entity.Stats.Set("animalHarvestingTime", "sitKindSpeedCancel", -VANILLA_KIND_SPEED_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("animalHarvestingTime", "sitKindSpeedCancel");
            }

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_RESOURCEFUL_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_RESOURCEFUL_LOOT_BONUS, lootBonusPercent);
            player.Entity.WatchedAttributes.SetInt(WATCHED_RESOURCEFUL_SPEED_BONUS, speedBonusPercent);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaResourceful", hasVanillaResourceful);

            // Sync negative trait status
            player.Entity.WatchedAttributes.SetBool("sitHasKind", hasKind);
            player.Entity.WatchedAttributes.SetInt(WATCHED_KIND_LOOT_REMAINING, kindLootRemaining);
            player.Entity.WatchedAttributes.SetInt(WATCHED_KIND_SPEED_REMAINING, kindSpeedRemaining);

            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_RESOURCEFUL_LEVEL);

            // Update extraTraits
            UpdateExtraTraitStatic(player.Entity, RESOURCEFUL_TRAIT_CODE, level > 0 && !hasVanillaResourceful);
        }

        /// <summary>
        /// Process animal harvested (called from Harmony patch when player harvests an animal).
        /// </summary>
        public static void ProcessAnimalHarvested(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            // Check if resourceful skill is disabled
            if (IsSkillDisabled("resourceful")) return;

            string playerUid = player.PlayerUID;
            var progress = ResourcefulProgress.GetOrAdd(playerUid, _ => new ResourcefulProgressData());

            // Get the player-specific max credits (accounts for Kind penalty)
            int maxCredits = GetMaxResourcefulCredits(player.Entity);

            if (progress.TotalCredits >= maxCredits) return;

            int oldCredits = progress.TotalCredits;
            // Apply sleep buff multiplier if active
            int modifiedAnimals = ApplyXPMultiplier(playerUid, 1);
            progress.AnimalsInIncrement += modifiedAnimals;

            while (progress.AnimalsInIncrement >= progress.CurrentIncrementSize && progress.TotalCredits < maxCredits)
            {
                progress.TotalCredits++;
                progress.AnimalsInIncrement -= progress.CurrentIncrementSize;
                progress.CurrentIncrementSize += ResourcefulIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned resourceful credit {progress.TotalCredits}");
            }

            pendingResourcefulProgressSave = true;

            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "resourceful");

            if (progress.TotalCredits > oldCredits)
            {
                ApplyResourcefulBonusStatic(player, progress.TotalCredits);
                // Notify player of level up with raw improvement (shows progress even when cancelling Kind)
                NotifyLevelUp(player,
                    Lang.Get("seraphleveling:message-resourceful-level-up", progress.TotalCredits, progress.TotalCredits));
            }
        }

        // =========================================================================
        // FORAGER TRAIT IMPLEMENTATION
        // =========================================================================

        /// <summary>
        /// Handler for /trait forager command.
        /// </summary>
        private TextCommandResult OnTraitForagerCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            var progress = ForagerProgress.GetOrAdd(player.PlayerUID, _ => new ForagerProgressData());
            bool hasVanillaForager = PlayerHasVanillaForagerStatic(player.Entity);
            bool hasCivil = PlayerHasVanillaCivil(player.Entity);
            bool hasHeavyhanded = PlayerHasVanillaHeavyhanded(player.Entity);
            int maxCredits = GetMaxForagerCredits(player.Entity);

            // Use net bonuses from WatchedAttributes (set by ApplyForagerBonusStatic)
            int netLootBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_LOOT_BONUS, 0);
            int netWildCropBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_WILD_CROP_BONUS, 0);

            var sb = new StringBuilder();
            sb.AppendLine($"Forager progression: Level {progress.TotalCredits} / {maxCredits}");
            sb.AppendLine($"Current bonus: +{netLootBonus}% foraging loot, +{netWildCropBonus}% wild crop drops");
            if (hasVanillaForager)
            {
                sb.AppendLine($"(Has vanilla Forager trait)");
            }
            if (hasCivil)
            {
                int civilRemaining = player.Entity.WatchedAttributes.GetInt(WATCHED_CIVIL_REMAINING, 0);
                if (civilRemaining > 0)
                    sb.AppendLine($"(Civil penalty remaining: -{civilRemaining}% foraging loot)");
                else
                    sb.AppendLine("(Civil penalty cancelled!)");
            }
            if (hasHeavyhanded)
            {
                int forageRemaining = player.Entity.WatchedAttributes.GetInt(WATCHED_HEAVYHANDED_FORAGING_REMAINING, 0);
                int wildCropRemaining = player.Entity.WatchedAttributes.GetInt(WATCHED_HEAVYHANDED_WILD_CROP_REMAINING, 0);
                if (forageRemaining > 0 || wildCropRemaining > 0)
                    sb.AppendLine($"(Heavyhanded penalties remaining: -{forageRemaining}% foraging, -{wildCropRemaining}% wild crop)");
                else
                    sb.AppendLine("(Heavyhanded penalties cancelled!)");
            }
            if (progress.TotalCredits < maxCredits)
            {
                sb.AppendLine($"Progress: {progress.CropsInIncrement} / {progress.CurrentIncrementSize} crops until next level");
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait foragerbase command.
        /// </summary>
        private TextCommandResult OnTraitForagerBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Base crops must be at least 1.");
                BaseForagerCropsPerIncrement = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Forager base crops set to {BaseForagerCropsPerIncrement}.");
            }

            return TextCommandResult.Success($"Current forager base crops: {BaseForagerCropsPerIncrement}.");
        }

        /// <summary>
        /// Handler for /trait foragerlevel command.
        /// Gets or sets the player's forager level.
        /// </summary>
        private TextCommandResult OnTraitForagerLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            // Get the player-specific max credits (accounts for Civil/Heavyhanded penalties)
            int maxCredits = GetMaxForagerCredits(player.Entity);

            var progress = ForagerProgress.GetOrAdd(player.PlayerUID, _ => new ForagerProgressData());

            int? newLevel = (int?)args[0];

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                int netLootBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_LOOT_BONUS, 0);
                int netWildCropBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_WILD_CROP_BONUS, 0);
                return TextCommandResult.Success($"Current forager level: {progress.TotalCredits}/{maxCredits} (+{netLootBonus}% loot, +{netWildCropBonus}% wild crop)");
            }

            if (newLevel.Value < 0 || newLevel.Value > maxCredits)
                return TextCommandResult.Error($"Level must be between 0 and {maxCredits}.");

            progress.TotalCredits = newLevel.Value;
            progress.CropsInIncrement = 0;
            progress.CurrentIncrementSize = BaseForagerCropsPerIncrement;

            for (int i = 0; i < newLevel.Value; i++)
            {
                progress.CurrentIncrementSize += ForagerIncrementStep;
            }

            pendingForagerProgressSave = true;

            ApplyForagerBonusStatic(player, progress.TotalCredits);
            // Use net bonuses from WatchedAttributes which accounts for Civil/Heavyhanded penalties
            int newNetLootBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_LOOT_BONUS, 0);
            int newNetWildCropBonus = player.Entity.WatchedAttributes.GetInt(WATCHED_FORAGER_WILD_CROP_BONUS, 0);

            UpdateSkillActivityDay(player.PlayerUID, "forager");

            return TextCommandResult.Success($"Forager level set to {newLevel.Value} (+{newNetLootBonus}% loot, +{newNetWildCropBonus}% wild crop).");
        }

        /// <summary>
        /// Handler for /trait foragermax command.
        /// </summary>
        private TextCommandResult OnTraitForagerMaxCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error("Max percent must be at least 1.");
                MaxForagerLootPercent = newValue.Value;
                pendingConfigSave = true;
                return TextCommandResult.Success($"Forager max loot bonus set to {MaxForagerLootPercent}%.");
            }

            return TextCommandResult.Success($"Current forager max loot bonus: {MaxForagerLootPercent}%.");
        }

        /// <summary>
        /// Calculate the forager loot bonus as an integer percentage.
        /// </summary>
        public static int CalculateForagerLootBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasVanillaForager = entity != null && PlayerHasVanillaForagerStatic(entity);
            int vanillaBonus = hasVanillaForager ? VANILLA_FORAGER_LOOT_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxForagerLootPercent - vanillaBonus);
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Calculate the forager wild crop bonus as an integer percentage.
        /// </summary>
        public static int CalculateForagerWildCropBonusPercent(int credits, EntityPlayer entity)
        {
            bool hasVanillaForager = entity != null && PlayerHasVanillaForagerStatic(entity);
            int vanillaBonus = hasVanillaForager ? VANILLA_FORAGER_WILD_CROP_BONUS : 0;
            int earnableBonus = Math.Max(0, MaxForagerWildCropPercent - vanillaBonus);
            return Math.Min(credits, earnableBonus);
        }

        /// <summary>
        /// Check if player has vanilla Forager trait.
        /// </summary>
        private static bool PlayerHasVanillaForagerStatic(EntityPlayer entity)
        {
            if (entity == null) return false;
            string[] classTraits = entity.WatchedAttributes.GetStringArray("characterTraits", null);
            if (classTraits != null)
            {
                foreach (string trait in classTraits)
                {
                    if (trait.Equals("forager", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            string characterClass = entity.WatchedAttributes.GetString("characterClass", "");
            return characterClass.Equals("hunter", StringComparison.OrdinalIgnoreCase) ||
                   characterClass.Equals("malefactor", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get the maximum forager credits a player can earn based on their traits.
        /// Players with negative traits (Civil, Heavyhanded) can earn extra credits
        /// to compensate for the penalty before gaining positive bonuses.
        /// </summary>
        public static int GetMaxForagerCredits(EntityPlayer entity)
        {
            if (entity == null) return MaxForagerLootPercent;

            bool hasCivil = PlayerHasVanillaCivil(entity);
            bool hasHeavyhanded = PlayerHasVanillaHeavyhanded(entity);

            // Civil penalty is 10% foraging loot, need 10 extra levels to cancel it
            if (hasCivil)
            {
                return MaxForagerLootPercent + VANILLA_CIVIL_FORAGING_PENALTY;
            }

            // Heavyhanded has two penalties - use the larger one (wild crop = 20%)
            if (hasHeavyhanded)
            {
                return MaxForagerWildCropPercent + VANILLA_HEAVYHANDED_WILD_CROP_PENALTY;
            }

            return MaxForagerLootPercent;
        }

        /// <summary>
        /// Apply forager bonus.
        /// Also handles Civil and Heavyhanded negative trait cancellation.
        /// </summary>
        private static void ApplyForagerBonusStatic(IServerPlayer player, int level)
        {
            if (player?.Entity == null) return;

            bool hasVanillaForager = PlayerHasVanillaForagerStatic(player.Entity);
            bool hasCivil = PlayerHasVanillaCivil(player.Entity);
            bool hasHeavyhanded = PlayerHasVanillaHeavyhanded(player.Entity);

            int lootBonusPercent = CalculateForagerLootBonusPercent(level, player.Entity);
            int wildCropBonusPercent = CalculateForagerWildCropBonusPercent(level, player.Entity);

            // Calculate remaining negative trait penalties
            int civilRemaining = hasCivil ? CalculateRemainingPenalty(VANILLA_CIVIL_FORAGING_PENALTY, level) : 0;
            int heavyhandedForagingRemaining = hasHeavyhanded ? CalculateRemainingPenalty(VANILLA_HEAVYHANDED_FORAGING_PENALTY, level) : 0;
            int heavyhandedWildCropRemaining = hasHeavyhanded ? CalculateRemainingPenalty(VANILLA_HEAVYHANDED_WILD_CROP_PENALTY, level) : 0;

            // Calculate net bonus (earned bonus - remaining penalty)
            // For Civil: need to earn level > 10 to start gaining bonus
            // For Heavyhanded: need to earn level > 15 for foraging, > 20 for wild crop
            int netLootBonus = lootBonusPercent;
            int netWildCropBonus = wildCropBonusPercent;

            if (hasCivil)
            {
                // Civil penalty is cancelled first, then bonus starts
                netLootBonus = Math.Max(0, level - VANILLA_CIVIL_FORAGING_PENALTY);
                if (!hasVanillaForager)
                {
                    netLootBonus = Math.Min(netLootBonus, MaxForagerLootPercent);
                }
            }

            if (hasHeavyhanded)
            {
                // Heavyhanded penalties are cancelled first
                netLootBonus = Math.Max(0, level - VANILLA_HEAVYHANDED_FORAGING_PENALTY);
                netWildCropBonus = Math.Max(0, level - VANILLA_HEAVYHANDED_WILD_CROP_PENALTY);
                if (!hasVanillaForager)
                {
                    netLootBonus = Math.Min(netLootBonus, MaxForagerLootPercent);
                    netWildCropBonus = Math.Min(netWildCropBonus, MaxForagerWildCropPercent);
                }
            }

            float lootBonus = netLootBonus * 0.01f;
            float wildCropBonus = netWildCropBonus * 0.01f;

            // Apply to forager-related stats
            // Note: forageDropRate/wildCropDropRate are additive stats where vanilla traits use
            // values like 0.1 for +10%. The game applies (1 + blended) as the multiplier.
            // Using just the bonus value (not 1 + bonus) to avoid doubling.
            player.Entity.Stats.Set("forageDropRate", FORAGER_LOOT_STAT_CODE, lootBonus, false);
            player.Entity.Stats.Set("wildCropDropRate", FORAGER_WILD_CROP_STAT_CODE, wildCropBonus, false);

            // Counter-stats: when negative trait penalties are fully cancelled, apply +penalty
            // counters so functional foraging stats match the displayed cap. Civil affects loot
            // (Tailor); Heavyhanded affects loot AND wild crop (Blackguard).
            if (hasCivil)
            {
                if (civilRemaining == 0)
                    player.Entity.Stats.Set("forageDropRate", "sitCivilForagingCancel", VANILLA_CIVIL_FORAGING_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("forageDropRate", "sitCivilForagingCancel");
            }
            if (hasHeavyhanded)
            {
                if (heavyhandedForagingRemaining == 0)
                    player.Entity.Stats.Set("forageDropRate", "sitHeavyhandedForagingCancel", VANILLA_HEAVYHANDED_FORAGING_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("forageDropRate", "sitHeavyhandedForagingCancel");

                if (heavyhandedWildCropRemaining == 0)
                    player.Entity.Stats.Set("wildCropDropRate", "sitHeavyhandedWildCropCancel", VANILLA_HEAVYHANDED_WILD_CROP_PENALTY * 0.01f, false);
                else
                    player.Entity.Stats.Remove("wildCropDropRate", "sitHeavyhandedWildCropCancel");
            }

            // Sync to WatchedAttributes
            player.Entity.WatchedAttributes.SetInt(WATCHED_FORAGER_LEVEL, level);
            player.Entity.WatchedAttributes.SetInt(WATCHED_FORAGER_LOOT_BONUS, netLootBonus);
            player.Entity.WatchedAttributes.SetInt(WATCHED_FORAGER_WILD_CROP_BONUS, netWildCropBonus);
            player.Entity.WatchedAttributes.SetBool("sitHasVanillaForager", hasVanillaForager);

            // Sync negative trait status
            player.Entity.WatchedAttributes.SetBool("sitHasCivil", hasCivil);
            player.Entity.WatchedAttributes.SetInt(WATCHED_CIVIL_REMAINING, civilRemaining);
            player.Entity.WatchedAttributes.SetBool("sitHasHeavyhanded", hasHeavyhanded);
            player.Entity.WatchedAttributes.SetInt(WATCHED_HEAVYHANDED_FORAGING_REMAINING, heavyhandedForagingRemaining);
            player.Entity.WatchedAttributes.SetInt(WATCHED_HEAVYHANDED_WILD_CROP_REMAINING, heavyhandedWildCropRemaining);

            player.Entity.WatchedAttributes.MarkPathDirty(WATCHED_FORAGER_LEVEL);

            // Update extraTraits
            UpdateExtraTraitStatic(player.Entity, FORAGER_TRAIT_CODE, level > 0 && !hasVanillaForager);
        }

        /// <summary>
        /// Process wild crop broken (for Forager progression).
        /// </summary>
        public static void ProcessWildCropBroken(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = ForagerProgress.GetOrAdd(playerUid, _ => new ForagerProgressData());

            // Get the player-specific max credits (accounts for Civil/Heavyhanded penalties)
            int maxCredits = GetMaxForagerCredits(player.Entity);

            if (progress.TotalCredits >= maxCredits) return;

            int oldCredits = progress.TotalCredits;
            // Apply sleep buff multiplier if active
            int modifiedCrops = ApplyXPMultiplier(playerUid, 1);
            progress.CropsInIncrement += modifiedCrops;

            while (progress.CropsInIncrement >= progress.CurrentIncrementSize && progress.TotalCredits < maxCredits)
            {
                progress.TotalCredits++;
                progress.CropsInIncrement -= progress.CurrentIncrementSize;
                progress.CurrentIncrementSize += ForagerIncrementStep;

                ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned forager credit {progress.TotalCredits}");
            }

            pendingForagerProgressSave = true;

            // Update last activity day for skill decay
            UpdateSkillActivityDay(playerUid, "forager");

            if (progress.TotalCredits > oldCredits)
            {
                ApplyForagerBonusStatic(player, progress.TotalCredits);
                // Notify player of level up with raw improvement (shows progress even when cancelling Civil/Heavyhanded)
                NotifyLevelUp(player,
                    Lang.Get("seraphleveling:message-forager-level-up", progress.TotalCredits, progress.TotalCredits, progress.TotalCredits));
            }
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

        // =========================================================================
        // NEW TRAIT COMMAND HANDLERS
        // =========================================================================

        /// <summary>
        /// Handler for /trait furtive command.
        /// </summary>
        private TextCommandResult OnTraitFurtiveCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData());

            bool hasVanillaFurtive = PlayerHasVanillaFurtiveStatic(player.Entity);
            int bonusPercent = hasVanillaFurtive ? 0 : progress.TotalCredits;

            var sb = new StringBuilder();
            sb.AppendLine($"Furtive progression: Level {progress.TotalCredits} / {MaxFurtivePercent}");
            sb.AppendLine($"Current bonus: -{bonusPercent}% animal detection range");
            if (hasVanillaFurtive)
            {
                sb.AppendLine($"Vanilla Furtive trait active: -{VANILLA_FURTIVE_DETECTION_REDUCTION}% detection (max reached)");
            }
            else if (progress.TotalCredits < MaxFurtivePercent)
            {
                float remaining = progress.CurrentIncrementSize - progress.BlocksInIncrement;
                sb.AppendLine($"Progress: {progress.BlocksInIncrement:F1} / {progress.CurrentIncrementSize} blocks sneaked");
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait furtivelevel command.
        /// Gets or sets the player's furtive level.
        /// </summary>
        private TextCommandResult OnTraitFurtiveLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData());

            int? newLevel = (int?)args[0];

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                bool hasVanillaFurtive = PlayerHasVanillaFurtiveStatic(player.Entity);
                int currentBonus = hasVanillaFurtive ? VANILLA_FURTIVE_DETECTION_REDUCTION : progress.TotalCredits;
                return TextCommandResult.Success($"Current furtive level: {progress.TotalCredits}/{MaxFurtivePercent} (-{currentBonus}% detection)");
            }

            if (newLevel.Value < 0 || newLevel.Value > MaxFurtivePercent)
                return TextCommandResult.Error($"Level must be between 0 and {MaxFurtivePercent}.");

            progress.TotalCredits = newLevel.Value;
            progress.BlocksInIncrement = 0;
            progress.CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement + (newLevel.Value * FurtiveIncrementStep);

            pendingFurtiveProgressSave = true;
            int bonusPercent = ApplyFurtiveBonusStatic(player, newLevel.Value);

            UpdateSkillActivityDay(player.PlayerUID, "furtive");

            return TextCommandResult.Success($"Furtive level set to {newLevel.Value} (-{bonusPercent}% detection).");
        }

        /// <summary>
        /// Handler for /trait precise command.
        /// </summary>
        private TextCommandResult OnTraitPreciseCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());

            bool hasVanillaPrecise = PlayerHasVanillaPreciseStatic(player.Entity);
            int effectiveMax = GetPreciseEffectiveMax(player.Entity);
            int bonusPercent = Math.Min(progress.TotalCredits, effectiveMax);

            var sb = new StringBuilder();
            sb.AppendLine($"Precise progression: Level {progress.TotalCredits} / {effectiveMax}");
            sb.AppendLine($"Current bonus: +{bonusPercent}% damage to mechanicals");
            if (hasVanillaPrecise)
            {
                int totalBonus = VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS + bonusPercent;
                sb.AppendLine($"Combined with Clockmaker trait: +{totalBonus}% total");
            }
            if (progress.TotalCredits < effectiveMax)
            {
                sb.AppendLine($"Per-weapon progress:");
                foreach (var kvp in progress.WeaponProgress)
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value.DamageInIncrement:F0} / {kvp.Value.CurrentIncrementSize} damage");
                }
            }
            else
            {
                sb.AppendLine("Maximum level reached!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait preciselevel command.
        /// Gets or sets the player's precise level.
        /// </summary>
        private TextCommandResult OnTraitPreciseLevelCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            int? newLevel = (int?)args[0];

            // If no value provided, show current level
            if (!newLevel.HasValue)
            {
                var progress = PreciseProgress.GetOrAdd(player.PlayerUID, _ => new PreciseProgressData());
                bool hasVanillaPrecise = PlayerHasVanillaPreciseStatic(player.Entity);
                int currentBonus = hasVanillaPrecise ? VANILLA_PRECISE_MECHANICAL_DAMAGE_BONUS : progress.TotalCredits;
                return TextCommandResult.Success($"Current precise level: {progress.TotalCredits}/{MaxPrecisePercent} (+{currentBonus}% mechanical damage)");
            }

            string toolName = (string)args[1];
            return SetPreciseLevelForPlayer(player, newLevel.Value, toolName);
        }

        /// <summary>
        /// Handler for /trait technical command.
        /// </summary>
        private TextCommandResult OnTraitTechnicalCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Technical trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
            sb.AppendLine($"Translocators repaired: {progress.TranslocatorsRepaired} / {TechnicalRequiredTranslocatorRepairs}");
            if (!progress.IsUnlocked)
            {
                int remaining = TechnicalRequiredTranslocatorRepairs - progress.TranslocatorsRepaired;
                sb.AppendLine($"Repair {remaining} more translocators to unlock!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait technicalunlock command.
        /// </summary>
        private TextCommandResult OnTraitTechnicalUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            progress.IsUnlocked = unlock;

            pendingTechnicalProgressSave = true;
            ApplyTechnicalBonusStatic(player, unlock);

            // Check if Tinkerer should be unlocked
            if (unlock)
            {
                CheckTinkererUnlock(player);
            }

            return TextCommandResult.Success($"Technical trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Process a translocator repair (called from Harmony patch).
        /// Gives progress toward Technical trait unlock.
        /// </summary>
        public static void ProcessTranslocatorRepair(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            // Check if technical skill is disabled
            if (IsSkillDisabled("technical")) return;

            string playerUid = player.PlayerUID;
            var progress = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());

            // Already unlocked - no more progress needed
            if (progress.IsUnlocked) return;

            // Increment translocator repairs (apply sleep buff multiplier if active)
            int modifiedRepairs = ApplyXPMultiplier(playerUid, 1);
            progress.TranslocatorsRepaired += modifiedRepairs;
            pendingTechnicalProgressSave = true;

            ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} repaired translocator ({progress.TranslocatorsRepaired} / {TechnicalRequiredTranslocatorRepairs})");

            // Check if we've reached the unlock threshold
            if (progress.TranslocatorsRepaired >= TechnicalRequiredTranslocatorRepairs)
            {
                progress.IsUnlocked = true;
                ApplyTechnicalBonusStatic(player, true);

                // Notify player
                NotifyLevelUp(player,
                    Lang.Get("seraphleveling:message-technical-unlock"));

                // Check if Tinkerer should now be unlocked
                CheckTinkererUnlock(player);
            }
        }

        /// <summary>
        /// Handler for /trait hardyhealth command.
        /// </summary>
        private TextCommandResult OnTraitHardyHealthCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());
            var miningProgress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Hardy Health trait: {(progress.IsUnlocked ? $"UNLOCKED (+{HardyHealthBonus} HP)" : "Locked")}");
            sb.AppendLine($"Requirements:");
            sb.AppendLine($"  Mining level: {miningProgress.TotalCredits} / {HardyHealthMiningThreshold} ({(miningProgress.TotalCredits >= HardyHealthMiningThreshold ? "✓" : "✗")})");
            sb.AppendLine($"  Armor durability: {armorProgress.TotalDurabilityCredits} / {HardyHealthArmorDurabilityThreshold} ({(armorProgress.TotalDurabilityCredits >= HardyHealthArmorDurabilityThreshold ? "✓" : "✗")})");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait bowyer command.
        /// </summary>
        private TextCommandResult OnTraitBowyerCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());
            var rangedProgress = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Bowyer trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
            sb.AppendLine($"Requirements:");
            sb.AppendLine($"  Ranged level: {rangedProgress.TotalCredits} / {BowyerRangedDamageThreshold} ({(rangedProgress.TotalCredits >= BowyerRangedDamageThreshold ? "✓" : "✗")})");
            sb.AppendLine($"  Bow damage: {progress.TotalBowDamage:F0} / {BowyerBowDamageThreshold} ({(progress.TotalBowDamage >= BowyerBowDamageThreshold ? "✓" : "✗")})");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait improviser command.
        /// </summary>
        private TextCommandResult OnTraitImproviserCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Improviser trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
            sb.AppendLine($"Rock damage: {progress.TotalRockDamage:F0} / {ImproviserRockDamageThreshold} ({(progress.TotalRockDamage >= ImproviserRockDamageThreshold ? "✓" : "✗")})");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait tinkerer command.
        /// </summary>
        private TextCommandResult OnTraitTinkererCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());
            var technicalProgress = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            var preciseProgress = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Tinkerer trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
            sb.AppendLine($"Requirements:");
            sb.AppendLine($"  Technical trait: {(technicalProgress.IsUnlocked ? "UNLOCKED ✓" : "Locked ✗")}");
            sb.AppendLine($"  Precise level: {preciseProgress.TotalCredits} / {TinkererPreciseThreshold} ({(preciseProgress.TotalCredits >= TinkererPreciseThreshold ? "✓" : "✗")})");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait merciless command.
        /// </summary>
        private TextCommandResult OnTraitMercilessCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());
            var armorProgress = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            var meleeProgress = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Merciless trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
            sb.AppendLine($"Requirements:");
            sb.AppendLine($"  Armor durability: {armorProgress.TotalDurabilityCredits} / {MercilessArmorDurabilityThreshold} ({(armorProgress.TotalDurabilityCredits >= MercilessArmorDurabilityThreshold ? "✓" : "✗")})");
            sb.AppendLine($"  Melee level: {meleeProgress.TotalCredits} / {MercilessMeleeDamageThreshold} ({(meleeProgress.TotalCredits >= MercilessMeleeDamageThreshold ? "✓" : "✗")})");

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait claustrophobic command.
        /// </summary>
        private TextCommandResult OnTraitClaustrophobicCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            // Check if player is Hunter
            if (!PlayerHasVanillaClaustrophobic(player.Entity))
            {
                return TextCommandResult.Success("Claustrophobic removal is only available for classes with that trait.");
            }

            string playerUid = player.PlayerUID;
            var progress = ClaustrophobicRemovalProgress.GetOrAdd(playerUid, _ => new ClaustrophobicRemovalProgressData());
            var miningProgress = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Claustrophobic trait: {(progress.IsRemoved ? "REMOVED" : "Active")}");
            sb.AppendLine($"Mining level: {miningProgress.TotalCredits} / {ClaustrophobicRemovalMiningThreshold} ({(miningProgress.TotalCredits >= ClaustrophobicRemovalMiningThreshold ? "✓" : "✗")})");
            if (!progress.IsRemoved)
            {
                int remaining = ClaustrophobicRemovalMiningThreshold - miningProgress.TotalCredits;
                sb.AppendLine($"Reach {remaining}% more mining level to remove Claustrophobic!");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait heavyfooted command.
        /// </summary>
        private TextCommandResult OnTraitHeavyFootedCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            if (!PlayerHasSLHeavyFooted(player.Entity))
            {
                return TextCommandResult.Success("Heavy-footed removal is only available for classes with that trait.");
            }

            string playerUid = player.PlayerUID;
            var progress = HeavyFootedRemovalProgress.GetOrAdd(playerUid, _ => new HeavyFootedRemovalProgressData());
            var furtiveProgress = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData());
            var walkingProgress = WalkingProgress.GetOrAdd(playerUid, _ => new WalkingProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Heavy Footed trait: {(progress.IsRemoved ? "REMOVED" : "Active")}");
            sb.AppendLine($"Requirements:");
            sb.AppendLine($"  Furtive level: {furtiveProgress.TotalCredits} / {HeavyFootedFurtiveThreshold} ({(furtiveProgress.TotalCredits >= HeavyFootedFurtiveThreshold ? "✓" : "✗")})");
            sb.AppendLine($"  Walking level: {walkingProgress.TotalCredits} / {HeavyFootedWalkingThreshold} ({(walkingProgress.TotalCredits >= HeavyFootedWalkingThreshold ? "✓" : "✗")})");

            if (!progress.IsRemoved)
            {
                int remainingFurtive = HeavyFootedFurtiveThreshold - furtiveProgress.TotalCredits;
                int remainingWalking = HeavyFootedWalkingThreshold - walkingProgress.TotalCredits;
                sb.AppendLine($"Reach {remainingFurtive}% more furtive level and {remainingWalking}% more walking level to remove Heavy-footed trait.");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        /// <summary>
        /// Handler for /trait hardyhealthunlock command.
        /// </summary>
        private TextCommandResult OnTraitHardyHealthUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());
            progress.IsUnlocked = unlock;

            pendingHardyHealthProgressSave = true;
            ApplyHardyHealthBonusStatic(player, unlock);

            return TextCommandResult.Success($"Hardy Health trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Handler for /trait bowyerunlock command.
        /// </summary>
        private TextCommandResult OnTraitBowyerUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());
            progress.IsUnlocked = unlock;

            pendingBowyerProgressSave = true;
            ApplyBowyerBonusStatic(player, unlock);

            return TextCommandResult.Success($"Bowyer trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Handler for /trait improviserunlock command.
        /// </summary>
        private TextCommandResult OnTraitImproviserUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());
            progress.IsUnlocked = unlock;

            pendingImproviserProgressSave = true;
            ApplyImproviserBonusStatic(player, unlock);

            return TextCommandResult.Success($"Improviser trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Handler for /trait tinkererunlock command.
        /// </summary>
        private TextCommandResult OnTraitTinkererUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());
            progress.IsUnlocked = unlock;

            pendingTinkererProgressSave = true;
            ApplyTinkererBonusStatic(player, unlock);

            return TextCommandResult.Success($"Tinkerer trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Handler for /trait mercilessunlock command.
        /// </summary>
        private TextCommandResult OnTraitMercilessUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());
            progress.IsUnlocked = unlock;

            pendingMercilessProgressSave = true;
            ApplyMercilessBonusStatic(player, unlock);

            return TextCommandResult.Success($"Merciless trait {(unlock ? "unlocked" : "locked")}.");
        }

        /// <summary>
        /// Handler for /trait claustrophobicunlock command.
        /// </summary>
        private TextCommandResult OnTraitClaustrophobicUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool removed = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = ClaustrophobicRemovalProgress.GetOrAdd(playerUid, _ => new ClaustrophobicRemovalProgressData());
            progress.IsRemoved = removed;

            pendingClaustrophobicRemovalProgressSave = true;
            ApplyClaustrophobicRemovalStatic(player, removed);

            return TextCommandResult.Success($"Claustrophobic trait {(removed ? "removed" : "restored")}.");
        }

        /// <summary>
        /// Handler for /trait heavyfootedunlock command.
        /// </summary>
        private TextCommandResult OnTraitHeavyFootedUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool removed = (bool)args[0];

            string playerUid = player.PlayerUID;
            var progress = HeavyFootedRemovalProgress.GetOrAdd(playerUid, _ => new HeavyFootedRemovalProgressData());
            progress.IsRemoved = removed;

            pendingHeavyFootedRemovalProgressSave = true;
            ApplyHeavyFootedRemovalStatic(player, removed);

            return TextCommandResult.Success($"Heavy-footed trait {(removed ? "removed" : "restored")}.");
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
            if (MiningProgress.TryGetValue(playerUid, out var miningProg))
            {
                miningProg.TotalCredits = 0;
                miningProg.PickaxeProgress.Clear();
                pendingMiningProgressSave = true;
            }
            ApplyMiningBonus(player, 0);

            // Reset Melee
            if (MeleeProgress.TryGetValue(playerUid, out var meleeProg))
            {
                meleeProg.TotalCredits = 0;
                meleeProg.WeaponProgress.Clear();
                pendingMeleeProgressSave = true;
            }
            ApplyMeleeBonusStatic(player, 0);

            // Reset Ranged
            if (RangedProgress.TryGetValue(playerUid, out var rangedProg))
            {
                rangedProg.TotalCredits = 0;
                rangedProg.WeaponProgress.Clear();
                pendingRangedProgressSave = true;
            }
            ApplyRangedBonusStatic(player, 0);

            // Reset Walking
            WalkingProgressData.ResetProgress(player);

            // Reset Hunger
            if (HungerProgress.TryGetValue(playerUid, out var hungerProg))
            {
                hungerProg.TotalCredits = 0;
                hungerProg.SecondsInIncrement = 0;
                hungerProg.CurrentIncrementSize = 300; // Default base (5 minutes)
                pendingHungerProgressSave = true;
            }
            ApplyHungerBonusStatic(player, 0);

            // Reset Armor
            if (ArmorProgress.TryGetValue(playerUid, out var armorProg))
            {
                armorProg.TotalDurabilityCredits = 0;
                armorProg.TotalWalkSpeedCredits = 0;
                armorProg.ArmorProgress.Clear();
                pendingArmorProgressSave = true;
            }
            ApplyArmorBonusesStatic(player, 0, 0);

            // Reset Clothier
            if (ClothierProgress.TryGetValue(playerUid, out var clothierProg))
            {
                clothierProg.SewingKitUnlocked = false;
                clothierProg.UniqueClothesWorn.Clear();
                pendingClothierProgressSave = true;
            }
            ApplyClothierBonusStatic(player, clothierProg ?? new ClothierProgressData());

            // Reset Mender
            if (MenderProgress.TryGetValue(playerUid, out var menderProg))
            {
                menderProg.TotalCredits = 0;
                menderProg.RepairsInIncrement = 0;
                menderProg.CurrentIncrementSize = 5; // Default base
                pendingMenderProgressSave = true;
            }
            ApplyMenderBonusStatic(player, 0);

            // Reset Pilferer
            if (PilfererProgress.TryGetValue(playerUid, out var pilfererProg))
            {
                pilfererProg.TotalCredits = 0;
                pilfererProg.PointsInIncrement = 0;
                pilfererProg.CurrentIncrementSize = 10; // Default base
                pendingPilfererProgressSave = true;
            }
            ApplyPilfererBonusStatic(player, 0);

            // Reset Resourceful
            if (ResourcefulProgress.TryGetValue(playerUid, out var resourcefulProg))
            {
                resourcefulProg.TotalCredits = 0;
                resourcefulProg.AnimalsInIncrement = 0;
                resourcefulProg.CurrentIncrementSize = 10; // Default base
                pendingResourcefulProgressSave = true;
            }
            ApplyResourcefulBonusStatic(player, 0);

            // Reset Forager
            if (ForagerProgress.TryGetValue(playerUid, out var foragerProg))
            {
                foragerProg.TotalCredits = 0;
                foragerProg.CropsInIncrement = 0;
                foragerProg.CurrentIncrementSize = 10; // Default base
                pendingForagerProgressSave = true;
            }
            ApplyForagerBonusStatic(player, 0);

            // Reset Furtive
            if (FurtiveProgress.TryGetValue(playerUid, out var furtiveProg))
            {
                furtiveProg.TotalCredits = 0;
                furtiveProg.BlocksInIncrement = 0;
                furtiveProg.CurrentIncrementSize = 100; // Default base
                pendingFurtiveProgressSave = true;
            }
            ApplyFurtiveBonusStatic(player, 0);

            // Reset Precise
            if (PreciseProgress.TryGetValue(playerUid, out var preciseProg))
            {
                preciseProg.TotalCredits = 0;
                preciseProg.WeaponProgress.Clear();
                pendingPreciseProgressSave = true;
            }
            ApplyPreciseBonusStatic(player, 0);

            // Reset Technical
            if (TechnicalProgress.TryGetValue(playerUid, out var technicalProg))
            {
                technicalProg.TranslocatorsRepaired = 0;
                technicalProg.IsUnlocked = false;
                pendingTechnicalProgressSave = true;
            }
            ApplyTechnicalBonusStatic(player, false);

            // Reset Hardy Health
            if (HardyHealthProgress.TryGetValue(playerUid, out var hardyHealthProg))
            {
                hardyHealthProg.IsUnlocked = false;
                pendingHardyHealthProgressSave = true;
            }
            ApplyHardyHealthBonusStatic(player, false);

            // Reset Bowyer
            if (BowyerProgress.TryGetValue(playerUid, out var bowyerProg))
            {
                bowyerProg.IsUnlocked = false;
                bowyerProg.TotalBowDamage = 0;
                pendingBowyerProgressSave = true;
            }
            ApplyBowyerBonusStatic(player, false);

            // Reset Improviser
            if (ImproviserProgress.TryGetValue(playerUid, out var improviserProg))
            {
                improviserProg.IsUnlocked = false;
                improviserProg.TotalRockDamage = 0;
                pendingImproviserProgressSave = true;
            }
            ApplyImproviserBonusStatic(player, false);

            // Reset Tinkerer
            if (TinkererProgress.TryGetValue(playerUid, out var tinkererProg))
            {
                tinkererProg.IsUnlocked = false;
                pendingTinkererProgressSave = true;
            }
            ApplyTinkererBonusStatic(player, false);

            // Reset Merciless
            if (MercilessProgress.TryGetValue(playerUid, out var mercilessProg))
            {
                mercilessProg.IsUnlocked = false;
                pendingMercilessProgressSave = true;
            }
            ApplyMercilessBonusStatic(player, false);

            // Reset Claustrophobic Removal
            if (ClaustrophobicRemovalProgress.TryGetValue(playerUid, out var claustrophobicProg))
            {
                claustrophobicProg.IsRemoved = false;
                pendingClaustrophobicRemovalProgressSave = true;
            }
            ApplyClaustrophobicRemovalStatic(player, false);

            if (HeavyFootedRemovalProgress.TryGetValue(playerUid, out var heavyFootedProg))
            {
                heavyFootedProg.IsRemoved = false;
                pendingHeavyFootedRemovalProgressSave = true;
            }
            ApplyHeavyFootedRemovalStatic(player, false);

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
            };

            if (MiningProgress.TryGetValue(uid, out var mining)) ex.Mining = mining;
            if (MeleeProgress.TryGetValue(uid, out var melee)) ex.Melee = melee;
            if (RangedProgress.TryGetValue(uid, out var ranged)) ex.Ranged = ranged;
            if (WalkingProgress.TryGetValue(uid, out var walking)) ex.Walking = walking;
            if (HungerProgress.TryGetValue(uid, out var hunger)) ex.Hunger = hunger;
            if (ArmorProgress.TryGetValue(uid, out var armor)) ex.Armor = armor;
            if (ClothierProgress.TryGetValue(uid, out var clothier)) ex.Clothier = clothier;
            if (MenderProgress.TryGetValue(uid, out var mender)) ex.Mender = mender;
            if (PilfererProgress.TryGetValue(uid, out var pilferer)) ex.Pilferer = pilferer;
            if (ResourcefulProgress.TryGetValue(uid, out var resourceful)) ex.Resourceful = resourceful;
            if (ForagerProgress.TryGetValue(uid, out var forager)) ex.Forager = forager;
            if (FurtiveProgress.TryGetValue(uid, out var furtive)) ex.Furtive = furtive;
            if (PreciseProgress.TryGetValue(uid, out var precise)) ex.Precise = precise;
            if (TechnicalProgress.TryGetValue(uid, out var technical)) ex.Technical = technical;
            if (HardyHealthProgress.TryGetValue(uid, out var hardy)) ex.HardyHealth = hardy;
            if (BowyerProgress.TryGetValue(uid, out var bowyer)) ex.Bowyer = bowyer;
            if (ImproviserProgress.TryGetValue(uid, out var improviser)) ex.Improviser = improviser;
            if (TinkererProgress.TryGetValue(uid, out var tinkerer)) ex.Tinkerer = tinkerer;
            if (MercilessProgress.TryGetValue(uid, out var merciless)) ex.Merciless = merciless;
            if (ClaustrophobicRemovalProgress.TryGetValue(uid, out var claustro)) ex.ClaustrophobicRemoval = claustro;
            if (HeavyFootedRemovalProgress.TryGetValue(uid, out var heavyFooted)) ex.HeavyFootedRemoval = heavyFooted;
            if (COProgress.TryGetValue(uid, out var co)) ex.CombatOverhaul = co;

            return ex;
        }

        /// <summary>Install imported progression under a UID and flag each system for save.</summary>
        private void ApplyImportedProgress(string uid, PlayerProgressExport ex)
        {
            if (ex.Mining != null) { MiningProgress[uid] = ex.Mining; pendingMiningProgressSave = true; }
            if (ex.Melee != null) { MeleeProgress[uid] = ex.Melee; pendingMeleeProgressSave = true; }
            if (ex.Ranged != null) { RangedProgress[uid] = ex.Ranged; pendingRangedProgressSave = true; }
            if (ex.Walking != null) { WalkingProgress[uid] = ex.Walking; pendingWalkingProgressSave = true; }
            if (ex.Hunger != null) { HungerProgress[uid] = ex.Hunger; pendingHungerProgressSave = true; }
            if (ex.Armor != null) { ArmorProgress[uid] = ex.Armor; pendingArmorProgressSave = true; }
            if (ex.Clothier != null) { ClothierProgress[uid] = ex.Clothier; pendingClothierProgressSave = true; }
            if (ex.Mender != null) { MenderProgress[uid] = ex.Mender; pendingMenderProgressSave = true; }
            if (ex.Pilferer != null) { PilfererProgress[uid] = ex.Pilferer; pendingPilfererProgressSave = true; }
            if (ex.Resourceful != null) { ResourcefulProgress[uid] = ex.Resourceful; pendingResourcefulProgressSave = true; }
            if (ex.Forager != null) { ForagerProgress[uid] = ex.Forager; pendingForagerProgressSave = true; }
            if (ex.Furtive != null) { FurtiveProgress[uid] = ex.Furtive; pendingFurtiveProgressSave = true; }
            if (ex.Precise != null) { PreciseProgress[uid] = ex.Precise; pendingPreciseProgressSave = true; }
            if (ex.Technical != null) { TechnicalProgress[uid] = ex.Technical; pendingTechnicalProgressSave = true; }
            if (ex.HardyHealth != null) { HardyHealthProgress[uid] = ex.HardyHealth; pendingHardyHealthProgressSave = true; }
            if (ex.Bowyer != null) { BowyerProgress[uid] = ex.Bowyer; pendingBowyerProgressSave = true; }
            if (ex.Improviser != null) { ImproviserProgress[uid] = ex.Improviser; pendingImproviserProgressSave = true; }
            if (ex.Tinkerer != null) { TinkererProgress[uid] = ex.Tinkerer; pendingTinkererProgressSave = true; }
            if (ex.Merciless != null) { MercilessProgress[uid] = ex.Merciless; pendingMercilessProgressSave = true; }
            if (ex.ClaustrophobicRemoval != null) { ClaustrophobicRemovalProgress[uid] = ex.ClaustrophobicRemoval; pendingClaustrophobicRemovalProgressSave = true; }
            if (ex.CombatOverhaul != null) { COProgress[uid] = ex.CombatOverhaul; pendingCOProgressSave = true; }
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

            // Max Mining
            // Pass raw credits to ApplyMiningBonus, NOT CalculateMiningBonusPercent(credits).
            // ApplyMiningBonus expects the raw credit/level value and subtracts the negative-
            // trait penalty internally (Hunter's Claustrophobic -10%, Tailor's Weak -10%).
            // Passing the already-capped bonus percent caused the penalty to be subtracted
            // twice, so Hunter would land at +40% mining instead of the intended +50%.
            int maxMiningCredits = GetMaxMiningCredits(player.Entity);
            var miningProg = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            miningProg.TotalCredits = maxMiningCredits;
            miningProg.PickaxeProgress.Clear();
            pendingMiningProgressSave = true;
            ApplyMiningBonus(player, maxMiningCredits);

            // Max Melee — same fix as Mining (pass raw credits so Farsighted/Nervous penalties
            // don't get subtracted twice and Hunter/Malefactor/Clockmaker can hit +50%).
            int maxMeleeCredits = GetMaxMeleeCredits(player.Entity);
            var meleeProg = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
            meleeProg.TotalCredits = maxMeleeCredits;
            meleeProg.WeaponProgress.Clear();
            pendingMeleeProgressSave = true;
            ApplyMeleeBonusStatic(player, maxMeleeCredits);

            // Max Ranged
            int maxRangedCredits = GetMaxRangedCredits(player.Entity);
            var rangedProg = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
            rangedProg.TotalCredits = maxRangedCredits;
            rangedProg.WeaponProgress.Clear();
            pendingRangedProgressSave = true;
            ApplyRangedBonusStatic(player, maxRangedCredits);

            // Max Walking
            WalkingProgressData.MaxStat(player);

            // Max Hunger
            int maxHungerCredits = CalculateMaxHungerCredits(player.Entity);
            var hungerProg = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData());
            hungerProg.TotalCredits = maxHungerCredits;
            hungerProg.SecondsInIncrement = 0;
            hungerProg.CurrentIncrementSize = BaseSecondsPerIncrement;
            pendingHungerProgressSave = true;
            ApplyHungerBonusStatic(player, CalculateHungerBonusPercent(maxHungerCredits, player.Entity));

            // Max Armor
            int maxArmorDurabilityCredits = MaxArmorDurabilityPercent;
            int maxArmorWalkSpeedCredits = MaxArmorWalkSpeedPercent;
            var armorProg = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            armorProg.TotalDurabilityCredits = maxArmorDurabilityCredits;
            armorProg.TotalWalkSpeedCredits = maxArmorWalkSpeedCredits;
            armorProg.ArmorProgress.Clear();
            pendingArmorProgressSave = true;
            ApplyArmorBonusesStatic(player, maxArmorDurabilityCredits, maxArmorWalkSpeedCredits);

            // Max Clothier (unlock sewing kit)
            var clothierProg = ClothierProgress.GetOrAdd(playerUid, _ => new ClothierProgressData());
            clothierProg.SewingKitUnlocked = true;
            pendingClothierProgressSave = true;
            ApplyClothierBonusStatic(player, clothierProg);

            // Max Mender
            int maxMenderCredits = MaxMenderPercent;
            var menderProg = MenderProgress.GetOrAdd(playerUid, _ => new MenderProgressData());
            menderProg.TotalCredits = maxMenderCredits;
            menderProg.RepairsInIncrement = 0;
            menderProg.CurrentIncrementSize = BaseMenderRepairsPerIncrement;
            pendingMenderProgressSave = true;
            ApplyMenderBonusStatic(player, maxMenderCredits);

            // Max Pilferer
            int maxPilfererCredits = GetMaxPilfererCredits(player.Entity);
            var pilfererProg = PilfererProgress.GetOrAdd(playerUid, _ => new PilfererProgressData());
            pilfererProg.TotalCredits = maxPilfererCredits;
            pilfererProg.PointsInIncrement = 0;
            pilfererProg.CurrentIncrementSize = BasePilfererPointsPerIncrement;
            pendingPilfererProgressSave = true;
            ApplyPilfererBonusStatic(player, maxPilfererCredits);

            // Max Resourceful
            int maxResourcefulCredits = GetMaxResourcefulCredits(player.Entity);
            var resourcefulProg = ResourcefulProgress.GetOrAdd(playerUid, _ => new ResourcefulProgressData());
            resourcefulProg.TotalCredits = maxResourcefulCredits;
            resourcefulProg.AnimalsInIncrement = 0;
            resourcefulProg.CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement;
            pendingResourcefulProgressSave = true;
            ApplyResourcefulBonusStatic(player, maxResourcefulCredits);

            // Max Forager
            int maxForagerCredits = GetMaxForagerCredits(player.Entity);
            var foragerProg = ForagerProgress.GetOrAdd(playerUid, _ => new ForagerProgressData());
            foragerProg.TotalCredits = maxForagerCredits;
            foragerProg.CropsInIncrement = 0;
            foragerProg.CurrentIncrementSize = BaseForagerCropsPerIncrement;
            pendingForagerProgressSave = true;
            ApplyForagerBonusStatic(player, maxForagerCredits);

            // Max Furtive
            int maxFurtiveCredits = MaxFurtivePercent;
            var furtiveProg = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData());
            furtiveProg.TotalCredits = maxFurtiveCredits;
            furtiveProg.BlocksInIncrement = 0;
            furtiveProg.CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement;
            pendingFurtiveProgressSave = true;
            ApplyFurtiveBonusStatic(player, maxFurtiveCredits);

            // Max Precise
            int maxPreciseCredits = MaxPrecisePercent;
            var preciseProg = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());
            preciseProg.TotalCredits = maxPreciseCredits;
            preciseProg.WeaponProgress.Clear();
            pendingPreciseProgressSave = true;
            ApplyPreciseBonusStatic(player, maxPreciseCredits);

            // Unlock Technical
            var technicalProg = TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            technicalProg.TranslocatorsRepaired = TechnicalRequiredTranslocatorRepairs;
            technicalProg.IsUnlocked = true;
            pendingTechnicalProgressSave = true;
            ApplyTechnicalBonusStatic(player, true);

            // Unlock Hardy Health
            var hardyHealthProg = HardyHealthProgress.GetOrAdd(playerUid, _ => new HardyHealthProgressData());
            hardyHealthProg.IsUnlocked = true;
            pendingHardyHealthProgressSave = true;
            ApplyHardyHealthBonusStatic(player, true);

            // Unlock Bowyer
            var bowyerProg = BowyerProgress.GetOrAdd(playerUid, _ => new BowyerProgressData());
            bowyerProg.IsUnlocked = true;
            bowyerProg.TotalBowDamage = BowyerBowDamageThreshold;
            pendingBowyerProgressSave = true;
            ApplyBowyerBonusStatic(player, true);

            // Unlock Improviser
            var improviserProg = ImproviserProgress.GetOrAdd(playerUid, _ => new ImproviserProgressData());
            improviserProg.IsUnlocked = true;
            improviserProg.TotalRockDamage = ImproviserRockDamageThreshold;
            pendingImproviserProgressSave = true;
            ApplyImproviserBonusStatic(player, true);

            // Unlock Tinkerer
            var tinkererProg = TinkererProgress.GetOrAdd(playerUid, _ => new TinkererProgressData());
            tinkererProg.IsUnlocked = true;
            pendingTinkererProgressSave = true;
            ApplyTinkererBonusStatic(player, true);

            // Unlock Merciless
            var mercilessProg = MercilessProgress.GetOrAdd(playerUid, _ => new MercilessProgressData());
            mercilessProg.IsUnlocked = true;
            pendingMercilessProgressSave = true;
            ApplyMercilessBonusStatic(player, true);

            // Remove Claustrophobic (if applicable)
            var claustrophobicProg = ClaustrophobicRemovalProgress.GetOrAdd(playerUid, _ => new ClaustrophobicRemovalProgressData());
            claustrophobicProg.IsRemoved = true;
            pendingClaustrophobicRemovalProgressSave = true;
            ApplyClaustrophobicRemovalStatic(player, true);
    
            // Remove Heavy-footed (if applicable)
            var heavyFootedProg = HeavyFootedRemovalProgress.GetOrAdd(playerUid, _ => new HeavyFootedRemovalProgressData());
            heavyFootedProg.IsRemoved = true;
            pendingHeavyFootedRemovalProgressSave = true;
            ApplyHeavyFootedRemovalStatic(player, true);

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

            // Mining (pass raw credits — Apply* handles negative-trait subtraction internally)
            var miningProg = MiningProgress.GetOrAdd(playerUid, _ => new MiningProgressData());
            miningProg.TotalCredits = CREDITS;
            miningProg.PickaxeProgress.Clear();
            pendingMiningProgressSave = true;
            ApplyMiningBonus(player, CREDITS);

            // Melee
            var meleeProg = MeleeProgress.GetOrAdd(playerUid, _ => new MeleeProgressData());
            meleeProg.TotalCredits = CREDITS;
            meleeProg.WeaponProgress.Clear();
            pendingMeleeProgressSave = true;
            ApplyMeleeBonusStatic(player, CREDITS);

            // Ranged
            var rangedProg = RangedProgress.GetOrAdd(playerUid, _ => new RangedProgressData());
            rangedProg.TotalCredits = CREDITS;
            rangedProg.WeaponProgress.Clear();
            pendingRangedProgressSave = true;
            ApplyRangedBonusStatic(player, CREDITS);

            // Walking
            WalkingProgressData.ApplyTraitTestSuite1Command(player);

            // Hunger
            var hungerProg = HungerProgress.GetOrAdd(playerUid, _ => new HungerProgressData());
            hungerProg.TotalCredits = CREDITS;
            hungerProg.SecondsInIncrement = 0;
            hungerProg.CurrentIncrementSize = BaseSecondsPerIncrement;
            pendingHungerProgressSave = true;
            ApplyHungerBonusStatic(player, CalculateHungerBonusPercent(CREDITS, player.Entity));

            // Armor (both durability and walkspeed tracks)
            var armorProg = ArmorProgress.GetOrAdd(playerUid, _ => new ArmorProgressData());
            armorProg.TotalDurabilityCredits = CREDITS;
            armorProg.TotalWalkSpeedCredits = CREDITS;
            armorProg.ArmorProgress.Clear();
            pendingArmorProgressSave = true;
            ApplyArmorBonusesStatic(player, CREDITS, CREDITS);

            // Mender
            var menderProg = MenderProgress.GetOrAdd(playerUid, _ => new MenderProgressData());
            menderProg.TotalCredits = CREDITS;
            menderProg.RepairsInIncrement = 0;
            menderProg.CurrentIncrementSize = BaseMenderRepairsPerIncrement;
            pendingMenderProgressSave = true;
            ApplyMenderBonusStatic(player, CREDITS);

            // Pilferer
            var pilfererProg = PilfererProgress.GetOrAdd(playerUid, _ => new PilfererProgressData());
            pilfererProg.TotalCredits = CREDITS;
            pilfererProg.PointsInIncrement = 0;
            pilfererProg.CurrentIncrementSize = BasePilfererPointsPerIncrement;
            pendingPilfererProgressSave = true;
            ApplyPilfererBonusStatic(player, CREDITS);

            // Resourceful
            var resourcefulProg = ResourcefulProgress.GetOrAdd(playerUid, _ => new ResourcefulProgressData());
            resourcefulProg.TotalCredits = CREDITS;
            resourcefulProg.AnimalsInIncrement = 0;
            resourcefulProg.CurrentIncrementSize = BaseResourcefulAnimalsPerIncrement;
            pendingResourcefulProgressSave = true;
            ApplyResourcefulBonusStatic(player, CREDITS);

            // Forager
            var foragerProg = ForagerProgress.GetOrAdd(playerUid, _ => new ForagerProgressData());
            foragerProg.TotalCredits = CREDITS;
            foragerProg.CropsInIncrement = 0;
            foragerProg.CurrentIncrementSize = BaseForagerCropsPerIncrement;
            pendingForagerProgressSave = true;
            ApplyForagerBonusStatic(player, CREDITS);

            // Furtive
            var furtiveProg = FurtiveProgress.GetOrAdd(playerUid, _ => new FurtiveProgressData());
            furtiveProg.TotalCredits = CREDITS;
            furtiveProg.BlocksInIncrement = 0;
            furtiveProg.CurrentIncrementSize = BaseFurtiveSneakBlocksPerIncrement;
            pendingFurtiveProgressSave = true;
            ApplyFurtiveBonusStatic(player, CREDITS);

            // Precise
            var preciseProg = PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());
            preciseProg.TotalCredits = CREDITS;
            preciseProg.WeaponProgress.Clear();
            pendingPreciseProgressSave = true;
            ApplyPreciseBonusStatic(player, CREDITS);

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

        /// <summary>
        /// Handler for /trait resetconfig command.
        /// Resets all trait configuration values (base, increment, max) to their defaults.
        /// </summary>
        private TextCommandResult OnTraitResetConfigCommand(TextCommandCallingArgs args)
        {
            // Mining defaults
            BaseBlocksPerIncrement = 100;
            IncrementStep = 100;
            MaxMiningSpeedPercent = 50;
            OreMultiplier = 5;

            // Melee defaults
            BaseDamagePerIncrement = 100;
            MeleeIncrementStep = 100;
            MaxMeleeDamagePercent = 50;

            // Ranged defaults
            BaseRangedDamagePerIncrement = 100;
            RangedIncrementStep = 100;
            MaxRangedDamagePercent = 50;
            MaxRangedAccuracyPercent = 50;
            MaxRangedDistancePercent = 50;

            // Walking defaults
            BaseBlocksWalkedPerIncrement = 1000;
            WalkingIncrementStep = 1000;
            MaxWalkingSpeedPercent = 15;

            // Hunger defaults
            BaseSecondsPerIncrement = 300;
            HungerIncrementStep = 60;
            MaxHungerReductionPercent = 25;

            // Armor defaults
            BaseSecondsInArmorPerIncrement = 2880;
            ArmorTimeIncrementStep = 2880;
            BaseDamageBlockedPerIncrement = 100;
            ArmorDamageIncrementStep = 100;
            BaseRepairsPerIncrement = 1;
            ArmorRepairIncrementStep = 1;
            MaxArmorDurabilityPercent = 50;
            MaxArmorWalkSpeedPercent = 50;

            // Clothier defaults
            ClothierRequiredUniqueClothes = 20;
            initializeClothierBlacklistedItems(api: ServerApi);

            // Mender defaults
            BaseMenderRepairsPerIncrement = 5;
            MenderIncrementStep = 1;
            MaxMenderPercent = 25;

            // Pilferer defaults
            BasePilfererPointsPerIncrement = 10;
            PilfererIncrementStep = 10;
            MaxPilfererPercent = 20;

            // Resourceful defaults
            BaseResourcefulAnimalsPerIncrement = 10;
            ResourcefulIncrementStep = 10;
            MaxResourcefulLootPercent = 20;
            MaxResourcefulSpeedPercent = 25;

            // Forager defaults
            BaseForagerCropsPerIncrement = 10;
            ForagerIncrementStep = 10;
            MaxForagerLootPercent = 20;
            MaxForagerWildCropPercent = 20;

            // Furtive defaults
            BaseFurtiveSneakBlocksPerIncrement = 100;
            FurtiveIncrementStep = 100;
            MaxFurtivePercent = 35;

            // Precise defaults
            BasePreciseDamagePerIncrement = 100;
            PreciseIncrementStep = 100;
            MaxPrecisePercent = 30;

            // Technical defaults
            TechnicalRequiredTranslocatorRepairs = 5;

            // Hardy Health defaults
            HardyHealthMiningThreshold = 10;
            HardyHealthArmorDurabilityThreshold = 10;
            HardyHealthBonus = 5;

            // Skill decay defaults
            EnableSkillDecay = false;
            DecayGracePeriodDays = 1.0;
            DecayBasePointsPerDay = 10;
            DecayMaxPointsPerDay = 100;
            DecayExemptSkills.Clear();
            DecayGracePeriodOverrides = new Dictionary<string, double>
            {
                { "walking", 2.0 }, { "hunger", 2.0 }, { "furtive", 2.0 }, { "armor", 2.0 },
                { "mender", 3.0 }, { "resourceful", 3.0 },
                { "forager", 5.0 }, { "pilferer", 5.0 }, { "precise", 5.0 }
            };
            DecayBasePointsOverrides = new Dictionary<string, int>
            {
                { "walking", 5 }, { "hunger", 5 }, { "furtive", 5 }, { "armor", 5 },
                { "mender", 3 }, { "resourceful", 3 },
                { "forager", 2 }, { "pilferer", 2 }, { "precise", 2 }
            };
            DecayMaxPointsOverrides = new Dictionary<string, int>
            {
                { "walking", 50 }, { "hunger", 50 }, { "furtive", 50 }, { "armor", 50 },
                { "mender", 30 }, { "resourceful", 30 },
                { "forager", 20 }, { "pilferer", 20 }, { "precise", 20 }
            };

            // Death penalty defaults
            EnableDeathPenalty = false;
            DeathPenaltyFraction = 0.5;
            DeathPenaltyExemptSkills.Clear();
            VerboseDecayLogging = false;

            // Save config
            pendingConfigSave = true;

            return TextCommandResult.Success("All trait configuration values have been reset to defaults.");
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
        // PERSISTENCE METHODS FOR NEW TRAITS
        // =========================================================================

        /// <summary>
        /// Persist clothier progress to world save data.
        /// </summary>
        public static void PersistClothierProgress()
        {
            PersistProgress<ClothierProgressData>();
        }

        /// <summary>
        /// Load clothier progress from world save data.
        /// </summary>
        private void LoadClothierProgress()
        {
            LoadProgress<ClothierProgressData>();
        }

        /// <summary>
        /// Persist mender progress to world save data.
        /// </summary>
        public static void PersistMenderProgress()
        {
            PersistProgress<MenderProgressData>();
        }

        /// <summary>
        /// Load mender progress from world save data.
        /// </summary>
        private void LoadMenderProgress()
        {
            LoadProgress<MenderProgressData>();
        }

        /// <summary>
        /// Persist pilferer progress to world save data.
        /// </summary>
        public static void PersistPilfererProgress()
        {
            PersistProgress<PilfererProgressData>();
        }

        /// <summary>
        /// Load pilferer progress from world save data.
        /// </summary>
        private void LoadPilfererProgress()
        {
            LoadProgress<PilfererProgressData>();
        }

        /// <summary>
        /// Persist resourceful progress to world save data.
        /// </summary>
        public static void PersistResourcefulProgress()
        {
            PersistProgress<ResourcefulProgressData>();
        }

        /// <summary>
        /// Load resourceful progress from world save data.
        /// </summary>
        private void LoadResourcefulProgress()
        {
            LoadProgress<ResourcefulProgressData>();
        }

        /// <summary>
        /// Persist forager progress to world save data.
        /// </summary>
        public static void PersistForagerProgress()
        {
            PersistProgress<ForagerProgressData>();
        }

        /// <summary>
        /// Load forager progress from world save data.
        /// </summary>
        private void LoadForagerProgress()
        {
            LoadProgress<ForagerProgressData>();
        }

        // =========================================================================
        // FURTIVE TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist furtive progress to world save data.
        /// </summary>
        public static void PersistFurtiveProgress()
        {
            PersistProgress<FurtiveProgressData>();
        }

        /// <summary>
        /// Load furtive progress from world save data.
        /// </summary>
        private void LoadFurtiveProgress()
        {
            LoadProgress<FurtiveProgressData>();
        }

        // =========================================================================
        // PRECISE TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist precise progress to world save data.
        /// </summary>
        public static void PersistPreciseProgress()
        {
            PersistProgress<PreciseProgressData>();
        }

        /// <summary>
        /// Load precise progress from world save data.
        /// </summary>
        private void LoadPreciseProgress()
        {
            LoadProgress<PreciseProgressData>();
        }

        // =========================================================================
        // TECHNICAL TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist technical progress to world save data.
        /// </summary>
        public static void PersistTechnicalProgress()
        {
            PersistProgress<TechnicalProgressData>();
        }

        /// <summary>
        /// Load technical progress from world save data.
        /// </summary>
        private void LoadTechnicalProgress()
        {
            LoadProgress<TechnicalProgressData>();
        }

        // =========================================================================
        // HARDY HEALTH TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist hardy health progress to world save data.
        /// </summary>
        public static void PersistHardyHealthProgress()
        {
            PersistProgress<HardyHealthProgressData>();
        }

        /// <summary>
        /// Load hardy health progress from world save data.
        /// </summary>
        private void LoadHardyHealthProgress()
        {
            LoadProgress<HardyHealthProgressData>();
        }

        // =========================================================================
        // BOWYER TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist bowyer progress to world save data.
        /// </summary>
        public static void PersistBowyerProgress()
        {
            PersistProgress<BowyerProgressData>();
        }

        /// <summary>
        /// Load bowyer progress from world save data.
        /// </summary>
        private void LoadBowyerProgress()
        {
            LoadProgress<BowyerProgressData>();
        }

        // =========================================================================
        // IMPROVISER TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist improviser progress to world save data.
        /// </summary>
        public static void PersistImproviserProgress()
        {
            PersistProgress<ImproviserProgressData>();
        }

        /// <summary>
        /// Load improviser progress from world save data.
        /// </summary>
        private void LoadImproviserProgress()
        {
            LoadProgress<ImproviserProgressData>();
        }

        // =========================================================================
        // TINKERER TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist tinkerer progress to world save data.
        /// </summary>
        public static void PersistTinkererProgress()
        {
            PersistProgress<TinkererProgressData>();
        }

        /// <summary>
        /// Load tinkerer progress from world save data.
        /// </summary>
        private void LoadTinkererProgress()
        {
            LoadProgress<TinkererProgressData>();
        }

        // =========================================================================
        // MERCILESS TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist merciless progress to world save data.
        /// </summary>
        public static void PersistMercilessProgress()
        {
            PersistProgress<MercilessProgressData>();
        }

        /// <summary>
        /// Load merciless progress from world save data.
        /// </summary>
        private void LoadMercilessProgress()
        {
            LoadProgress<MercilessProgressData>();
        }

        // =========================================================================
        // CLAUSTROPHOBIC REMOVAL TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist claustrophobic removal progress to world save data.
        /// </summary>
        public static void PersistClaustrophobicRemovalProgress()
        {
            PersistProgress<ClaustrophobicRemovalProgressData>();
        }

        // =========================================================================
        // HEAVYFOOTED REMOVAL TRAIT PERSISTENCE
        // =========================================================================

        /// <summary>
        /// Persist heavyfooted removal progress to world save data.
        /// </summary>
        public static void PersistHeavyFootedRemovalProgress()
        {
            PersistProgress<HeavyFootedRemovalProgressData>();
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
                                    ServerApi.Logger.Warning($"[SeraphLeveling] Skipping corrupt player entry {i+1}/{count} in sleep buff data: {innerEx.Message}");
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

        /// <summary>
        /// Load claustrophobic removal progress from world save data.
        /// </summary>
        private void LoadClaustrophobicRemovalProgress()
        {
            LoadProgress<ClaustrophobicRemovalProgressData>();
        }

        private void LoadHeavyFootedRemovalProgress()
        {
            LoadProgress<HeavyFootedRemovalProgressData>();
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

            // Mirror server's IsCombatOverhaulLoaded flag on the client. The server-side
            // assignment in StartServerSide doesn't run on a client-only instance, so without
            // this the postfix would think CO is never loaded and skip CO trait display
            // even when CO is actually installed. Accepts the original mod and the 1.22 fork.
            SeraphLevelingModSystem.IsCombatOverhaulLoaded =
                SeraphLevelingModSystem.DetectAnyCombatOverhaul(api.ModLoader);
            SeraphLevelingModSystem.IsCombatOverhaulForkLoaded = api.ModLoader.IsModEnabled("combatoverhaulfork");

            SeraphLevelingModSystem.IsSacredLibLoaded = SeraphLevelingModSystem.DetectAnySacredLib(api.ModLoader);

            // Register network channel for receiving level-up sounds from server
            api.Network.RegisterChannel("seraphleveling")
                .RegisterMessageType<LevelUpSoundMessage>()
                .SetMessageHandler<LevelUpSoundMessage>(OnLevelUpSoundReceived);

            // Apply Harmony patches manually for better control
            harmony = new Harmony("seraphleveling");
            try
            {
                ApplyPatches(api);
                api.Logger.Notification("[SeraphLeveling] Client-side mod loaded, Harmony patches applied");
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
