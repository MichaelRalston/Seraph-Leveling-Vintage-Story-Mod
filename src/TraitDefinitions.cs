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
    }
}
