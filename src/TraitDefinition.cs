using System;
using System.Collections.Generic;

namespace SeraphLeveling
{
    public record class TraitDefinition
    {
        public string Id { get; init; }
        public List<AttributeDefinition> Attributes { get; init; }
    }
}
