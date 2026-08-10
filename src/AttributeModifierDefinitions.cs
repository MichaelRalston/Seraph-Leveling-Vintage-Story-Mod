using System;

namespace SeraphLeveling
{
    public static class AttributeModifierDefinitions
    {
        public static readonly AttributeModifierDefinition<LeveledAttributeModifierDefinition> WalkingSpeed = new LeveledAttributeModifierDefinition()
        {
            Id = "walkingSpeed",
            ProgressDataFactory = (def, version) => new LeveledAttributeModifierProgressData((LeveledAttributeModifierDefinition)def, version),
            SaveKey = "sitWalkingProgress",
            Description = "walking",
            PersistenceHeader = "SIW",
            SkillKey = "walking",
            Name = "Walking",
            Stat = "% speed",
            LongDescription = "walking speed",
            BaseIncrement = SeraphLevelingModSystem.BaseBlocksWalkedPerIncrement,
            IncrementStep = SeraphLevelingModSystem.WalkingIncrementStep,
            GlobalMaxCredits = SeraphLevelingModSystem.MaxWalkingSpeedPercent
        };
    }
}
