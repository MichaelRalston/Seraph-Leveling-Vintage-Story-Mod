using System;
using SeraphLeveling.Data.Attributes;

namespace SeraphLeveling.Data.Traits
{
    public static class TraitDefinitions
    {
        public static readonly TraitDefinition Focused = new()
        {
            Id = "focused",
            Attributes = new()
            {
                [AttributeModifierDefinitions.RangedDamage] = 20,
                [AttributeModifierDefinitions.RangedAccuracy] = 30,
                [AttributeModifierDefinitions.RangedDistance] = 20,
            }
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
            PlainTraitNameKey = "seraphleveling:trait-sitwalkingmastery",
            MergeWithVanilla = true,
            Attributes = new()
            {
                [AttributeModifierDefinitions.WalkingSpeed] = 10,
            }
        };

        public static readonly TraitDefinition Bowyer = new()
        {
            Id = "bowyer",
            Attributes = new()
            {
                [AttributeModifierDefinitions.Bowyer] = 1,
            },
            Requirements = [
                new RequiredUnlockedAttribute(AttributeModifierDefinitions.Bowyer)
                // TODO Generic ranged damage 10%
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
            Attributes = new()
            {
                [AttributeModifierDefinitions.MiningSpeed] = -10,
            },
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
            Attributes = new()
            {
                [AttributeModifierDefinitions.Furtive] = 35,
            },
        };

        public static readonly TraitDefinition Improviser = new()
        {
            Id = "improviser",
            Attributes = new()
            {
                [AttributeModifierDefinitions.Improviser] = 1,
            },
            Requirements = [
                new RequiredUnlockedAttribute(AttributeModifierDefinitions.Improviser)
            ]
        };

        public static readonly TraitDefinition Frail = new()
        {
            Id = "frail",
            Attributes = new()
            {
                [AttributeModifierDefinitions.RangedDistance] = -25,
            },
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
            Attributes = new()
            {
                [AttributeModifierDefinitions.Technical] = 1,
            },
            Requirements = [
                new RequiredUnlockedAttribute(AttributeModifierDefinitions.Technical)
            ]
        };

        public static readonly TraitDefinition Tinkerer = new()
        {
            Id = "tinkerer",
            Attributes = new()
            {
                [AttributeModifierDefinitions.Tinkerer] = 1,
            },
            Requirements = [
                new RequiredUnlockedAttribute(AttributeModifierDefinitions.Technical),
                // TODO Precise 10%
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
            Attributes = new()
            {
                [AttributeModifierDefinitions.HungerRate] = -30,
            },
        };

        public static readonly TraitDefinition Nearsighted = new()
        {
            Id = "nearsighted",
            Attributes = new()
            {
                [AttributeModifierDefinitions.RangedDamage] = -15,
            },
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
            Attributes = new()
            {
                [AttributeModifierDefinitions.Clothier] = 1,
            },
            Requirements = [
                new RequiredUnlockedAttribute(AttributeModifierDefinitions.Clothier)
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
            Attributes = new()
            {
                [AttributeModifierDefinitions.MiningSpeed] = -10,
            },
        };

        public static readonly TraitDefinition Kind = new()
        {
            Id = "kind",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Carpenter = new()
        {
            Id = "carpenter",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition Lumberjack = new()
        {
            Id = "lumberjack",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition TreeWhisperer = new()
        {
            Id = "treewhisperer",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition HeavyFooted = new()
        {
            Id = "heavyfooted",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Mason = new()
        {
            Id = "mason",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition InteriorDesigner = new()
        {
            Id = "interiordesigner",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition Potter = new()
        {
            Id = "potter",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition Technician = new()
        {
            Id = "technician",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition SiltSeeker = new()
        {
            Id = "siltseeker",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Townie = new()
        {
            Id = "townie",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Agoraphobic = new()
        {
            Id = "agoraphobic",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Alchemist = new()
        {
            Id = "alchemist",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition Propagator = new()
        {
            Id = "propagator",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition Naturalist = new()
        {
            Id = "naturalist",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Medic = new()
        {
            Id = "medic",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition MasterCraftsman = new()
        {
            Id = "mastercraftsman",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition Blacksmith = new()
        {
            Id = "blacksmith",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Armorer = new()
        {
            Id = "armourer", // not a typo
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Engineer = new()
        {
            Id = "engineer",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Detonator = new()
        {
            Id = "detonator",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition Stonespeaker = new()
        {
            Id = "stonespeaker",
            Attributes = new()
            {
                [AttributeModifierDefinitions.MiningSpeed] = 220,
            },
        };

        public static readonly TraitDefinition CaveExplorer = new()
        {
            Id = "caveexplorer",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition EarthSinger = new()
        {
            Id = "earthsinger",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Rancher = new()
        {
            Id = "rancher",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition WildernessExplorer = new()
        {
            Id = "wildernessexplorer",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition Butcher = new()
        {
            Id = "butcher",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Ranger = new()
        {
            Id = "ranger",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition WellAdjusted = new()
        {
            Id = "welladjusted",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Bulwark = new()
        {
            Id = "bulwark",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition ArmyMedic = new()
        {
            Id = "armymedic",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition StrongArmed = new()
        {
            Id = "strongarmed",
            Attributes = new()
            {
                [AttributeModifierDefinitions.MiningSpeed] = 100,
            },
        };

        public static readonly TraitDefinition HeavyHands = new()
        {
            Id = "heavyhands",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Culinary = new()
        {
            Id = "culinary",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition Allumette = new()
        {
            Id = "allumette",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Weaver = new()
        {
            Id = "weaver",
            Attributes = [
            ],
            Requirements = [
            ]
        };

        public static readonly TraitDefinition Sacrificial = new()
        {
            Id = "sacrificial",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Insane = new()
        {
            Id = "insane",
            Attributes = [
            ]
        };

        public static readonly TraitDefinition Nudist = new()
        {
            Id = "nudist",
            Attributes = [
            ]
        };
    }
}
