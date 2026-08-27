using System;
using System.Collections.Generic;
using SeraphLeveling.Data.CharacterClasses;

namespace SeraphLeveling.Data.Mods
{
    public static class ModDefinitions
    {
        public static readonly List<ModDefinition> All = [];

        public static readonly ModDefinition Vanilla = Register(new()
        {
            ModId = "game",
            DisplayName = "Vintage Story",
            CharacterClasses = [
                CharacterClassDefinitions.Commoner,
                CharacterClassDefinitions.Hunter,
                CharacterClassDefinitions.Malefactor,
                CharacterClassDefinitions.Clockmaker,
                CharacterClassDefinitions.Blackguard,
                CharacterClassDefinitions.Tailor,
                CharacterClassDefinitions.VanillaDummy,
            ]
        });

        public static readonly ModDefinition SacredClasses = Register(new()
        {
            ModId = "sacredlib",
            DisplayName = "Sacred Classes",
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
        });

        public static readonly ModDefinition Butchering = Register(new()
        {
            ModId = "butchering",
            DisplayName = "Butchering",
            CharacterClasses = [],
        });

        public static readonly ModDefinition ExoticMilk = Register(new()
        {
            ModId = "exoticmilk",
            DisplayName = "Exotic Milk",
            CharacterClasses = [],
        });

        private static ModDefinition Register(ModDefinition def)
        {
            All.Add(def);
            return def;
        }
    }
}
