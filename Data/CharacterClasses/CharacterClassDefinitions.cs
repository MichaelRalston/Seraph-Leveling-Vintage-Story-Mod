using System;
using SeraphLeveling.Data.Traits;

namespace SeraphLeveling.Data.CharacterClasses
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

        public static readonly CharacterClassDefinition Clockmaker = new()
        {
            Id = "clockmaker",
            Traits = [
                TraitDefinitions.Tinkerer
            ]
        };
    }
}
