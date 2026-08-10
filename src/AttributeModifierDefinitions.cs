using System;

namespace SeraphLeveling
{
    public static class AttributeModifierDefinitions
    {
        public static readonly UnlockedAttributeModifierDefinition Tinkerer = new()
        {
            Id = "tinkerer",
            SaveKey = "sitTinkererProgress",
            Description = "tinkerer",
            PersistenceHeader = "TNK",
            Name = "Tinkerer",
            ExtraTraitKey = SeraphLevelingModSystem.TINKERER_TRAIT_CODE
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
    }
}
