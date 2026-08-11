using System;
using System.Collections.Generic;
using SeraphLeveling.Data.Attributes;

namespace SeraphLeveling.Data.Traits
{
    public record class TraitDefinition
    {
        public required string Id { get; init; }
        public required List<(ISaveableAttribute, int)> Attributes { get; init; }
        public List<IRequiredAttribute> Requirements { get; init; } = [];
    }
}
