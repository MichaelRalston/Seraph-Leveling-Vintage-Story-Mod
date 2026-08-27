using System;
using SeraphLeveling.Data.CharacterClasses;

namespace SeraphLeveling.Data.Mods
{
    public static class ModDefinitions
    {
        public static readonly ModDefinition Vanilla = new()
        {
            ModId = "vanilla",
            CharacterClasses = [
                CharacterClassDefinitions.Commoner,
                CharacterClassDefinitions.Hunter,
                CharacterClassDefinitions.Malefactor,
                CharacterClassDefinitions.Clockmaker,
                CharacterClassDefinitions.Blackguard,
                CharacterClassDefinitions.Tailor,
                CharacterClassDefinitions.VanillaDummy,
            ]
        };

        public static readonly ModDefinition SacredClasses = new()
        {
            ModId = "sacredlib",
            CharacterClasses = [
                CharacterClassDefinitions.Commoner,
                CharacterClassDefinitions.Woodsman,
                CharacterClassDefinitions.Craftsman,
                CharacterClassDefinitions.Witch,
                CharacterClassDefinitions.Blacksmith,
                CharacterClassDefinitions.Artificer,
                CharacterClassDefinitions.Miner,
                CharacterClassDefinitions.Homesteader,
                CharacterClassDefinitions.Huntsman,
                CharacterClassDefinitions.Guardsman,
                CharacterClassDefinitions.Hearthmaster,
                CharacterClassDefinitions.Haberdasher,
                CharacterClassDefinitions.Zealot,
                CharacterClassDefinitions.SacredClassesDummy,
            ]
        };

        public static readonly ModDefinition Butchering = new()
        {
            ModId = "butchering",
            CharacterClasses = [],
        };

        public static readonly ModDefinition ExoticMilk = new()
        {
            ModId = "exoticmilk",
            CharacterClasses = [],
        };

        public static readonly ModDefinition RustboundMagic = new()
        {
            ModId = "rustboundmagic",
            CharacterClasses = [CharacterClassDefinitions.RustMage],
        };
    }
}
