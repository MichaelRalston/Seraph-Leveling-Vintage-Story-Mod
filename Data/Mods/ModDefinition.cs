using System;
using System.Collections.Generic;
using SeraphLeveling.Data.CharacterClasses;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Mods
{
    public record class ModDefinition
    {
        public required string ModId { get; init; }
        public required string DisplayName { get; init; }
        public required List<CharacterClassDefinition> CharacterClasses { get; init; }
        
        internal bool IsLoaded { get; private set; } = false;
        internal bool IsEnabled { get; set; } = true;
        public bool IsActive => IsLoaded && IsEnabled;

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

        private bool DetectInner(IModLoader modLoader) => modLoader?.IsModEnabled(ModId) ?? false;
    }
}
