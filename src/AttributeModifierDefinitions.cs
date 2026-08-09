using System;

namespace SeraphLeveling
{
    public static class AttributeModifierDefinitions
    {
        public static readonly AttributeModifierDefinition WalkingSpeed = new LeveledAttributeModifierDefinition()
        {
            Id = "walkingSpeed",
            ProgressDataFactory = (def, version) => new LeveledAttributeModifierProgressData<float>(def, version),
            SaveKey = "sitWalkingProgress",
            Description = "walking",
            PersistenceHeader = "SIW",
            Name = "Walking",
            Stat = "% speed",
            LongDescription = "walking speed",
            GlobalMaxCredits = SeraphLevelingModSystem.MaxWalkingSpeedPercent
        };
    }
}
