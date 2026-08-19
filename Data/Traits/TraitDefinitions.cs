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
                IAttributeModifier.Bonus(AttributeModifierDefinitions.RangedDamage, 20),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.RangedAccuracy, 30),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.RangedDistance, 20),
            ],
        };

        public static readonly TraitDefinition Resourceful = new()
        {
            Id = "resourceful",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.AnimalDropRate, 10),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.AnimalHarvestRate, 25),
            ],
        };

        public static readonly TraitDefinition Fleetfooted = new()
        {
            Id = "fleetfooted",
            PlainTraitNameKey = "seraphleveling:trait-sitwalkingmastery",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.WalkingSpeed, 10),
            ],
        };

        public static readonly TraitDefinition Bowyer = new()
        {
            Id = "bowyer",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.Bowyer, 1, [
                    new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Bowyer },
                    new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.RangedDamage, ThresholdPercentage = 10 },
                ])
            ],
        };

        public static readonly TraitDefinition Farsighted = new()
        {
            Id = "farsighted",
            Attributes = [
                IAttributeModifier.Penalty(AttributeModifierDefinitions.MeleeDamage, 15, [ new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.MeleeDamage, ThresholdPercentage = 0 }]),
            ],
        };

        public static readonly TraitDefinition Claustrophobic = new()
        {
            Id = "claustrophobic",
            Attributes = [
                IAttributeModifier.PenaltyOffset(AttributeModifierDefinitions.ClaustrophobicOre, 1, AttributeModifierDefinitions.ClaustrophobicOrePenalty, [
                    new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.MiningSpeed, ThresholdPercentage = 0 },
                ]),
                IAttributeModifier.Penalty(AttributeModifierDefinitions.MiningSpeed, 10, [ new LeveledAttributeMinimumRequirement() { Attribute = AttributeModifierDefinitions.MiningSpeed, ThresholdPercentage = 0 }]),
            ],
        };

        public static readonly TraitDefinition Forager = new()
        {
            Id = "forager",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.LootingBonus, 10),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.WildCropDropRate, 20),
            ]
        };

        public static readonly TraitDefinition Pilferer = new()
        {
            Id = "pilferer",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.GearDropRate, 10),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.VesselDropRate, 15),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.WholeVesselRate, 12),
            ]
        };

        public static readonly TraitDefinition Furtive = new()
        {
            Id = "furtive",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.Furtive, 35),
            ],
        };

        public static readonly TraitDefinition Improviser = new()
        {
            Id = "improviser",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.Improviser, 1, [ new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Improviser } ])
            ],
        };

        public static readonly TraitDefinition Frail = new()
        {
            Id = "frail",
            Attributes = [
                IAttributeModifier.PenaltyOffset(AttributeModifierDefinitions.FrailHealthOffset, 1, AttributeModifierDefinitions.FrailHealthPenalty, [
                    new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.RangedDistance, ThresholdPercentage = 0 },
                ]),
                IAttributeModifier.Penalty(AttributeModifierDefinitions.RangedDistance, 25, [ new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.RangedDistance, ThresholdPercentage = 0 }]),
            ],
        };

        public static readonly TraitDefinition Nervous = new()
        {
            Id = "nervous",
            Attributes = [
                IAttributeModifier.Penalty(AttributeModifierDefinitions.MeleeDamage, 15, [ new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.MeleeDamage, ThresholdPercentage = 0 }]),
            ],
        };

        public static readonly TraitDefinition Precise = new()
        {
            Id = "precise",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.Precise, 25),
            ],
        };

        public static readonly TraitDefinition Technical = new()
        {
            Id = "technical",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.Technical, 1, [ new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Technical } ])
            ],
        };

        public static readonly TraitDefinition Tinkerer = new()
        {
            Id = "tinkerer",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.Tinkerer, 1, [
                    new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Technical },
                    new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.Precise, ThresholdPercentage = 10 },
                ])
            ],
        };

        public static readonly TraitDefinition Soldier = new()
        {
            Id = "soldier",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.MeleeDamage, 30),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.ArmorDurability, 15),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.ArmorWalkSpeed, 25),
            ],
        };

        public static readonly TraitDefinition Hardy = new()
        {
            Id = "hardy",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.HardyHealth, 1, [
                    new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.MiningSpeed, ThresholdPercentage = 10 },
                    new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.ArmorDurability, ThresholdPercentage = 10 }
                ]),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.MiningSpeed, 10),
            ]
        };

        public static readonly TraitDefinition Merciless = new()
        {
            Id = "merciless",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.Merciless, 1, [
                    new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.MeleeDamage, ThresholdPercentage = 15 },
                    new LeveledAttributeMinimumRequirement{ Attribute = AttributeModifierDefinitions.ArmorDurability , ThresholdPercentage = 10}
                ])
            ],
        };

        public static readonly TraitDefinition HungerMastery = new()
        {
            // This trait doesn't actually exist in vanilla, but we still want to display it for players that have earned the attribute
            Id = "hungermastery",
            DynamicTraitHeaderKey = "seraphleveling:trait-hungermastery",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.HungerRate, 1),
            ],
        };

        public static readonly TraitDefinition Ravenous = new()
        {
            Id = "ravenous",
            Attributes = [
                IAttributeModifier.Penalty(AttributeModifierDefinitions.HungerRate, 30, [ new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.HungerRate, ThresholdPercentage = 0 }])
            ],
        };

        public static readonly TraitDefinition Nearsighted = new()
        {
            Id = "nearsighted",
            Attributes = [
                IAttributeModifier.Penalty(AttributeModifierDefinitions.RangedDamage, 15, [ new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.RangedDamage, ThresholdPercentage = 0 }])
            ],
        };

        public static readonly TraitDefinition Heavyhanded = new()
        {
            Id = "heavyhanded",
            Attributes = [
                IAttributeModifier.Penalty(AttributeModifierDefinitions.VesselDropRate, 10, [ new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.VesselDropRate, ThresholdPercentage = 0 }]),
                IAttributeModifier.Penalty(AttributeModifierDefinitions.LootingBonus, 15, [ new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.LootingBonus, ThresholdPercentage = 0 }]),
                IAttributeModifier.Penalty(AttributeModifierDefinitions.WildCropDropRate, 20, [ new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.WildCropDropRate, ThresholdPercentage = 0 }]),
            ]
        };

        public static readonly TraitDefinition Clothier = new()
        {
            Id = "clothier",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.Clothier, 1, [ new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Clothier } ])
            ],
        };

        public static readonly TraitDefinition Mender = new()
        {
            Id = "mender",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.Mender, 25)
            ]
        };

        public static readonly TraitDefinition Civil = new()
        {
            Id = "civil",
            Attributes = [
                IAttributeModifier.Penalty(AttributeModifierDefinitions.LootingBonus, 10, [ new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.LootingBonus, ThresholdPercentage = 0 } ]),
            ]
        };

        public static readonly TraitDefinition Weak = new()
        {
            Id = "weak",
            Attributes = [
                IAttributeModifier.PenaltyOffset(AttributeModifierDefinitions.WeakHealthOffset, 1, AttributeModifierDefinitions.WeakHealthPenalty, [
                    new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.MiningSpeed, ThresholdPercentage = 0 },
                ]),
                IAttributeModifier.Penalty(AttributeModifierDefinitions.MiningSpeed, 10, [ new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.MiningSpeed, ThresholdPercentage = 0 } ]),
            ],
        };

        public static readonly TraitDefinition Kind = new()
        {
            Id = "kind",
            Attributes = [
                IAttributeModifier.Penalty(AttributeModifierDefinitions.AnimalDropRate, 10, [ new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.AnimalDropRate, ThresholdPercentage = 0 } ]),
                IAttributeModifier.Penalty(AttributeModifierDefinitions.AnimalHarvestRate, 10, [ new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.AnimalHarvestRate, ThresholdPercentage = 0 } ]),
            ]
        };

        public static readonly TraitDefinition Carpenter = new()
        {
            Id = "carpenter",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.Carpenter, 1, [
                    new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Carpenter },
                    new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.TreeChoppingSpeed, ThresholdPercentage = 10 },
                ])
            ],
        };

        public static readonly TraitDefinition Lumberjack = new()
        {
            Id = "lumberjack",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.TreeChoppingSpeed, 120),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.AxeDamage, 75),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.AxeDurability, 70),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.CharcoalDropRate, 20),
            ]
        };

        public static readonly TraitDefinition TreeWhisperer = new()
        {
            Id = "treewhisperer",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.WoodDropRate, 75),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.SeedDropRate, 1000),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.StickDropRate, 300),
            ]
        };

        public static readonly TraitDefinition HeavyFooted = new()
        {
            Id = "heavyfooted",
            Attributes = [
                IAttributeModifier.BasicPenalty(AttributeModifierDefinitions.Furtive, 50),
                IAttributeModifier.BasicPenalty(AttributeModifierDefinitions.WalkingSpeed, 10),
            ]
        };

        public static readonly TraitDefinition Mason = new()
        {
            Id = "mason",
            Attributes = [
            ],
        };

        public static readonly TraitDefinition InteriorDesigner = new()
        {
            Id = "interiordesigner",
            Attributes = [
            ],
        };

        public static readonly TraitDefinition Potter = new()
        {
            Id = "potter",
            Attributes = [
            ],
        };

        public static readonly TraitDefinition Technician = new()
        {
            Id = "technician",
            Attributes = [
            ],
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
                IAttributeModifier.BasicPenalty(AttributeModifierDefinitions.RangedAccuracy, 20),
                IAttributeModifier.BasicPenalty(AttributeModifierDefinitions.RangedDamage, 50),
            ]
        };

        public static readonly TraitDefinition Alchemist = new()
        {
            Id = "alchemist",
            Attributes = [
            ],
        };

        public static readonly TraitDefinition Propagator = new()
        {
            Id = "propagator",
            Attributes = [
            ],
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
        };

        public static readonly TraitDefinition Stonespeaker = new()
        {
            Id = "stonespeaker",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.MiningSpeed, 220),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.OreDropRate, 300),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.StoneDropRate, 300),
            ],
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
                IAttributeModifier.Bonus(AttributeModifierDefinitions.AnimalHarvestRate, 50),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.AnimalDropRate, 50),
            ]
        };

        public static readonly TraitDefinition WildernessExplorer = new()
        {
            Id = "wildernessexplorer",
            Attributes = [
            ],
        };

        public static readonly TraitDefinition Butcher = new()
        {
            Id = "butcher",
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.AnimalHarvestRate, 100),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.AnimalDropRate, 100),
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
            Attributes = [
                IAttributeModifier.Bonus(AttributeModifierDefinitions.MiningSpeed, 100),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.OreDropRate, 100),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.StoneDropRate, 100),
                IAttributeModifier.Bonus(AttributeModifierDefinitions.VesselDropRate, 75),
            ],
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
