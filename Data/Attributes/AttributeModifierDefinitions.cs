using System;

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
            UnlockedKey = SeraphLevelingModSystem.WATCHED_TINKERER_UNLOCKED
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
            BaseIncrement = SeraphLevelingModSystem.BaseBlocksWalkedPerIncrement,
            IncrementStep = SeraphLevelingModSystem.WalkingIncrementStep,
            GlobalMaxCredits = SeraphLevelingModSystem.MaxWalkingSpeedPercent
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
            Tool = new()
            {
                Name = "pickaxe",
                BaseIncrement = SeraphLevelingModSystem.BaseBlocksPerIncrement,
                IncrementStep = SeraphLevelingModSystem.IncrementStep,
                IncrementUnits = "blocks"
            },
            GlobalMaxCredits = SeraphLevelingModSystem.MaxMiningSpeedPercent
        };
    }
}
