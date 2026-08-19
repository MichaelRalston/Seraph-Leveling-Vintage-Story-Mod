using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using SeraphLeveling.Data.Tools;

namespace SeraphLeveling.Data.Attributes
{
    public static class AttributeModifierDefinitions
    {
        public static readonly TinkererAttributeModifierDefinition Tinkerer = new()
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
        };

        public static readonly MercilessAttributeModifierDefinition Merciless = new()
        {
            Id = "merciless",
            SkillKey = "merciless",
            PersistenceHeader = "MRC",
            Name = "Merciless",
            Trait = new(() => Traits.TraitDefinitions.Merciless)
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

        public static readonly GenericLeveledAttributeModifierDefinition LootingBonus = new()
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

        public static readonly MiningAttributeModifierDefinition MiningSpeed = new()
        {
            Id = "miningSpeed",
            SkillKey = "mining",
            Name = "Mining",
            Stat = "% mining speed",
            LongDescription = "mining speed",
            PersistenceHeader = "SIT",
            Tool = ToolDefinitions.Pickaxe,
            IncrementData = MiningIncrementData,
            GlobalMaxCredits = 50,
            StatName = "miningSpeedMul"
        };

        public static readonly MiningAttributeModifierDefinition StoneDropRate = new()
        {
            Id = "stoneDropRate",
            SkillKey = "stonerate",
            Name = "StoneDropRate",
            Stat = "% bonus stone drop rate",
            LongDescription = "stone drop rate",
            PersistenceHeader = "SDR",
            Tool = ToolDefinitions.Pickaxe,
            IncrementData = MiningIncrementData,
            GlobalMaxCredits = 300,
            StatName = "sacredlib:stoneDropRate"
        };

        public static readonly MiningAttributeModifierDefinition OreDropRate = new()
        {
            Id = "oreDropRate",
            SkillKey = "orerate",
            Name = "OreDropRate",
            Stat = "% bonus ore drop rate",
            LongDescription = "ore drop rate",
            PersistenceHeader = "SDR",
            Tool = ToolDefinitions.Pickaxe,
            IncrementData = MiningIncrementData,
            GlobalMaxCredits = 300,
            StatName = "sacredlib:oreDropRate"
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

        public static readonly AxeAttributeModifierDefinition WoodDropRate = new()
        {
            Id = "woodDropRate",
            SkillKey = "woodrate",
            Name = "WoodDropRate",
            Stat = "% bonus wood drop rate",
            LongDescription = "wood drop rate",
            PersistenceHeader = "WDR",
            Tool = ToolDefinitions.Axe,
            IncrementData = TreeIncrementData,
            GlobalMaxCredits = 100,
            StatName = "sacredlib:woodDropRate"
        };

        public static readonly AxeAttributeModifierDefinition SeedDropRate = new()
        {
            Id = "seedDropRate",
            SkillKey = "seedrate",
            Name = "SeedDropRate",
            Stat = "% bonus tree seed drop rate",
            LongDescription = "tree seed drop rate",
            PersistenceHeader = "SDR",
            Tool = ToolDefinitions.Axe,
            IncrementData = TreeIncrementData,
            GlobalMaxCredits = 1000,
            StatName = "sacredlib:treeseedDropRate"
        };

        public static readonly AxeAttributeModifierDefinition StickDropRate = new()
        {
            Id = "stickDropRate",
            SkillKey = "stickrate",
            Name = "StickDropRate",
            Stat = "% bonus stick drop rate",
            LongDescription = "stick drop rate",
            PersistenceHeader = "WDR",
            Tool = ToolDefinitions.Axe,
            IncrementData = TreeIncrementData,
            GlobalMaxCredits = 300,
            StatName = "sacredlib:stickDropRate"
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
        };

        public static readonly ClothierAttributeModifierDefinition Clothier = new()
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
            Tool = ToolDefinitions.Weapon,
            IncrementData = DamageIncrementData,
            GlobalMaxCredits = 50,
            StatName = "rangedWeaponsDamage",
        };

        public static readonly DamageAttributeModifierDefinition RangedAccuracy = new()
        {
            Id = "rangedAccuracy",
            Name = "RangedAccuracy",
            LongDescription = "ranged accuracy",
            Stat = "% ranged accuracy",
            SkillKey = "rangedaccuracy",
            PersistenceHeader = "SIR",
            Tool = ToolDefinitions.Weapon,
            IncrementData = DamageIncrementData,
            GlobalMaxCredits = 50,
            StatName = "rangedWeaponsAcc",
        };

        public static readonly DamageAttributeModifierDefinition RangedDistance = new()
        {
            Id = "rangedDistance",
            Name = "RangedDistance",
            LongDescription = "ranged distance",
            Stat = "% ranged distance",
            SkillKey = "rangeddistance",
            PersistenceHeader = "SIR",
            Tool = ToolDefinitions.Weapon,
            IncrementData = DamageIncrementData,
            GlobalMaxCredits = 50,
            StatName = "bowDrawingStrength",
        };

        public static readonly DamageAttributeModifierDefinition MeleeDamage = new()
        {
            Id = "meleeDamage",
            Name = "Melee",
            LongDescription = "melee damage",
            Stat = "% melee damage",
            SkillKey = "melee",
            PersistenceHeader = "SIM",
            Tool = ToolDefinitions.Weapon,
            IncrementData = DamageIncrementData,
            GlobalMaxCredits = 50,
            StatName = "meleeWeaponsDamage",
        };

        public static readonly DamageAttributeModifierDefinition Precise = new()
        {
            Id = "precise",
            Name = "Precise",
            LongDescription = "mechanical damage",
            Stat = "% mechanical damage",
            SkillKey = "precise",
            PersistenceHeader = "PRC",
            Tool = ToolDefinitions.Weapon,
            IncrementData = DamageIncrementData,
            GlobalMaxCredits = 30,
            StatName = "mechanicalsDamage",
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
            Tool = ToolDefinitions.Armor,
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
            Tool = ToolDefinitions.Armor,
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
            Tool = ToolDefinitions.Armor,
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
            Tool = ToolDefinitions.Armor,
            IncrementData = ArmorWornIncrementData,
            GlobalMaxCredits = 25,
        };

        public static readonly CarpenterAttributeModifierDefinition Carpenter = new()
        {
            Id = "carpenter",
            SkillKey = "carpenter",
            PersistenceHeader = "CRP",
            Name = "Carpenter",
            GlobalMaxCredits = 120,
            CreditDescription = "boards",
            WatchedCreditsAttributeKey = "sitCarpenterBoards",
            Trait = new(() => Traits.TraitDefinitions.Carpenter),
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

        public static readonly AxeAttributeModifierDefinition TreeChoppingSpeed = new()
        {
            Id = "treeChoppingSpeed",
            SkillKey = "treechopping",
            Name = "Tree Chopping",
            Stat = "% chopping speed",
            LongDescription = "chopping speed",
            PersistenceHeader = "TRC",
            Tool = ToolDefinitions.Axe,
            IncrementData = TreeChoppingIncrementData,
            GlobalMaxCredits = 150,
            StatName = "ats:wood|axe-?-harvestSpeed"
        };

        public static readonly AxeAttributeModifierDefinition AxeDamage = new()
        {
            Id = "axeDamage",
            Name = "Axe Damage",
            LongDescription = "axe damage",
            Stat = "% axe damage",
            SkillKey = "axedamage",
            PersistenceHeader = "XDM",
            Tool = ToolDefinitions.Axe,
            IncrementData = TreeChoppingIncrementData,
            GlobalMaxCredits = 100,
            StatName = "ats:axe-?-meleeDamageMult",
        };

        public static readonly AxeAttributeModifierDefinition AxeDurability = new()
        {
            Id = "axeDurability",
            Name = "AxeDurability",
            Stat = "% axe durability bonus",
            SkillKey = "axedurability",
            PersistenceHeader = "XDU",
            IsInverted = true,
            InvertedOnDisplay = false,
            StatName = "axeDurabilityLoss",
            Tool = ToolDefinitions.Axe,
            IncrementData = TreeChoppingIncrementData,
            GlobalMaxCredits = 75,
        };
    }
}
