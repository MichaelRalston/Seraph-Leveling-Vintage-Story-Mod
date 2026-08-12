using System;
using SeraphLeveling.Data.Attributes;

namespace SeraphLeveling.Data.Traits
{
    public static class TraitDefinitions
    {
        public static readonly TraitDefinition Focused = new()
        {
            Id = "focused",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Resourceful = new()
        {
            Id = "resourceful",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Fleetfooted = new()
        {
            Id = "fleetfooted",
            Attributes = [ 
                (AttributeModifierDefinitions.WalkingSpeed, 10)
            ]
        };

        public static readonly TraitDefinition Bowyer = new()
        {
            Id = "focused",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition Farsighted = new()
        {
            Id = "farsighted",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Claustrophobic = new()
        {
            Id = "claustrophobic",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Forager = new()
        {
            Id = "forager",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Pilferer = new()
        {
            Id = "pilferer",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Furtive = new()
        {
            Id = "furtive",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Improviser = new()
        {
            Id = "improviser",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition Frail = new()
        {
            Id = "frail",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Nervous = new()
        {
            Id = "nervous",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Precise = new()
        {
            Id = "precise",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Technical = new()
        {
            Id = "technical",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition Tinkerer = new()
        {
            Id = "tinkerer",
            Attributes = [
                (AttributeModifierDefinitions.Tinkerer, 1)
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition Soldier = new()
        {
            Id = "soldier",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Hardy = new()
        {
            Id = "hardy",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Merciless = new()
        {
            Id = "precise",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition Ravenous = new()
        {
            Id = "ravenous",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Nearsighted = new()
        {
            Id = "nearsighted",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Heavyhanded = new()
        {
            Id = "heavyhanded",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Clothier = new()
        {
            Id = "clothier",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition Mender = new()
        {
            Id = "mender",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Civil = new()
        {
            Id = "civil",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Weak = new()
        {
            Id = "weak",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Kind = new()
        {
            Id = "kind",
            Attributes = [
            ]
        };
    }
}
