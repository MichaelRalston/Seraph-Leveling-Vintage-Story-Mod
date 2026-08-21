using System.Text;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using SeraphLeveling.Data.Legacy;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public class CaveExplorerAttributeModifierDefinition : UnlockedAttributeModifierDefinition<CaveExplorerAttributeModifierDefinition, CaveExplorerAttributeModifierProgressData>, IConstructable<CaveExplorerAttributeModifierDefinition, CaveExplorerAttributeModifierProgressData>
    {
        public static CaveExplorerAttributeModifierProgressData Create(CaveExplorerAttributeModifierDefinition def)
        {
            return new CaveExplorerAttributeModifierProgressData(def);
        }
        public override void ApplyUnlock(IServerPlayer player, CaveExplorerAttributeModifierProgressData progress)
        {
            if (player?.Entity == null) return;

            player.Entity.WatchedAttributes.SetBool(UnlockedKey, progress.IsUnlocked);
            player.Entity.Stats.Set("ats:cavevisionstrength", "sitCaveExplorerBonus", progress.IsUnlocked?1.2f:0, false);

            // Update extraTraits to show trait if unlocked (for UI display)
            SeraphLevelingModSystem.UpdateExtraTraitStatic(player.Entity, TraitCode, progress.IsUnlocked);

            // IMPORTANT: Add ID to extraTraits to unlock tuning spear etc recipes
            // The game's recipe system checks extraTraits for dynamically granted traits
            // that unlock recipes via requiresTrait (e.g., the tuning spear requires "tinkerer")
            SeraphLevelingModSystem.UpdateExtraTraitStatic(player.Entity, Id, progress.IsUnlocked);
        }

    }

    public class CaveExplorerAttributeModifierProgressData(CaveExplorerAttributeModifierDefinition definition) : UnlockedAttributeModifierProgressData<CaveExplorerAttributeModifierDefinition, CaveExplorerAttributeModifierProgressData>(definition)
    {

    }
}
