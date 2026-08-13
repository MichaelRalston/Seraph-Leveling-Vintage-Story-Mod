using System;
using SeraphLeveling.Data.Tools;

namespace SeraphLeveling.Data.Attributes
{
    public static class AttributeModifierDefinitions
    {
        public static readonly TinkererAttributeModifierDefinition Tinkerer = new()
        {
            Id = "tinkerer",
            SaveKey = "sitTinkererProgress",
            Description = "tinkerer",
            PersistenceHeader = "TNK",
            Name = "Tinkerer",
            ExtraTraitKey = "sittinkerermastery",
            UnlockedKey = "sitTinkererUnlocked",
            NotifyLangKey = "seraphleveling:message-tinkerer-unlock",
            Trait = new(() => Traits.TraitDefinitions.Tinkerer)
        };

        public static readonly TechnicalAttributeModifierDefinition Technical = new()
        {
            Id = "technical",
            SaveKey = "sitTechnicalProgress",
            Description = "technical",
            PersistenceHeader = "TEC",
            Name = "Technical",
            ExtraTraitKey = "sittechnicalmastery",
            UnlockedKey = "sitTechnicalUnlocked",
            NotifyLangKey = "seraphleveling:message-technical-unlock",
            GlobalMaxCredits = 5,
            CreditDescription = "translocators",
            Trait = new(() => Traits.TraitDefinitions.Technical),
        };

        public static readonly BowyerAttributeModifierDefinition Bowyer = new()
        {
            Id = "bowyer",
            SaveKey = "sitBowyerProgress",
            Description = "bowyer",
            PersistenceHeader = "BWY",
            Name = "Bowyer",
            ExtraTraitKey = "sitbowyermastery",
            UnlockedKey = "sitBowyerUnlocked",
            NotifyLangKey = "seraphleveling:message-bowyer-unlock",
            GlobalMaxCredits = 300,
            CreditDescription = "bow damage",
            WatchedCreditsAttributeKey = "sitBowyerBowDamage",
            Trait = new(() => Traits.TraitDefinitions.Bowyer),
        };

        public static readonly WalkingAttributeModifierDefinition WalkingSpeed = new()
        {
            Id = "walkingSpeed",
            SaveKey = "sitWalkingProgress",
            Description = "walking",
            PersistenceHeader = "SIW",
            SkillKey = "walking",
            Name = "Walking",
            Stat = "% speed",
            LongDescription = "walking speed",
            IncrementUnits = "blocks",
            BaseIncrement = 1000,
            IncrementStep = 1000,
            GlobalMaxCredits = 15,
            StatName = "walkspeed",
        };

        public static readonly HungerAttributeModifierDefinition HungerRate = new()
        {
            Id = "hungerRate",
            SaveKey = "sitHungerProgress",
            Description = "hunger",
            PersistenceHeader = "SIH",
            SkillKey = "hunger",
            Name = "Hunger",
            Direction = "-",
            IncrementUnits = "seconds",
            LongDescription = "hunger rate",
            Stat = "% hunger rate",
            BaseIncrement = 300,
            IncrementStep = 60,
            GlobalMaxCredits = 25,
            StatName = "hungerrate",
        };

        public static readonly MiningAttributeModifierDefinition MiningSpeed = new()
        {
            Id = "miningSpeed",
            SaveKey = "sitMiningProgress",
            Description = "mining",
            SkillKey = "mining",
            Name = "Mining",
            Stat = "% mining speed",
            LongDescription = "mining speed",
            PersistenceHeader = "SIT",
            PersistenceVersion = 4,
            Tool = ToolDefinitions.Pickaxe,
            BaseIncrement = 100,
            IncrementStep = 100,
            IncrementUnits = "blocks",
            GlobalMaxCredits = 50,
            StatName = "miningSpeedMul"
        };

        public static readonly ImproviserAttributeModifierDefinition Improviser = new()
        {
            Id = "improviser",
            SaveKey = "sitImproviserProgress",
            Description = "improviser",
            PersistenceHeader = "IMP",
            Name = "Improviser",
            ExtraTraitKey = "sitimprovisermastery",
            UnlockedKey = "sitImproviserUnlocked",
            NotifyLangKey = "seraphleveling:message-improviser-unlock",
            GlobalMaxCredits = 300,
            CreditDescription = "thrown rock damage",
            WatchedCreditsAttributeKey = "sitImproviserRockDamage",
            Trait = new(() => Traits.TraitDefinitions.Improviser),
        };

        public static readonly ClothierAttributeModifierDefinition Clothier = new()
        {
            Id = "clothier",
            SaveKey = "sitClothierProgress",
            Description = "clothier",
            PersistenceHeader = "CLT",
            Name = "Clothier",
            ExtraTraitKey = "sitclothiermastery",
            UnlockedKey = "sitClothierUnlocked",
            NotifyLangKey = "seraphleveling:message-clothier-unlocked",
            RequiredCollectionSize = 20,
            CollectedItemDescription = "clothes worn",
            CollectedItemCountKey = "sitClothierCount",
            TokenAllowList = [ "clothes-", "shirt-", "trousers-", "dress-", "hat-", "cape-", "cloak-", "jacket-", "vest-", "skirt-", "gloves-", "boots-", "shoes-", "headband-", "mask-", "scarf-" ],
            Trait = new(() => Traits.TraitDefinitions.Clothier),
        };
        
        public static readonly RangedDamageAttributeModifierDefinition RangedDamage = new ()
        {
            Id = "rangedDamage",
            SaveKey = "sitRangedProgress",
            Description = "ranged",
            Name = "Ranged",
            LongDescription = "ranged damage",
            Stat = "% ranged damage",
            SkillKey = "ranged",
            PersistenceHeader = "SIR",
            PersistenceVersion = 2,
            Tool = ToolDefinitions.Weapon,
            IncrementUnits = "damage",
            BaseIncrement = 100,
            IncrementStep = 100,
            GlobalMaxCredits = 50,
            StatName = "rangedWeaponsDamage",
        };

        public static readonly RangedDamageAttributeModifierDefinition RangedAccuracy = new ()
        {
            Id = "rangedAccuracy",
            SaveKey = "sitRangedAccuracyProgress",
            Description = "ranged",
            Name = "Ranged",
            LongDescription = "ranged accuracy",
            Stat = "% ranged accuracy",
            SkillKey = "rangedaccuracy",
            PersistenceHeader = "SIR",
            PersistenceVersion = 2,
            Tool = ToolDefinitions.Weapon,
            IncrementUnits = "damage",
            BaseIncrement = 100,
            IncrementStep = 100,
            GlobalMaxCredits = 50,
            StatName = "rangedWeaponsAcc",
        };

        public static readonly RangedDamageAttributeModifierDefinition RangedDistance = new()
        {
            Id = "rangedDistance",
            SaveKey = "sitRangedDistanceProgress",
            Description = "ranged",
            Name = "Ranged",
            LongDescription = "ranged distance",
            Stat = "% ranged distance",
            SkillKey = "rangeddistance",
            PersistenceHeader = "SIR",
            PersistenceVersion = 2,
            Tool = ToolDefinitions.Weapon,
            IncrementUnits = "damage",
            BaseIncrement = 100,
            IncrementStep = 100,
            GlobalMaxCredits = 50,
            StatName = "bowDrawingStrength",
        };
    }
}
