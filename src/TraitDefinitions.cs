using System;

namespace SeraphLeveling
{
    public static class TraitDefinitions
    {
        public static readonly TraitDefinition Fleetfooted = new()
        {
            Id = "fleetfooted",
            Attributes = [ 
                AttributeModifierDefinitions.WalkingSpeed
            ]
        };

        public static readonly TraitDefinition Tinkerer = new()
        {
            Id = "tinkerer",
            Attributes = [
                AttributeModifierDefinitions.Tinkerer
            ]
        };
    }
}
