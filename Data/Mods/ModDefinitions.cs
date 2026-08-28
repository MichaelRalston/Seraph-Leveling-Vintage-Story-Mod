using System;
using System.Collections.Generic;
using System.Linq;
using SeraphLeveling.Config;
using SeraphLeveling.Data.CharacterClasses;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace SeraphLeveling.Data.Mods
{
    public static class ModDefinitions
    {
        public static readonly List<ModDefinition> All = [];

        public static readonly ModDefinition Vanilla = Register(new()
        {
            ModId = "game",
            DisplayName = "Vintage Story",
            IncompatibleWith = [ new(() => SacredClasses) ],    // Sacred Classes replaces the vanilla set of classes
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

        public static readonly ModDefinition RustboundMagic = Register(new()
        {
            ModId = "rustboundmagic",
            DisplayName = "Rustbound Magic",
            CharacterClasses = [
                CharacterClassDefinitions.RustMage
            ],
        });

        public static readonly ModDefinition CombatOverhaul = Register(new()
        {
            ModId = SeraphLevelingModSystem.COMBAT_OVERHAUL_BASE_ID,
            ModVariants = [ SeraphLevelingModSystem.COMBAT_OVERHAUL_FORK_ID ],
            DisplayName = "Combat Overhaul",
            CharacterClasses = [],
        });

        private static ModDefinition Register(ModDefinition def)
        {
            All.Add(def);
            return def;
        }

        public static HashSet<ModDefinition> DetectActive(IModLoader modLoader, SeraphLevelingConfig config)
        {
            // Detect whether each defined mod is loaded and/or enabled in configuration
            All.ForEach(mod => mod.Detect(modLoader, config));

            // Disable any mods that are incompatible with another active mod
            bool changed;
            int retries = 0;
            const int MAX_RETRIES = 500;
            do
            {
                changed = false;
                foreach (var mod in All)
                {
                    bool oldHasConflict = mod.HasConflict;
                    mod.HasConflict = mod.IncompatibleWith.Any(conflicting => conflicting.Value.IsActive);
                    if (mod.HasConflict != oldHasConflict)
                    {
                        changed = true;
                    }
                }
            } while (changed && ++retries <= MAX_RETRIES);
            if (retries > MAX_RETRIES)
            {
                SeraphLevelingModSystem.ServerApi?.Logger?.Error($"[SeraphLeveling] Failed to resolve mod conflicts after {MAX_RETRIES} retries, disabling all mod support");
                All.ForEach(mod => mod.HasConflict = true);
            }
            else
            {
                All.Where(mod => mod.HasConflict).Foreach(mod =>
                {
                    string incompatibleStr = string.Join(", ", mod.IncompatibleWith.Where(m => m.Value.IsActive).Select(m => m.Value.DisplayName));
                    SeraphLevelingModSystem.ServerApi?.Logger?.Notification($"[SeraphLeveling] Mod {mod.DisplayName} is incompatible with mod(s) {incompatibleStr}. Compatibility disabled for {mod.DisplayName}.");
                });
            }
            
            // Return the final set of mods to use for this run
            return [.. All.Where(mod => mod.IsActive)];
        }
    }
}
