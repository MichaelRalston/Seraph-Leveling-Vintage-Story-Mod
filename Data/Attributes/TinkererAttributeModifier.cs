using System.Text;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using SeraphLeveling.Data.Legacy;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public record class TinkererAttributeModifierDefinition : UnlockedAttributeModifierDefinition<TinkererAttributeModifierDefinition, TinkererAttributeModifierProgressData>, IConstructable<TinkererAttributeModifierDefinition, TinkererAttributeModifierProgressData>
    {
        public static TinkererAttributeModifierProgressData Create(TinkererAttributeModifierDefinition def)
        {
            return new TinkererAttributeModifierProgressData(def);
        }

        public override void CheckUnlocks(IServerPlayer player)
        {
            if (player?.Entity == null) return;

            string playerUid = player.PlayerUID;
            var progress = GetDict(player);

            // Already unlocked
            if (progress.IsUnlocked) return;

            // FIXME Abstract checking of requirements
            
            // Check Technical trait
            var technicalProgress = SeraphLevelingModSystem.TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            if (!technicalProgress.IsUnlocked) return;

            // Check Precise threshold
            var preciseProgress = SeraphLevelingModSystem.PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());
            if (preciseProgress.TotalCredits < SeraphLevelingModSystem.TinkererPreciseThreshold) return;

            // Both conditions met - unlock Tinkerer!
            progress.IsUnlocked = true;
            MarkForSave(true);

            // Apply the trait
            ApplyUnlock(player, progress);

            // Notify player
            SeraphLevelingModSystem.NotifyLevelUp(player,
                Lang.Get("seraphleveling:message-tinkerer-unlock"));
        }

        public override TextCommandResult HandleTraitCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            string playerUid = player.PlayerUID;
            var progress = GetDict(player);
            var technicalProgress = SeraphLevelingModSystem.TechnicalProgress.GetOrAdd(playerUid, _ => new TechnicalProgressData());
            var preciseProgress = SeraphLevelingModSystem.PreciseProgress.GetOrAdd(playerUid, _ => new PreciseProgressData());

            var sb = new StringBuilder();
            sb.AppendLine($"Tinkerer trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
            sb.AppendLine($"Requirements:");
            sb.AppendLine($"  Technical trait: {(technicalProgress.IsUnlocked ? "UNLOCKED ✓" : "Locked ✗")}");
            sb.AppendLine($"  Precise level: {preciseProgress.TotalCredits} / {SeraphLevelingModSystem.TinkererPreciseThreshold} ({(preciseProgress.TotalCredits >= SeraphLevelingModSystem.TinkererPreciseThreshold ? "✓" : "✗")})");

            return TextCommandResult.Success(sb.ToString());
        }
    }

    public class TinkererAttributeModifierProgressData(TinkererAttributeModifierDefinition definition) : UnlockedAttributeModifierProgressData<TinkererAttributeModifierDefinition, TinkererAttributeModifierProgressData>(definition)
    {
        
    }
}
