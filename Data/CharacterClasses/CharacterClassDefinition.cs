using System;
using System.Collections.Generic;
using SeraphLeveling.Data.Traits;

namespace SeraphLeveling.Data.CharacterClasses
{
    public record class CharacterClassDefinition
    {
        public string Id { get; init; }
        public List<TraitDefinition> Traits { get; init; }
    }
}
