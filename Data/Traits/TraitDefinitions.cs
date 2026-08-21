using System;
using SeraphLeveling.Data.Attributes;
using static SeraphLeveling.Data.Attributes.AttributeModifierDefinitions;
using static SeraphLeveling.Data.Attributes.IAttributeModifier;

namespace SeraphLeveling.Data.Traits
{
    public static class TraitDefinitions
    {
        public static readonly TraitDefinition Focused = new()
        {
            Id = "focused",
            Attributes = [
                Bonus(RangedDamage, 20),
                Bonus(RangedAccuracy, 30),
                Bonus(RangedDistance, 20),
            ],
        };

        public static readonly TraitDefinition Resourceful = new()
        {
            Id = "resourceful",
            Attributes = [
                Bonus(AnimalDropRate, 10),
                Bonus(AnimalHarvestRate, 25),
            ],
        };

        public static readonly TraitDefinition Fleetfooted = new()
        {
            Id = "fleetfooted",
            PlainTraitNameKey = "seraphleveling:trait-sitwalkingmastery",
            Attributes = [
                Bonus(WalkingSpeed, 10),
            ],
        };

        public static readonly TraitDefinition Bowyer = new()
        {
            Id = "bowyer",
            Attributes = [
                Bonus(AttributeModifierDefinitions.Bowyer, 1, [
                    new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Bowyer },
                    new LeveledAttributeMinimumRequirement { Attribute = RangedDamage, ThresholdPercentage = 10 },
                ])
            ],
        };

        public static readonly TraitDefinition Farsighted = new()
        {
            Id = "farsighted",
            Attributes = [
                Penalty(MeleeDamage, 15, [ new LeveledAttributeMinimumRequirement { Attribute = MeleeDamage, ThresholdPercentage = 0 }]),
            ],
        };

        public static readonly TraitDefinition Claustrophobic = new()
        {
            Id = "claustrophobic",
            Attributes = [
                PenaltyOffset(ClaustrophobicOre, 1, ClaustrophobicOrePenalty, [
                    new LeveledAttributeMinimumRequirement { Attribute = MiningSpeed, ThresholdPercentage = 0 },
                ]),
                Penalty(MiningSpeed, 10, [ new LeveledAttributeMinimumRequirement() { Attribute = MiningSpeed, ThresholdPercentage = 0 }]),
            ],
        };

        public static readonly TraitDefinition Forager = new()
        {
            Id = "forager",
            Attributes = [
                Bonus(ForageLootingBonus, 10),
                Bonus(WildCropDropRate, 20),
            ]
        };

        public static readonly TraitDefinition Pilferer = new()
        {
            Id = "pilferer",
            Attributes = [
                Bonus(GearDropRate, 10),
                Bonus(VesselDropRate, 15),
                Bonus(WholeVesselRate, 12),
            ]
        };

        public static readonly TraitDefinition Furtive = new()
        {
            Id = "furtive",
            Attributes = [
                Bonus(AttributeModifierDefinitions.Furtive, 35),
            ],
        };

        public static readonly TraitDefinition Improviser = new()
        {
            Id = "improviser",
            Attributes = [
                Bonus(AttributeModifierDefinitions.Improviser, 1, [ new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Improviser } ])
            ],
        };

        public static readonly TraitDefinition Frail = new()
        {
            Id = "frail",
            Attributes = [
                PenaltyOffset(FrailHealthOffset, 1, FrailHealthPenalty, [
                    new LeveledAttributeMinimumRequirement { Attribute = RangedDistance, ThresholdPercentage = 0 },
                ]),
                Penalty(RangedDistance, 25, [ new LeveledAttributeMinimumRequirement { Attribute = RangedDistance, ThresholdPercentage = 0 }]),
            ],
        };

        public static readonly TraitDefinition Nervous = new()
        {
            Id = "nervous",
            Attributes = [
                Penalty(MeleeDamage, 15, [ new LeveledAttributeMinimumRequirement { Attribute = MeleeDamage, ThresholdPercentage = 0 }]),
            ],
        };

        public static readonly TraitDefinition Precise = new()
        {
            Id = "precise",
            Attributes = [
                Bonus(AttributeModifierDefinitions.Precise, 25),
            ],
        };

        public static readonly TraitDefinition Technical = new()
        {
            Id = "technical",
            Attributes = [
                Bonus(AttributeModifierDefinitions.Technical, 1, [ new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Technical } ])
            ],
        };

        public static readonly TraitDefinition Tinkerer = new()
        {
            Id = "tinkerer",
            Attributes = [
                Bonus(AttributeModifierDefinitions.Tinkerer, 1, [
                    new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Technical },
                    new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.Precise, ThresholdPercentage = 10 },
                ])
            ],
        };

        public static readonly TraitDefinition Soldier = new()
        {
            Id = "soldier",
            Attributes = [
                Bonus(MeleeDamage, 30),
                Bonus(ArmorDurability, 15),
                Bonus(ArmorWalkSpeed, 25),
            ],
        };

        public static readonly TraitDefinition Hardy = new()
        {
            Id = "hardy",
            Attributes = [
                Bonus(HardyHealth, 1, [
                    new LeveledAttributeMinimumRequirement { Attribute = MiningSpeed, ThresholdPercentage = 10 },
                    new LeveledAttributeMinimumRequirement { Attribute = ArmorDurability, ThresholdPercentage = 10 }
                ]),
                Bonus(MiningSpeed, 10),
            ]
        };

        public static readonly TraitDefinition Merciless = new()
        {
            Id = "merciless",
            Attributes = [
                Bonus(AttributeModifierDefinitions.Merciless, 1, [
                    new LeveledAttributeMinimumRequirement { Attribute = MeleeDamage, ThresholdPercentage = 15 },
                    new LeveledAttributeMinimumRequirement{ Attribute = ArmorDurability , ThresholdPercentage = 10}
                ])
            ],
        };

        public static readonly TraitDefinition HungerMastery = new()
        {
            // This trait doesn't actually exist in vanilla, but we still want to display it for players that have earned the attribute
            Id = "hungermastery",
            DynamicTraitHeaderKey = "seraphleveling:trait-hungermastery",
            Attributes = [
                Bonus(HungerRate, 1),
            ],
        };

        public static readonly TraitDefinition Ravenous = new()
        {
            Id = "ravenous",
            Attributes = [
                Penalty(HungerRate, 30, [ new LeveledAttributeMinimumRequirement { Attribute = HungerRate, ThresholdPercentage = 0 }])
            ],
        };

        public static readonly TraitDefinition Nearsighted = new()
        {
            Id = "nearsighted",
            Attributes = [
                Penalty(RangedDamage, 15, [ new LeveledAttributeMinimumRequirement { Attribute = RangedDamage, ThresholdPercentage = 0 }])
            ],
        };

        public static readonly TraitDefinition Heavyhanded = new()
        {
            Id = "heavyhanded",
            Attributes = [
                Penalty(VesselDropRate, 10, [ new LeveledAttributeMinimumRequirement { Attribute = VesselDropRate, ThresholdPercentage = 0 }]),
                Penalty(ForageLootingBonus, 15, [ new LeveledAttributeMinimumRequirement { Attribute = ForageLootingBonus, ThresholdPercentage = 0 }]),
                Penalty(WildCropDropRate, 20, [ new LeveledAttributeMinimumRequirement { Attribute = WildCropDropRate, ThresholdPercentage = 0 }]),
            ]
        };

        public static readonly TraitDefinition Clothier = new()
        {
            Id = "clothier",
            Attributes = [
                Bonus(AttributeModifierDefinitions.Clothier, 1, [ new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Clothier } ])
            ],
        };

        public static readonly TraitDefinition Mender = new()
        {
            Id = "mender",
            Attributes = [
                Bonus(AttributeModifierDefinitions.Mender, 25)
            ]
        };

        public static readonly TraitDefinition Civil = new()
        {
            Id = "civil",
            Attributes = [
                Penalty(ForageLootingBonus, 10, [ new LeveledAttributeMinimumRequirement { Attribute = ForageLootingBonus, ThresholdPercentage = 0 } ]),
            ]
        };

        public static readonly TraitDefinition Weak = new()
        {
            Id = "weak",
            Attributes = [
                PenaltyOffset(WeakHealthOffset, 1, WeakHealthPenalty, [
                    new LeveledAttributeMinimumRequirement { Attribute = MiningSpeed, ThresholdPercentage = 0 },
                ]),
                Penalty(MiningSpeed, 10, [ new LeveledAttributeMinimumRequirement { Attribute = MiningSpeed, ThresholdPercentage = 0 } ]),
            ],
        };

        public static readonly TraitDefinition Kind = new()
        {
            Id = "kind",
            Attributes = [
                Penalty(AnimalDropRate, 10, [ new LeveledAttributeMinimumRequirement { Attribute = AnimalDropRate, ThresholdPercentage = 0 } ]),
                Penalty(AnimalHarvestRate, 10, [ new LeveledAttributeMinimumRequirement { Attribute = AnimalHarvestRate, ThresholdPercentage = 0 } ]),
            ]
        };

        public static readonly TraitDefinition Carpenter = new()
        {
            Id = "carpenter",
            Attributes = [
                Bonus(AttributeModifierDefinitions.Carpenter, 1, [
                    new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Carpenter },
                    new LeveledAttributeMinimumRequirement { Attribute = TreeChoppingSpeed, ThresholdPercentage = 10 },
                ])
            ],
        };

        public static readonly TraitDefinition Lumberjack = new()
        {
            Id = "lumberjack",
            Attributes = [
                Bonus(TreeChoppingSpeed, 120), // TODO: make sure this is right, because it looks like 220 to me in SL's data.
                Bonus(AxeDamage, 75),
                Bonus(AxeDurability, 70),
                Bonus(CharcoalDropRate, 20), // TODO: is this right, or is it 120? I think it's 120.
            ]
        };

        public static readonly TraitDefinition TreeWhisperer = new()
        {
            Id = "treewhisperer",
            Attributes = [
                Bonus(WoodDropRate, 75),
                Bonus(SeedDropRate, 1000),
                Bonus(StickDropRate, 300),
            ]
        };

        public static readonly TraitDefinition HeavyFooted = new()
        {
            Id = "heavyfooted",
            Attributes = [
                BasicPenalty(AttributeModifierDefinitions.Furtive, 50),
                BasicPenalty(WalkingSpeed, 10),
            ]
        };

        public static readonly TraitDefinition Mason = new()
        {
            Id = "mason",
            Attributes = [
                Bonus(AttributeModifierDefinitions.Mason, 1, [
                    new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Mason },
                    new LeveledAttributeMinimumRequirement { Attribute = MiningSpeed, ThresholdPercentage = 10 },
                ])
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
                Bonus(AttributeModifierDefinitions.Potter, 1, [
                    new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Potter },
                    new LeveledAttributeMinimumRequirement { Attribute = ClayDropRate, ThresholdPercentage = 10 },
                ])
            ],
        };

        public static readonly TraitDefinition Technician = new()
        {
            Id = "technician",
            Attributes = [
                Bonus(AttributeModifierDefinitions.Technician, 1, [
                    new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Technician },
                    new LeveledAttributeMinimumRequirement { Attribute = AttributeModifierDefinitions.Precise, ThresholdPercentage = 10 },
                ])
            ],
        };

        public static readonly TraitDefinition SiltSeeker = new()
        {
            Id = "siltseeker",
            Attributes = [
                Bonus(ClayDropRate, 50),
                Bonus(ClayformSpeed, 100),
                Bonus(PeatDropRate, 50),
            ]
        };

        public static readonly TraitDefinition Townie = new()
        {
            Id = "townie",
            Attributes = [
                Bonus(AttributeModifierDefinitions.Townie, 10),
            ]
        };

        public static readonly TraitDefinition Agoraphobic = new()
        {
            Id = "agoraphobic",
            Attributes = [
                BasicPenalty(RangedAccuracy, 20),
                BasicPenalty(RangedDamage, 50),
            ]
        };

        public static readonly TraitDefinition Alchemist = new()
        {
            Id = "alchemist",
            Attributes = [
                Bonus(AttributeModifierDefinitions.Alchemist, 1, [
                    new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Alchemist },
                    new LeveledAttributeMinimumRequirement { Attribute = HealUseSpeed, ThresholdPercentage = 10 },
                ])
            ],
        };

        public static readonly TraitDefinition Propagator = new()
        {
            Id = "propagator",
            Attributes = [
                Bonus(AttributeModifierDefinitions.Propagator, 1, [
                    new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.Propagator },
                    new LeveledAttributeMinimumRequirement { Attribute = ForageLootingBonus, ThresholdPercentage = 10 },
                ])
            ],
        };

        public static readonly TraitDefinition Naturalist = new()
        {
            Id = "naturalist",
            Attributes = [
                Bonus(ForageLootingBonus, 100),
                Bonus(AttributeModifierDefinitions.Furtive, 80),
            ]
        };

        public static readonly TraitDefinition Medic = new()
        {
            Id = "medic",
            Attributes = [
                Bonus(HealUseSpeed, 70),
            ]
        };

        public static readonly TraitDefinition MasterCraftsman = new()
        {
            Id = "mastercraftsman",
            Attributes = [
                Bonus(AttributeModifierDefinitions.MasterCraftsman, 1, [
                    new UnlockedAttributeRequirement { Attribute = AttributeModifierDefinitions.MasterCraftsman },
                    new LeveledAttributeMinimumRequirement { Attribute = HammerDurability, ThresholdPercentage = 10 },
                ])
            ],
        };

        public static readonly TraitDefinition Blacksmith = new()
        {
            Id = "blacksmith",
            Attributes = [
                // TODO: two other attributes.
                Bonus(CharcoalDropRate, 200),
                Bonus(HammerDurability, 70),
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
                Bonus(GearDropRate, 200),
                Bonus(AttributeModifierDefinitions.Precise, 300),
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
                Bonus(MiningSpeed, 220),
                Bonus(OreDropRate, 300),
                Bonus(StoneDropRate, 300),
            ],
        };

        public static readonly TraitDefinition CaveExplorer = new()
        {
            Id = "caveexplorer",
            Attributes = [
                Bonus(AttributeModifierDefinitions.CaveExplorer, 120, [
                    new LeveledAttributeMinimumRequirement { Attribute = PickaxeDurability, ThresholdPercentage = 20 },
                    new LeveledAttributeMinimumRequirement { Attribute = WalkingSpeed, ThresholdPercentage = 10 },
                ]),
                Bonus(PickaxeDurability, 70),
            ]
        };

        public static readonly TraitDefinition EarthSinger = new()
        {
            Id = "earthsinger",
            Attributes = [
                Bonus(FarmedCropDropRate, 220),
                Bonus(HoeDurability, 70),
                Bonus(ScytheDurability, 70),
            ]
        };

        public static readonly TraitDefinition Rancher = new()
        {
            Id = "rancher",
            Attributes = [
                Bonus(AnimalHarvestRate, 50),
                Bonus(AnimalDropRate, 50),
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
                Bonus(AnimalHarvestRate, 100),
                Bonus(AnimalDropRate, 100),
            ]
        };

        public static readonly TraitDefinition Ranger = new()
        {
            Id = "ranger",
            Attributes = [
                // TODO: Figure out what's up with bow vs ranged damage here.
                Bonus(BowDurability, 70),
                Bonus(AttributeModifierDefinitions.Furtive, 80),
            ]
        };

        public static readonly TraitDefinition WellAdjusted = new()
        {
            Id = "welladjusted",
            Attributes = [
                Bonus(ArmorDurability, 10),
                Bonus(ArmorWalkSpeed, 25),
            ]
        };

        public static readonly TraitDefinition Bulwark = new()
        {
            Id = "bulwark",
            Attributes = [
                Bonus(ArmorWalkSpeed, 70),
                Bonus(ArmorDurability, 50),
                Bonus(BulwarkHealth, 1, [
                    new LeveledAttributeMinimumRequirement { Attribute = ArmorWalkSpeed, ThresholdPercentage = 70 },
                    new LeveledAttributeMinimumRequirement { Attribute = ArmorDurability, ThresholdPercentage = 50 }
                ]),

            ]
        };

        public static readonly TraitDefinition ArmyMedic = new()
        {
            Id = "armymedic",
            Attributes = [
                Bonus(HealUseSpeed, 30),
            ]
        };

        public static readonly TraitDefinition StrongArmed = new()
        {
            Id = "strongarmed",
            Attributes = [
                Bonus(MiningSpeed, 100),
                Bonus(OreDropRate, 100),
                Bonus(StoneDropRate, 100),
                Bonus(VesselDropRate, 75),
            ],
        };

        public static readonly TraitDefinition HeavyHands = new()
        {
            Id = "heavyhands",
            Attributes = [
                Penalty(WildCropDropRate, 20),
                Penalty(ForageLootingBonus, 15),
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
