using System;
using System.Collections.Generic;
using SeraphLeveling.Data.CharacterClasses;

namespace SeraphLeveling.Data.Mods
{
    public record class ModDefinition
    {
        public string ModId { get; init; }
        public List<CharacterClassDefinition> CharacterClasses { get; init; }
    }
}
