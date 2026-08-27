using System;
using System.Collections.Generic;
using System.Linq;
using SeraphLeveling.Data.CharacterClasses;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Mods
{
    public record class ModDefinition
    {
        public required string ModId { get; init; }
        public virtual List<string> ModVariants { get; init; } = [];
        private List<string> FullIdList => [ModId, .. ModVariants];
        public required string DisplayName { get; init; }
        public required List<CharacterClassDefinition> CharacterClasses { get; init; }
        
        internal bool IsLoaded { get; private set; } = false;
        internal bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Used to determine if this mod is both loaded and config-enabled, i.e. is ready for use.
        /// </summary>
        public bool IsActive => IsLoaded && IsEnabled;

        private readonly Dictionary<string, bool> LoadStatus = [];

        public void Detect(IModLoader modLoader)
        {
            IsLoaded = DetectInner(modLoader);

            if (SeraphLevelingModSystem.ServerApi != null)
            {
                if (IsLoaded)
                {
                    if (IsEnabled)
                    {
                        SeraphLevelingModSystem.ServerApi.Logger.Notification($"[SeraphLeveling] {DisplayName} mod detected. Compatibility enabled.");
                    }
                    else
                    {
                        SeraphLevelingModSystem.ServerApi.Logger.Notification($"[SeraphLeveling] {DisplayName} mod detected, but compatibility is disabled in config.");
                    }
                }
                else
                {
                    SeraphLevelingModSystem.ServerApi.Logger.Notification($"[SeraphLeveling] {DisplayName} mod not detected. Compatibility disabled.");
                }
            }
        }

        private bool DetectInner(IModLoader modLoader)
        {
            LoadStatus.Clear();
            foreach (string id in FullIdList)
            {
                LoadStatus[id] = modLoader?.IsModEnabled(id) ?? false;
            }
            return LoadStatus.Values.Any(x => x);
        }

        /// <summary>
        /// Used to detect whether a particular variant of this mod is active. For example, if you specifically need to know that
        /// the "Combat Overhaul 1.22 Fork" mod is installed instead of any Combat Overhaul mod.
        /// </summary>
        public bool IsVariantActive(string variantId) => IsEnabled && LoadStatus.TryGetValue(variantId, out bool variantLoaded) && variantLoaded;
    }
}
