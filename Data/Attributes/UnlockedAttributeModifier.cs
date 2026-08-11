using System;
using System.IO;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public abstract record class UnlockedAttributeModifierDefinition<D, PD> : AttributeModifierDefinition<D, PD> where PD : UnlockedAttributeModifierProgressData<D, PD> where D : UnlockedAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        public required string Name { get; init; }
        public required string ExtraTraitKey { get; init; }
        public required string UnlockedKey { get; init; }

        public void GetTraitAllCommandLine(IPlayer player, StringBuilder sb) {
            var progress = GetDict(player);
            sb.AppendLine($"{Name}: {(progress.IsUnlocked ? "UNLOCKED" : "locked")}");
        }

        public void ResetProgress(IServerPlayer player)
        {
            var progress = GetDict(player);
            progress.IsUnlocked = false;
            MarkForSave(true);
            ApplyUnlock(player, progress);
        }

        public void Unlock(IServerPlayer player)
        {
            var progress = GetDict(player);
            progress.IsUnlocked = true;
            MarkForSave(true);
            ApplyUnlock(player, progress);
        }

        /// <summary>
        /// Check and apply unlock if all requirements are met.
        /// </summary>
        public abstract void CheckUnlock(IServerPlayer player);

        public virtual void ApplyUnlock(IServerPlayer player, PD progress)
        {
            player.Entity.WatchedAttributes.SetBool(UnlockedKey, progress.IsUnlocked);

            // Update extraTraits to show trait if unlocked (for UI display)
            SeraphLevelingModSystem.UpdateExtraTraitStatic(player.Entity, ExtraTraitKey, progress.IsUnlocked);

            // IMPORTANT: Add ID to extraTraits to unlock tuning spear recipes
            // The game's recipe system checks extraTraits for dynamically granted traits
            // that unlock recipes via requiresTrait (e.g., the tuning spear requires "tinkerer")
            SeraphLevelingModSystem.UpdateExtraTraitStatic(player.Entity, Id, progress.IsUnlocked);
        }

        public override void HandleLogin(IServerPlayer player)
        {
            var progress = GetDict(player);
            ApplyUnlock(player, progress);
            if (progress.IsUnlocked)
            {
                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Applied {Description} unlock to player {player.PlayerName}");
            }
        }

        public abstract TextCommandResult HandleTraitCommand(TextCommandCallingArgs args);

        public TextCommandResult HandleUnlockCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0];

            var progress = GetDict(player);
            progress.IsUnlocked = unlock;

            MarkForSave(true);
            ApplyUnlock(player, progress);

            return TextCommandResult.Success($"{Name} trait {(unlock ? "unlocked" : "locked")}.");
        }
    }

    public class UnlockedAttributeModifierProgressData<D, PD>(D definition) : AttributeModifierProgressData<D, PD>(definition) where PD : UnlockedAttributeModifierProgressData<D, PD> where D : UnlockedAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        /// <summary>Whether the trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; } = false;

        public override void ReadVersion(byte version, BinaryReader reader)
        {
            switch (version)
            {
                case 1:
                    IsUnlocked = reader.ReadBoolean();
                    break;
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }

        public override void WriteOut(BinaryWriter writer)
        {
            writer.Write(IsUnlocked);
        }
    }
}
