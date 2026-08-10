using System;
using System.Collections.Generic;
using SeraphLeveling.Data.Attributes;

namespace SeraphLeveling.Data.Traits
{
    public record class TraitDefinition
    {
        public string Id { get; init; }
        public List<ISaveableAttribute> Attributes { get; init; }
    }
}
