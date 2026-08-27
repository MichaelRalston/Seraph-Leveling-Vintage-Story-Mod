using System;
using System.Collections.Generic;
using SeraphLeveling.Data.CharacterClasses;

namespace SeraphLeveling.Data.Mods
{
    public record class ModDefinition
    {
        public required string ModId { get; init; }
        public required List<CharacterClassDefinition> CharacterClasses { get; init; }
    }
}
