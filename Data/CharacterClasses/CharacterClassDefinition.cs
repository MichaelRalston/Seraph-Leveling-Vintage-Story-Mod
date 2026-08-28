using System;
using System.Collections.Generic;
using SeraphLeveling.Data.Traits;

namespace SeraphLeveling.Data.CharacterClasses
{
    public record class CharacterClassDefinition
    {
        public required string Id { get; init; }
        public required List<TraitDefinition> Traits { get; init; }
    }
}
