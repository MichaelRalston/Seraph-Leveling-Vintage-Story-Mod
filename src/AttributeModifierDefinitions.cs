using System;

namespace SeraphLeveling
{
    public static class AttributeModifierDefinitions
    {
        public static readonly AttributeModifierDefinition WalkingSpeed = new LeveledAttributeModifierDefinition()
        {
            Id = "walkingSpeed",
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
