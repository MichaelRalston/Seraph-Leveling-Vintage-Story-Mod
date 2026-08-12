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
            ExtraTraitKey = SeraphLevelingModSystem.TINKERER_TRAIT_CODE,
            UnlockedKey = SeraphLevelingModSystem.WATCHED_TINKERER_UNLOCKED,
            NotifyLangKey = "seraphleveling:message-tinkerer-unlock"
        };

        public static readonly TechnicalAttributeModifierDefinition Technical = new()
        {
            Id = "technical",
            SaveKey = "sitTechnicalProgress",
            Description = "technical",
            PersistenceHeader = "TEC",
            Name = "Technical",
            ExtraTraitKey = SeraphLevelingModSystem.TECHNICAL_TRAIT_CODE,
            UnlockedKey = SeraphLevelingModSystem.WATCHED_TECHNICAL_UNLOCKED,
            NotifyLangKey = "seraphleveling:message-technical-unlock",
            GlobalMaxCredits = SeraphLevelingModSystem.TechnicalRequiredTranslocatorRepairs,
            CreditDescription = "translocators",
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
            BaseIncrement = SeraphLevelingModSystem.BaseSecondsPerIncrement,
            IncrementStep = SeraphLevelingModSystem.HungerIncrementStep,
            GlobalMaxCredits = SeraphLevelingModSystem.MaxHungerReductionPercent,
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
            GlobalMaxCredits = SeraphLevelingModSystem.MaxMiningSpeedPercent,
            StatName = "miningSpeedMul"
        };
    }
}
