using System;

namespace SeraphLeveling
{
    public record class AttributeModifierDefinition
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string SaveKey { get; init; }
        public required string Description { get; init; }
        public required string LongDescription { get; init; }
        public required string Stat { get; init; }
    }
}
