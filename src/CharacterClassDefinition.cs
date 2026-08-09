using System;
using System.Collections.Generic;

namespace SeraphLeveling
{
    public record class CharacterClassDefinition
    {
        public string Id { get; init; }
        public List<TraitDefinition> Traits { get; init; }
    }
}
