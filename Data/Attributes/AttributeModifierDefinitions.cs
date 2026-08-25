using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using SeraphLeveling.Data.Mods;
using SeraphLeveling.Data.Tools;
using SeraphLeveling.Patches;
using SeraphLeveling.Util;
using static SeraphLeveling.Util.IAssetLocationMatcher;

namespace SeraphLeveling.Data.Attributes
{
    public static class AttributeModifierDefinitions
    {
        public static readonly GenericUnlockedAttributeModifierDefinition Tinkerer = new()
        {
            Id = "tinkerer",
            SkillKey = "tinkerer",
            PersistenceHeader = "TNK",
            Name = "Tinkerer",
            Trait = new(() => Traits.TraitDefinitions.Tinkerer)
        };

        public static readonly TechnicalAttributeModifierDefinition Technical = new()
        {
            Id = "technical",
            SkillKey = "technical",
            PersistenceHeader = "TEC",
            Name = "Technical",
            GlobalMaxCredits = 5,
            CreditDescription = "translocators",
            PersistenceVersion = 2,
            Trait = new(() => Traits.TraitDefinitions.Technical),
        };

        public static readonly GenericGridCraftUnlockedAttributeModifierDefinition Detonator = new()
        {
            Id = "detonator",
            SkillKey = "detonator",
            PersistenceHeader = "DET",
            Name = "Detonator",
            GlobalMaxCredits = 80,
            CreditDescription = "bombs",
            Trait = new(() => Traits.TraitDefinitions.Detonator),
            CraftedItemName = "Bombs",
            ResultAllowList = Simple("bomb-"),
        };

        public static readonly GenericGridCraftUnlockedAttributeModifierDefinition Weaver = new()
        {
            Id = "weaver",
            SkillKey = "weaver",
            PersistenceHeader = "WVR",
            Name = "Weaver",
            GlobalMaxCredits = 25,
            CreditDescription = "linen",
            Trait = new(() => Traits.TraitDefinitions.Weaver),
            CraftedItemName = "Linen cloth",
            ResultAllowList = Simple("linen-normal-down", MatcherType.PathExact),
        };

        public static readonly GenericGridCraftUnlockedAttributeModifierDefinition InteriorDesigner = new()
        {
            Id = "interiorDesigner",
            SkillKey = "interiordesigner",
            PersistenceHeader = "IDR",
            Name = "InteriorDesigner",
            GlobalMaxCredits = 20,
            CreditDescription = "furniture",
            Trait = new(() => Traits.TraitDefinitions.InteriorDesigner),
            CraftedItemName = "Furniture",
            ResultAllowList = Or(Simple("table-"), Simple("chair-")),
        };

        public static readonly BowyerAttributeModifierDefinition Bowyer = new()
        {
            Id = "bowyer",
            SkillKey = "bowyer",
            PersistenceHeader = "BWY",
            Name = "Bowyer",
            GlobalMaxCredits = 300,
            CreditDescription = "bow damage",
            WatchedCreditsAttributeKey = "sitBowyerBowDamage",
            Trait = new(() => Traits.TraitDefinitions.Bowyer),
            Weapons = [ ToolDefinitions.Bow ],
        };

        public static readonly GenericUnlockedAttributeModifierDefinition Merciless = new()
        {
            Id = "merciless",
            SkillKey = "merciless",
            PersistenceHeader = "MRC",
            Name = "Merciless",
            Trait = new(() => Traits.TraitDefinitions.Merciless)
        };

        public static readonly CaveExplorerAttributeModifierDefinition CaveExplorer = new()
        {
            Id = "caveExplorer",
            SkillKey = "caveexplorer",
            PersistenceHeader = "CEX",
            Name = "CaveExplorer",
            Trait = new(() => Traits.TraitDefinitions.CaveExplorer),
        };

        public static readonly GenericLeveledAttributeModifierDefinition WalkingSpeed = new()
        {
            Id = "walkingSpeed",
            SkillKey = "walking",
            PersistenceHeader = "SIW",
            Name = "Walking",
            Stat = "% speed",
            LongDescription = "walking speed",
            IncrementUnits = "blocks",
            BaseIncrement = 1000,
            IncrementStep = 1000,
            GlobalMaxCredits = 15,
            StatName = "walkspeed",
        };

        public static readonly GenericLeveledAttributeModifierDefinition Townie = new()
        {
            Id = "townie",
            SkillKey = "townie",
            PersistenceHeader = "TOW",
            Name = "Townie",
            Stat = "% speed on path",
            LongDescription = "walking speed on path",
            IncrementUnits = "blocks",
            BaseIncrement = 250,
            IncrementStep = 250,
            GlobalMaxCredits = 15,
            StatName = "sacredlib:onTheRoad",
        };

        public static readonly GenericLeveledAttributeModifierDefinition Furtive = new()
        {
            Id = "furtive",
            SkillKey = "furtive",
            PersistenceHeader = "FUR",
            Name = "Furtive",
            Stat = "% animal detection range",
            LongDescription = "furtive",
            IncrementUnits = "blocks",
            StatName = "animalSeekingRange",
            IsInverted = true,
            BaseIncrement = 100,
            IncrementStep = 100,
            GlobalMaxCredits = 35,
        };

        public static readonly GenericLeveledAttributeModifierDefinition HungerRate = new()
        {
            Id = "hungerRate",
            SkillKey = "hunger",
            PersistenceHeader = "SIH",
            Name = "Hunger",
            IsInverted = true,
            IncrementUnits = "seconds",
            LongDescription = "hunger rate",
            Stat = "% hunger rate",
            BaseIncrement = 300,
            IncrementStep = 60,
            GlobalMaxCredits = 25,
            StatName = "hungerrate",
        };

        public static readonly GenericLeveledAttributeModifierDefinition ForageLootingBonus = new()
        {
            Id = "forageLooting",
            SkillKey = "foragerlooting",
            PersistenceHeader = "FRG",
            Name = "ForagingLoot",
            LongDescription = "looting bonus",
            Stat = "% foraging loot",
            BaseIncrement = 10,
            IncrementStep = 10,
            GlobalMaxCredits = 20,
            IncrementUnits = "crops",
            StatName = "forageDropRate",
        };

        public static readonly GenericLeveledAttributeModifierDefinition WildCropDropRate = new()
        {
            Id = "forageWildCrop",
            SkillKey = "forager",
            PersistenceHeader = "FRG",
            Name = "Foraging",
            LongDescription = "wild crop rate",
            Stat = "% wild crop drops",
            BaseIncrement = 10,
            IncrementStep = 10,
            GlobalMaxCredits = 20,
            IncrementUnits = "crops",
            StatName = "wildCropDropRate",
        };

        public static readonly GenericLeveledAttributeModifierDefinition FarmedCropDropRate = new()
        {
            Id = "cropRate",
            SkillKey = "croprate",
            PersistenceHeader = "FCR",
            Name = "CropRate",
            LongDescription = "produce drop rate",
            Stat = "% produce drop rate",
            BaseIncrement = 15,
            IncrementStep = 15,
            GlobalMaxCredits = 250,
            IncrementUnits = "crops",
            StatName = "sacredlib:produceDropRate",
        };

        public static readonly GenericLeveledAttributeModifierDefinition AnimalDropRate = new()
        {
            Id = "Resourceful",
            SkillKey = "resourceful",
            PersistenceHeader = "RSF",
            Name = "Resourceful",
            Stat = "% animal loot",
            BaseIncrement = 10,
            IncrementStep = 10,
            GlobalMaxCredits = 20,
            IncrementUnits = "animals",
            StatName = "animalLootDropRate",
        };

        public static readonly GenericLeveledAttributeModifierDefinition AnimalHarvestRate = new()
        {
            Id = "animalHarvesting",
            SkillKey = "animalharvester",
            PersistenceHeader = "RSF",
            Name = "AnimalHarvester",
            Stat = "% animal harvest rate",
            BaseIncrement = 10,
            IncrementStep = 10,
            GlobalMaxCredits = 25,
            IncrementUnits = "animals",
            StatName = "animalHarvestingTime",
        };

        public static readonly GenericLeveledAttributeModifierDefinition GearDropRate = new()
        {
            Id = "gearDropRate",
            Name = "GearDropRate",
            SkillKey = "geardroprate",
            PersistenceHeader = "PLF",
            Stat = "% rusty gear rate",
            IncrementUnits = "vessels looted",
            BaseIncrement = 10,
            IncrementStep = 10,
            GlobalMaxCredits = 20,
            StatName = "rustyGearDropRate"
        };

        public static readonly GenericLeveledAttributeModifierDefinition VesselDropRate = new()
        {
            Id = "vesselDropRate",
            Name = "VesselDropRate",
            SkillKey = "vesseldroprate",
            PersistenceHeader = "PLF",
            Stat = "% vessel loot bonus",
            IncrementUnits = "vessels looted",
            BaseIncrement = 10,
            IncrementStep = 10,
            GlobalMaxCredits = 20,
            StatName = "vesselContentsDropRate"
        };

        public static readonly GenericLeveledAttributeModifierDefinition WholeVesselRate = new()
        {
            Id = "wholeVesselRate",
            Name = "WholeVesselRate",
            SkillKey = "wholevesselrate",
            PersistenceHeader = "PLF",
            Stat = "% chance of looting entire vessel",
            IncrementUnits = "vessels looted",
            BaseIncrement = 10,
            IncrementStep = 10,
            GlobalMaxCredits = 20,
            StatName = "wholeVesselLootChance",
        };

        public static readonly GenericLeveledAttributeModifierDefinition Mender = new()
        {
            Id = "mender",
            Name = "Mender",
            SkillKey = "mender",
            PersistenceHeader = "MND",
            IsInverted = true,
            InvertedOnDisplay = false,
            Stat = "% armor durability",
            IncrementUnits = "repairs",
            BaseIncrement = 5,
            IncrementStep = 1,
            GlobalMaxCredits = 25,
            StatName = "armorDurabilityLoss"
        };

        public static readonly ConcurrentDictionary<SimpleToolProgress, IncrementData> MiningIncrementData = new()
        {
            [default] = new IncrementData
            {
                IncrementUnits = "blocks",
                BaseIncrement = 100,
                IncrementStep = 100,
            }
        };

        private static readonly ConcurrentDictionary<IAssetLocationMatcher, float> StoneBlockPoints = new()
        {
            [Simple("ore-", MatcherType.PathContains)] = SeraphLevelingModSystem.OreMultiplier,
            [Or(Simple("meteorite"), Simple("meteoriciron", MatcherType.PathContains))] = SeraphLevelingModSystem.OreMultiplier,
            [Or(Simple("rock-"), Simple("crackedrock-"))] = 1,
        };

        public static readonly MiningAttributeModifierDefinition MiningSpeed = new()
        {
            Id = "miningSpeed",
            SkillKey = "mining",
            Name = "Mining",
            Stat = "% mining speed",
            LongDescription = "mining speed",
            PersistenceHeader = "SIT",
            Tools = [ ToolDefinitions.Pickaxe ],
            IncrementData = MiningIncrementData,
            GlobalMaxCredits = 50,
            StatName = "miningSpeedMul",
            BrokenBlockScores = StoneBlockPoints,
        };

        public static readonly MiningAttributeModifierDefinition StoneDropRate = new()
        {
            Id = "stoneDropRate",
            SkillKey = "stonerate",
            Name = "StoneDropRate",
            Stat = "% bonus stone drop rate",
            LongDescription = "stone drop rate",
            PersistenceHeader = "SDR",
            Tools = [ ToolDefinitions.Pickaxe ],
            IncrementData = MiningIncrementData,
            GlobalMaxCredits = 300,
            StatName = "sacredlib:stoneDropRate",
            BrokenBlockScores = StoneBlockPoints,
        };

        public static readonly MiningAttributeModifierDefinition OreDropRate = new()
        {
            Id = "oreDropRate",
            SkillKey = "orerate",
            Name = "OreDropRate",
            Stat = "% bonus ore drop rate",
            LongDescription = "ore drop rate",
            PersistenceHeader = "SDR",
            Tools = [ ToolDefinitions.Pickaxe ],
            IncrementData = MiningIncrementData,
            GlobalMaxCredits = 300,
            StatName = "sacredlib:oreDropRate",
            BrokenBlockScores = StoneBlockPoints,
        };

        public static readonly ConcurrentDictionary<SimpleToolProgress, IncrementData> TreeIncrementData = new()
        {
            [default] = new IncrementData
            {
                IncrementUnits = "trees",
                BaseIncrement = 20,
                IncrementStep = 20,
            }
        };

        private static readonly ConcurrentDictionary<IAssetLocationMatcher, float> LeavesPoints = new()
        {
            [Simple("leavesbranchy-grown")] = 2,
            [Or(Simple("leaves-grown"), Simple("leavesnarrow-grown"))] = 1,
        };

        public static readonly GenericToolAttributeModifierDefinition WoodDropRate = new()
        {
            Id = "woodDropRate",
            SkillKey = "woodrate",
            Name = "WoodDropRate",
            Stat = "% bonus wood drop rate",
            LongDescription = "wood drop rate",
            PersistenceHeader = "WDR",
            Tools = [ ToolDefinitions.Shears, ToolDefinitions.Axe ],
            IncrementData = TreeIncrementData,
            GlobalMaxCredits = 100,
            StatName = "sacredlib:woodDropRate",
            BrokenBlockScores = LeavesPoints,
        };

        public static readonly GenericToolAttributeModifierDefinition SeedDropRate = new()
        {
            Id = "seedDropRate",
            SkillKey = "seedrate",
            Name = "SeedDropRate",
            Stat = "% bonus tree seed drop rate",
            LongDescription = "tree seed drop rate",
            PersistenceHeader = "SDR",
            Tools = [ ToolDefinitions.Shears, ToolDefinitions.Axe ],
            IncrementData = TreeIncrementData,
            GlobalMaxCredits = 1000,
            StatName = "sacredlib:treeseedDropRate",
            BrokenBlockScores = LeavesPoints,
        };

        public static readonly GenericToolAttributeModifierDefinition StickDropRate = new()
        {
            Id = "stickDropRate",
            SkillKey = "stickrate",
            Name = "StickDropRate",
            Stat = "% bonus stick drop rate",
            LongDescription = "stick drop rate",
            PersistenceHeader = "WDR",
            Tools = [ ToolDefinitions.Shears, ToolDefinitions.Axe ],
            IncrementData = TreeIncrementData,
            GlobalMaxCredits = 300,
            StatName = "sacredlib:stickDropRate",
            BrokenBlockScores = LeavesPoints,
        };


        public static readonly ImproviserAttributeModifierDefinition Improviser = new()
        {
            Id = "improviser",
            SkillKey = "improviser",
            PersistenceHeader = "IMP",
            Name = "Improviser",
            GlobalMaxCredits = 300,
            CreditDescription = "thrown rock damage",
            WatchedCreditsAttributeKey = "sitImproviserRockDamage",
            Trait = new(() => Traits.TraitDefinitions.Improviser),
            Weapons = [ ToolDefinitions.Stone, ToolDefinitions.Sling ],
        };

        public static readonly GenericCollectionUnlockedAttributeModifierDefinition Clothier = new()
        {
            Id = "clothier",
            SkillKey = "clothier",
            PersistenceHeader = "CLT",
            Name = "Clothier",
            RequiredCollectionSize = 20,
            CollectedItemDescription = "clothes worn",
            CollectedItemCountKey = "sitClothierCount",
            TokenAllowList = ["clothes-", "shirt-", "trousers-", "dress-", "hat-", "cape-", "cloak-", "jacket-", "vest-", "skirt-", "gloves-", "boots-", "shoes-", "headband-", "mask-", "scarf-"],
            Trait = new(() => Traits.TraitDefinitions.Clothier),
        };

        public static readonly ConcurrentDictionary<SimpleToolProgress, IncrementData> DamageIncrementData = new()
        {
            [default] = new IncrementData
            {
                IncrementUnits = "damage",
                BaseIncrement = 100,
                IncrementStep = 100,
            }
        };


        public static readonly DamageAttributeModifierDefinition RangedDamage = new()
        {
            Id = "rangedDamage",
            Name = "RangedDamage",
            LongDescription = "ranged damage",
            Stat = "% ranged damage",
            SkillKey = "ranged",
            PersistenceHeader = "SIR",
            Tools = [ ToolDefinitions.Weapon ],
            IncrementData = DamageIncrementData,
            GlobalMaxCredits = 50,
            StatName = "rangedWeaponsDamage",
            Weapons = [ ToolDefinitions.RangedWeapon ],
        };

        public static readonly DamageAttributeModifierDefinition RangedAccuracy = new()
        {
            Id = "rangedAccuracy",
            Name = "RangedAccuracy",
            LongDescription = "ranged accuracy",
            Stat = "% ranged accuracy",
            SkillKey = "rangedaccuracy",
            PersistenceHeader = "SIR",
            Tools = [ ToolDefinitions.Weapon ],
            IncrementData = DamageIncrementData,
            GlobalMaxCredits = 50,
            StatName = "rangedWeaponsAcc",
            Weapons = [ ToolDefinitions.RangedWeapon ],
        };

        public static readonly DamageAttributeModifierDefinition RangedDistance = new()
        {
            Id = "rangedDistance",
            Name = "RangedDistance",
            LongDescription = "ranged distance",
            Stat = "% ranged distance",
            SkillKey = "rangeddistance",
            PersistenceHeader = "SIR",
            Tools = [ ToolDefinitions.Weapon ],
            IncrementData = DamageIncrementData,
            GlobalMaxCredits = 50,
            StatName = "bowDrawingStrength",
            Weapons = [ ToolDefinitions.RangedWeapon ],
        };

        public static readonly DamageAttributeModifierDefinition MeleeDamage = new()
        {
            Id = "meleeDamage",
            Name = "Melee",
            LongDescription = "melee damage",
            Stat = "% melee damage",
            SkillKey = "melee",
            PersistenceHeader = "SIM",
            Tools = [ ToolDefinitions.Weapon ],
            IncrementData = DamageIncrementData,
            GlobalMaxCredits = 50,
            StatName = "meleeWeaponsDamage",
            Weapons = [ ToolDefinitions.MeleeWeapon ],
        };

        public static readonly DamageAttributeModifierDefinition Precise = new()
        {
            Id = "precise",
            Name = "Precise",
            LongDescription = "mechanical damage",
            Stat = "% mechanical damage",
            SkillKey = "precise",
            PersistenceHeader = "PRC",
            Tools = [ ToolDefinitions.Weapon ],
            IncrementData = DamageIncrementData,
            GlobalMaxCredits = 30,
            StatName = "mechanicalsDamage",
            Weapons = [ ToolDefinitions.Weapon ],
        };

        public static readonly MaxHealthUnlockedAttributeModifierDefinition HardyHealth = new()
        {
            Id = "hardyhealth",
            SkillKey = "hardyhealth",
            PersistenceHeader = "HDH",
            Name = "HardyHealth",
            ModifierAmount = 5,
            Trait = new(() => Traits.TraitDefinitions.Hardy),
        };

        public static readonly MaxHealthUnlockedAttributeModifierDefinition BulwarkHealth = new()
        {
            Id = "bulwarkhealth",
            SkillKey = "bulwarkhealth",
            PersistenceHeader = "BWH",
            Name = "BulwarkHealth",
            ModifierAmount = 3,
            Trait = new(() => Traits.TraitDefinitions.Bulwark),
        };

        public static readonly OreRateUnlockedAttributeModifierDefinition ClaustrophobicOrePenalty = new()
        {
            Id = "claustrophobicpenalty",
            SkillKey = "claustrophobic",
            PersistenceHeader = "COR",
            Name = "ClaustrophobicOre",
            ModifierAmount = -0.15f,
            ModifierIsPercentage = true,
            Trait = new(() => Traits.TraitDefinitions.Claustrophobic),
        };

        public static readonly OreRateUnlockedAttributeModifierDefinition ClaustrophobicOre = new()
        {
            Id = "claustrophobic",
            SkillKey = "claustrophobic",
            PersistenceHeader = "COR",
            Name = "ClaustrophobicOre",
            ModifierAmount = 0.15f,
            ModifierIsPercentage = true,
            Trait = new(() => Traits.TraitDefinitions.Claustrophobic),
        };

        public static readonly MaxHealthUnlockedAttributeModifierDefinition FrailHealthPenalty = new()
        {
            Id = "frailhealthpenalty",
            SkillKey = "frailhealth",
            PersistenceHeader = "FRH",
            Name = "FrailHealth",
            ModifierAmount = -2.5f,
            Trait = new(() => Traits.TraitDefinitions.Frail),
        };

        public static readonly MaxHealthUnlockedAttributeModifierDefinition FrailHealthOffset = new()
        {
            Id = "frailhealth",
            SkillKey = "frailhealth",
            PersistenceHeader = "FRH",
            Name = "FrailHealth",
            ModifierAmount = 2.5f,
            Trait = new(() => Traits.TraitDefinitions.Frail),
        };

        public static readonly MaxHealthUnlockedAttributeModifierDefinition WeakHealthPenalty = new()
        {
            Id = "weakhealthpenalty",
            SkillKey = "weakhealth",
            PersistenceHeader = "WKH",
            Name = "WeakHealth",
            ModifierAmount = -2,
            Trait = new(() => Traits.TraitDefinitions.Weak),
        };

        public static readonly MaxHealthUnlockedAttributeModifierDefinition WeakHealthOffset = new()
        {
            Id = "weakhealth",
            SkillKey = "weakhealth",
            PersistenceHeader = "WKH",
            Name = "WeakHealth",
            ModifierAmount = 2,
            Trait = new(() => Traits.TraitDefinitions.Weak),
        };

        public static readonly ConcurrentDictionary<ArmorDurabilityProgressTypes, IncrementData> ArmorDurabilityIncrementData = new()
        {
            [ArmorDurabilityProgressTypes.DamageBlocked] = new IncrementData
            {
                BaseIncrement = 100,
                IncrementStep = 100,
                IncrementUnits = "damage",
            },
            [ArmorDurabilityProgressTypes.RepairProgress] = new IncrementData
            {
                IncrementUnits = "repairs",
                BaseIncrement = 1,
                IncrementStep = 1,
            },
        };
        public static readonly ArmorDurabilityModifierDefinition ArmorDurability = new()
        {
            Id = "armorDurability",
            Name = "ArmorDurability",
            Stat = "% durability bonus",
            SkillKey = "armordurability",
            PersistenceHeader = "ARD",
            IsInverted = true,
            InvertedOnDisplay = false,
            StatName = "armorDurabilityLoss",
            Tools = [ ToolDefinitions.Armor ],
            IncrementData = ArmorDurabilityIncrementData,
            GlobalMaxCredits = 50,
        };

        public static readonly ConcurrentDictionary<SimpleToolProgress, IncrementData> ArmorWornIncrementData = new()
        {
            [default] = new IncrementData
            {
                IncrementUnits = "seconds",
                BaseIncrement = 2880,
                IncrementStep = 2880,
            },
        };
        public static readonly SimpleArmorModifierDefinition ArmorWalkSpeed = new()
        {
            Id = "armorWalkSpeed",
            Name = "ArmorWalkSpeed",
            Stat = "% armor walk speed penalty reduction",
            SkillKey = "armorwalkspeed",
            PersistenceHeader = "ARW",
            IsInverted = true,
            StatName = "armorWalkSpeedAffectedness",
            Tools = [ ToolDefinitions.Armor ],
            IncrementData = ArmorWornIncrementData,
            GlobalMaxCredits = 50,
        };
        public static readonly SimpleArmorModifierDefinition ArmorHungerRate = new()
        {
            Id = "armorHungerRate",
            Name = "ArmorHungerRate",
            Stat = "% armor hunger rate penalty reduction",
            SkillKey = "armorhungerrate",
            PersistenceHeader = "ARH",
            IsInverted = true,
            StatName = "hungerrate",
            Tools = [ ToolDefinitions.Armor ],
            IncrementData = ArmorWornIncrementData,
            GlobalMaxCredits = 50,
        };
        public static readonly SimpleArmorModifierDefinition ArmorHealing = new()
        {
            Id = "armorHealing",
            Name = "ArmorHealing",
            Stat = "% armor healing effectiveness",
            SkillKey = "armorhealing",
            PersistenceHeader = "ARH",
            StatName = "healingeffectivness", // yes, misspelled - that's correct.
            Tools = [ ToolDefinitions.Armor ],
            IncrementData = ArmorWornIncrementData,
            GlobalMaxCredits = 25,
        };

        public static readonly GenericGridCraftUnlockedAttributeModifierDefinition Carpenter = new()
        {
            Id = "carpenter",
            SkillKey = "carpenter",
            PersistenceHeader = "CRP",
            Name = "Carpenter",
            GlobalMaxCredits = 120,
            CreditDescription = "boards",
            WatchedCreditsAttributeKey = "sitCarpenterBoards",
            Trait = new(() => Traits.TraitDefinitions.Carpenter),
            CraftedItemName = "Boards",
            ResultAllowList = Simple("plank-"),
        };

        public static readonly ConcurrentDictionary<SimpleToolProgress, IncrementData> TreeChoppingIncrementData = new()
        {
            [default] = new IncrementData
            {
                IncrementUnits = "trees",
                BaseIncrement = 100,
                IncrementStep = 100,
            }
        };

        private static readonly ConcurrentDictionary<IAssetLocationMatcher, float> WoodLogPoints = new()
        {
            [Simple("log-grown-")] = 5,
        };

        public static readonly GenericToolAttributeModifierDefinition TreeChoppingSpeed = new()
        {
            Id = "treeChoppingSpeed",
            SkillKey = "treechopping",
            Name = "Tree Chopping",
            Stat = "% chopping speed",
            LongDescription = "chopping speed",
            PersistenceHeader = "TRC",
            Tools = [ ToolDefinitions.Axe ],
            IncrementData = TreeChoppingIncrementData,
            GlobalMaxCredits = 250,
            StatName = "ats:wood|axe-?-harvestSpeed",
            BrokenBlockScores = WoodLogPoints,
        };

        public static readonly GenericToolAttributeModifierDefinition AxeDamage = new()
        {
            Id = "axeDamage",
            Name = "Axe Damage",
            LongDescription = "axe damage",
            Stat = "% axe damage",
            SkillKey = "axedamage",
            PersistenceHeader = "XDM",
            Tools = [ ToolDefinitions.Axe ],
            IncrementData = TreeChoppingIncrementData,
            GlobalMaxCredits = 100,
            StatName = "ats:axe-?-meleeDamageMult",
            BrokenBlockScores = WoodLogPoints,
        };

        public static readonly GenericRepairableToolAttributeModifierDefinition AxeDurability = new()
        {
            Id = "axeDurability",
            Name = "AxeDurability",
            Stat = "% axe durability bonus",
            SkillKey = "axedurability",
            PersistenceHeader = "XDU",
            IsInverted = true,
            InvertedOnDisplay = false,
            StatName = "ats:axe-?-reduceDurabilityLoss",
            Tools = [ ToolDefinitions.Axe ],
            IncrementData = new()
            {
                [RepairableToolProgress.Usage] = new()
                {
                    IncrementUnits = "trees",
                    BaseIncrement = 100,
                    IncrementStep = 100,
                },
                [RepairableToolProgress.Repair] = new()
                {
                    IncrementUnits = "repairs",
                    BaseIncrement = 1,
                    IncrementStep = 1,
                }
            },
            GlobalMaxCredits = 75,
        };

        public static readonly GenericLeveledAttributeModifierDefinition CharcoalDropRate = new()
        {
            Id = "charcoalDropRate",
            Name = "CharcoalDropRate",
            SkillKey = "charcoalrate",
            PersistenceHeader = "CDR",
            Stat = "% bonus charcoal drop rate",
            IncrementUnits = "pit charcoal harvested",
            BaseIncrement = 30,
            IncrementStep = 10,
            GlobalMaxCredits = 200,
            StatName = "sacredlib:charcoalDropRate",
        };

        public static readonly ConcurrentDictionary<SimpleToolProgress, IncrementData> DiggingIncrementData = new()
        {
            [default] = new IncrementData
            {
                IncrementUnits = "blocks",
                BaseIncrement = 100,
                IncrementStep = 100,
            }
        };

        private static readonly ConcurrentDictionary<IAssetLocationMatcher, float> DirtPoints = new()
        {
            [Simple("rawclay-")] = 5,
            [Simple("peat-")] = 5,
            [Or(Simple("soil-"), Simple("forestfloor-"), Simple("farmland-"))] = 1,
        };

        public static readonly GenericToolAttributeModifierDefinition ClayDropRate = new()
        {
            Id = "clayDropRate",
            SkillKey = "clayrate",
            Name = "ClayDropRate",
            Stat = "% bonus clay drop rate",
            LongDescription = "clay drop rate",
            PersistenceHeader = "CLD",
            Tools = [ ToolDefinitions.Shovel ],
            IncrementData = DiggingIncrementData,
            GlobalMaxCredits = 50,
            StatName = "sacredlib:clayDropRate",
            BrokenBlockScores = DirtPoints,
        };

        public static readonly GenericToolAttributeModifierDefinition PeatDropRate = new()
        {
            Id = "peatDropRate",
            SkillKey = "peatrate",
            Name = "PeatDropRate",
            Stat = "% bonus peat drop rate",
            LongDescription = "peat drop rate",
            PersistenceHeader = "PDR",
            Tools = [ ToolDefinitions.Shovel ],
            IncrementData = DiggingIncrementData,
            GlobalMaxCredits = 50,
            StatName = "sacredlib:peatDropRate",
            BrokenBlockScores = DirtPoints,
        };

        public static readonly GenericToolAttributeModifierDefinition ClayformSpeed = new()
        {
            Id = "clayformSpeed",
            SkillKey = "clayformspeed",
            Name = "ClayformSpeed",
            Stat = "% clayforming speed",
            LongDescription = "clayform speed",
            PersistenceHeader = "CFS",
            Tools = [ ToolDefinitions.Shovel ],
            IncrementData = DiggingIncrementData,
            GlobalMaxCredits = 100,
            StatName = "ats:handclayformingspeed",
            BrokenBlockScores = DirtPoints,
        };

        public static readonly GenericGridCraftUnlockedAttributeModifierDefinition Mason = new()
        {
            Id = "mason",
            SkillKey = "mason",
            PersistenceHeader = "MAS",
            Name = "Mason",
            GlobalMaxCredits = 40,
            CreditDescription = "ashlar blocks",
            WatchedCreditsAttributeKey = "sitMasonStoneBricks",
            Trait = new(() => Traits.TraitDefinitions.Mason),
            CraftedItemName = "Ashlar blocks",
            ResultAllowList = Simple("stonebrick-"),
        };

        public static readonly GenericGridCraftUnlockedAttributeModifierDefinition Technician = new()
        {
            Id = "technician",
            SkillKey = "technician",
            PersistenceHeader = "TCN",
            Name = "Technician",
            GlobalMaxCredits = 10,
            CreditDescription = "large gears",
            WatchedCreditsAttributeKey = "sitTechnicianLargeGears",
            Trait = new(() => Traits.TraitDefinitions.Technician),
            CraftedItemName = "Large gears",
            ResultAllowList = Simple("largegear3", MatcherType.PathExact),
        };

        public static readonly ConcurrentDictionary<SimpleToolProgress, IncrementData> HealingIncrementData = new()
        {
            [default] = new IncrementData
            {
                IncrementUnits = "items",
                BaseIncrement = 10,
                IncrementStep = 10,
            }
        };

        public static readonly PoulticeAttributeModifierDefinition HealUseSpeed = new()
        {
            Id = "healUseSpeed",
            SkillKey = "healusespeed",
            Name = "HealUseSpeed",
            Stat = "% healing item use speed",
            LongDescription = "healing item use speed",
            PersistenceHeader = "HUS",
            Tools = [ ToolDefinitions.Poultice ],
            IncrementData = HealingIncrementData,
            GlobalMaxCredits = 75,
            StatName = "ats:healitemusetime"
        };

        public static readonly AlchemistAttributeModifierDefinition Alchemist = new()
        {
            Id = "alchemist",
            SkillKey = "alchemist",
            PersistenceHeader = "ALC",
            Name = "Alchemist",
            GlobalMaxCredits = 40,
            CreditDescription = "poultices",
            WatchedCreditsAttributeKey = "sitAlchemistPoultices",
            Trait = new(() => Traits.TraitDefinitions.Alchemist),
        };

        public static readonly PropagatorAttributeModifierDefinition Propagator = new()
        {
            Id = "propagator",
            SkillKey = "propagator",
            PersistenceHeader = "PRP",
            Name = "Propagator",
            GlobalMaxCredits = 80,
            CreditDescription = "compost",
            WatchedCreditsAttributeKey = "sitPropagatorCompost",
            Trait = new(() => Traits.TraitDefinitions.Propagator),
        };

        public static readonly GenericRepairableToolAttributeModifierDefinition HoeDurability = new()
        {
            Id = "hoeDurability",
            SkillKey = "hoedurability",
            Name = "HoeDurability",
            Stat = "% hoe durability loss reduction",
            LongDescription = "hoe durability",
            PersistenceHeader = "HDR",
            Tools = [ ToolDefinitions.Hoe ],
            IncrementData = new()
            {
                [RepairableToolProgress.Usage] = new()
                {
                    IncrementUnits = "blocks",
                    BaseIncrement = 100,
                    IncrementStep = 100,
                },
                [RepairableToolProgress.Repair] = new()
                {
                    IncrementUnits = "repairs",
                    BaseIncrement = 1,
                    IncrementStep = 1,
                }
            },
            GlobalMaxCredits = 75,
            StatName = "ats:hoe-?-reduceDurabilityLoss"
        };

        public static readonly GenericRepairableToolAttributeModifierDefinition ScytheDurability = new()
        {
            Id = "scytheDurability",
            SkillKey = "scythedurability",
            Name = "ScytheDurability",
            Stat = "% scythe durability loss reduction",
            LongDescription = "scythe durability",
            PersistenceHeader = "SDR",
            Tools = [ ToolDefinitions.Scythe ],
            IncrementData = new()
            {
                [RepairableToolProgress.Usage] = new()
                {
                    IncrementUnits = "blocks",
                    BaseIncrement = 100,
                    IncrementStep = 100,
                },
                [RepairableToolProgress.Repair] = new()
                {
                    IncrementUnits = "repairs",
                    BaseIncrement = 1,
                    IncrementStep = 1,
                }
            },
            GlobalMaxCredits = 75,
            StatName = "ats:scythe-?-reduceDurabilityLoss"
        };

        public static readonly GenericRepairableToolAttributeModifierDefinition HammerDurability = new()
        {
            Id = "hammerDurability",
            SkillKey = "hammerdurability",
            Name = "HammerDurability",
            Stat = "% hammer durability loss reduction",
            LongDescription = "hammer durability",
            PersistenceHeader = "HDR",
            Tools = [ ToolDefinitions.Hammer ],
            IncrementData = new()
            {
                [RepairableToolProgress.Usage] = new()
                {
                    IncrementUnits = "smithing strikes",
                    BaseIncrement = 100,
                    IncrementStep = 100,
                },
                [RepairableToolProgress.Repair] = new()
                {
                    IncrementUnits = "repairs",
                    BaseIncrement = 1,
                    IncrementStep = 1,
                }
            },
            GlobalMaxCredits = 75,
            StatName = "ats:hammer-?-reduceDurabilityLoss"
        };

        public static readonly GenericRepairableToolAttributeModifierDefinition PickaxeDurability = new()
        {
            Id = "pickaxeDurability",
            SkillKey = "pickaxedurability",
            Name = "PickaxeDurability",
            Stat = "% pickaxe durability loss reduction",
            LongDescription = "pickaxe durability",
            PersistenceHeader = "PDR",
            Tools = [ ToolDefinitions.Pickaxe ],
            IncrementData = new()
            {
                [RepairableToolProgress.Usage] = new()
                {
                    IncrementUnits = "blocks",
                    BaseIncrement = 100,
                    IncrementStep = 100,
                },
                [RepairableToolProgress.Repair] = new()
                {
                    IncrementUnits = "repairs",
                    BaseIncrement = 1,
                    IncrementStep = 1,
                }
            },
            GlobalMaxCredits = 75,
            StatName = "ats:pickaxe-?-reduceDurabilityLoss"
        };

        public static readonly GenericRepairableToolAttributeModifierDefinition BowDurability = new()
        {
            Id = "bowDurability",
            SkillKey = "bowdurability",
            Name = "BowDurability",
            Stat = "% bow durability loss reduction",
            LongDescription = "bow durability",
            PersistenceHeader = "BDR",
            Tools = [ ToolDefinitions.Bow ],
            IncrementData = new()
            {
                [RepairableToolProgress.Usage] = new()
                {
                    IncrementUnits = "damage",
                    BaseIncrement = 100,
                    IncrementStep = 100,
                },
                [RepairableToolProgress.Repair] = new()
                {
                    IncrementUnits = "repairs",
                    BaseIncrement = 1,
                    IncrementStep = 1,
                }
            },
            GlobalMaxCredits = 75,
            StatName = "ats:bow-?-reduceDurabilityLoss"
        };
        public static readonly GenericRepairableToolAttributeModifierDefinition BowDamage = new()
        {
            Id = "bowDamage",
            SkillKey = "bowdamage",
            Name = "BowDamage",
            Stat = "% bow damage increase",
            LongDescription = "bow damage",
            PersistenceHeader = "BDM",
            Tools = [ ToolDefinitions.Bow ],
            IncrementData = new()
            {
                [RepairableToolProgress.Usage] = new()
                {
                    IncrementUnits = "damage",
                    BaseIncrement = 100,
                    IncrementStep = 100,
                },
                [RepairableToolProgress.Repair] = new()
                {
                    IncrementUnits = "repairs",
                    BaseIncrement = 1,
                    IncrementStep = 1,
                }
            },
            GlobalMaxCredits = 75,
            StatName = "ats:bow-?-rangedDamageMult",
            Weapons = [ ToolDefinitions.Bow ],
        };

        public static readonly GenericCollectionUnlockedAttributeModifierDefinition Potter = new()
        {
            Id = "potter",
            SkillKey = "potter",
            PersistenceHeader = "POT",
            Name = "Potter",
            RequiredCollectionSize = 20,
            CollectedItemDescription = "items clayformed",
            CollectedItemCountKey = "sitPotterCount",
            TokenAllowList = ["*"],
            Trait = new(() => Traits.TraitDefinitions.Potter),
        };

        public static readonly GenericCollectionUnlockedAttributeModifierDefinition MasterCraftsman = new()
        {
            Id = "masterCraftsman",
            SkillKey = "mastercraftsman",
            PersistenceHeader = "MCR",
            Name = "MasterCraftsman",
            RequiredCollectionSize = 20,
            CollectedItemDescription = "items smithed",
            CollectedItemCountKey = "sitMasterCraftsmanCount",
            TokenAllowList = ["*"],
            Trait = new(() => Traits.TraitDefinitions.MasterCraftsman),
        };

        public static readonly ConcurrentDictionary<RepairableToolProgress, IncrementData> SmithingIncrementData = new()
        {
            [RepairableToolProgress.Usage] = new IncrementData
            {
                IncrementUnits = "smithing strikes",
                BaseIncrement = 100,
                IncrementStep = 100,
            }
        };

        public static readonly GenericRepairableToolAttributeModifierDefinition SmithingSpeed = new()
        {
            Id = "smithingSpeed",
            SkillKey = "smithing",
            Name = "Smithing Speed",
            Stat = "% smithing speed",
            LongDescription = "smithing speed",
            PersistenceHeader = "SMS",
            Tools = [ ToolDefinitions.Hammer ],
            IncrementData = SmithingIncrementData,
            GlobalMaxCredits = 100,
            StatName = "ats:handsmithingspeed"
        };

        public static readonly GenericRepairableToolAttributeModifierDefinition BitRecoveryRate = new()
        {
            Id = "bitRecoveryRate",
            SkillKey = "bitrecoveryrate",
            Name = "Bit Recovery Rate",
            Stat = "% bit recovery rate",
            LongDescription = "bit recovery rate",
            PersistenceHeader = "BRR",
            Tools = [ ToolDefinitions.Hammer ],
            IncrementData = SmithingIncrementData,
            GlobalMaxCredits = 100,
            StatName = "ats:bitrecoveryrate"
        };

        public static readonly GenericToolAttributeModifierDefinition HammerDamage = new()
        {
            Id = "hammerDamage",
            Name = "Hammer Damage",
            LongDescription = "hammer damage",
            Stat = "% hammer damage",
            SkillKey = "hammerdamage",
            PersistenceHeader = "HDM",
            Tools = [ ToolDefinitions.Hammer ],
            IncrementData = DamageIncrementData,
            GlobalMaxCredits = 100,
            StatName = "ats:hammer-?-meleeDamageMult",
            Weapons = [ ToolDefinitions.Hammer ],
        };

        public static readonly GenericToolAttributeModifierDefinition TemperingPowerLoss = new()
        {
            Id = "temperPowerLoss",
            Name = "Tempering Power Loss",
            LongDescription = "tempering power loss",
            Stat = "% tempering power loss",
            SkillKey = "temperpowerloss",
            PersistenceHeader = "TPL",
            Tools = [ ToolDefinitions.Hammer ],
            IncrementData = DamageIncrementData,
            GlobalMaxCredits = 10,
            StatName = "ats:temperingpowerlossrate",
            IsInverted = true,
            Weapons = [ ToolDefinitions.Hammer ],
        };

        public static readonly GenericToolAttributeModifierDefinition QuenchingShatter = new()
        {
            Id = "quenchShatter",
            Name = "Quenching Shatter Chance",
            LongDescription = "quenching shatter chance",
            Stat = "% quenching shatter chance reduction",
            SkillKey = "quenchshatter",
            PersistenceHeader = "QNS",
            Tools = [ ToolDefinitions.Hammer ],
            IncrementData = DamageIncrementData,
            GlobalMaxCredits = 5,
            StatName = "ats:quenchshatterrate",
            IsInverted = true,
            Weapons = [ ToolDefinitions.Hammer ],
        };

        public static readonly GenericToolAttributeModifierDefinition KnifeDamage = new()
        {
            Id = "knifeDamage",
            Name = "Knife Damage",
            LongDescription = "knife damage",
            Stat = "% knife damage",
            SkillKey = "knifedamage",
            PersistenceHeader = "KDM",
            Tools = [ ToolDefinitions.Knife ],
            IncrementData = DamageIncrementData,
            GlobalMaxCredits = 100,
            StatName = "ats:knife-?-meleeDamageMult",
            Weapons = [ ToolDefinitions.Knife ],
        };

        public static readonly GenericRepairableToolAttributeModifierDefinition KnifeDurability = new()
        {
            Id = "knifeDurability",
            SkillKey = "knifedurability",
            Name = "Knife Durability",
            Stat = "% knife durability loss reduction",
            LongDescription = "knife durability",
            PersistenceHeader = "KDR",
            Tools = [ ToolDefinitions.Knife ],
            IncrementData = new()
            {
                [RepairableToolProgress.Usage] = new()
                {
                    IncrementUnits = "damage",
                    BaseIncrement = 100,
                    IncrementStep = 100,
                },
                [RepairableToolProgress.Repair] = new()
                {
                    IncrementUnits = "repairs",
                    BaseIncrement = 1,
                    IncrementStep = 1,
                }
            },
            GlobalMaxCredits = 75,
            StatName = "ats:knife-?-reduceDurabilityLoss"
        };

        public static readonly GenericToolAttributeModifierDefinition CleaverDamage = new()
        {
            Id = "cleaverDamage",
            RequiredMod = new(() => ModDefinitions.Butchering),
            Name = "Cleaver Damage",
            LongDescription = "Cleaver damage",
            Stat = "% cleaver damage",
            SkillKey = "cleaverdamage",
            PersistenceHeader = "CDM",
            Tools = [ ToolDefinitions.Knife ],
            IncrementData = DamageIncrementData,
            GlobalMaxCredits = 100,
            StatName = "ats:aculinaryartillery:cleaver-?-meleeDamageMult",
            Weapons = [ ToolDefinitions.Knife ],
        };

        public static readonly GenericUnlockedAttributeModifierDefinition Culinary = new()
        {
            Id = "culinary",
            SkillKey = "culinary",
            PersistenceHeader = "CUL",
            Name = "Culinary",
            Trait = new(() => Traits.TraitDefinitions.Culinary)
        };

        public static readonly GenericUnlockedAttributeModifierDefinition WildernessExplorer = new()
        {
            Id = "wildernessExplorer",
            SkillKey = "wildernessexplorer",
            PersistenceHeader = "WEX",
            Name = "Wilderness Explorer",
            Trait = new(() => Traits.TraitDefinitions.WildernessExplorer)
        };

        public static readonly GenericLeveledAttributeModifierDefinition TemporalStabilityDamageReceived = new()
        {
            Id = "temporalStabilityDamage",
            SkillKey = "temporalstabilitydamage",
            PersistenceHeader = "TSD",
            Name = "Temporal Stability Damage",
            IsInverted = true,
            IncrementUnits = "seconds",
            LongDescription = "temporal instability damage received",
            Stat = "% temporal instability damage received",
            BaseIncrement = 300,
            IncrementStep = 60,
            GlobalMaxCredits = 80,
            StatName = "ats:temporalstabilitydamagereceived",
        };

        public static readonly GenericUnlockedAttributeModifierDefinition LactoseEnthusiast = new()
        {
            Id = "lactoseenthusiast",
            RequiredMod = new(() => ModDefinitions.ExoticMilk),
            SkillKey = "lactoseenthusiast",
            PersistenceHeader = "LEN",
            Name = "Lactose Enthusiast",
            Trait = new(() => Traits.TraitDefinitions.Culinary)
        };
    }
}
