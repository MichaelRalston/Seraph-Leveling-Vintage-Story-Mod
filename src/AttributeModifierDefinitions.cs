using System;

namespace SeraphLeveling
{
    public static class AttributeModifierDefinitions
    {
        public static readonly AttributeModifierDefinition WalkingSpeed = new()
        {
            Id = "walkingSpeed",
            Name = "Walking",
            SaveKey = "sitWalkingProgress",
            Description = "walking",
            LongDescription = "walking speed",
            Stat = "% speed"
        };
    }
}
