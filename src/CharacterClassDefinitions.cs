using System;

namespace SeraphLeveling
{
    public static class CharacterClassDefinitions
    {
        public static readonly CharacterClassDefinition Commoner = new()
        {
            Id = "commoner",
            Traits = []
        };

        public static readonly CharacterClassDefinition Hunter = new()
        {
            Id = "hunter",
            Traits = [
                TraitDefinitions.Fleetfooted
            ]
        };
    }
}
