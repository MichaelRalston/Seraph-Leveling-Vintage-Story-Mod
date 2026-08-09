using System;
using System.Collections.Generic;

namespace SeraphLeveling
{
    public record class ModDefinition
    {
        public string ModId { get; init; }
        public List<CharacterClassDefinition> CharacterClasses { get; init; }
    }
}
