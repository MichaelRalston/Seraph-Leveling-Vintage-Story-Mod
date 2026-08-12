using System;
using System.IO;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public interface IUnlockedAttributeModifierDefinition
    {
        public string Name { get; }
        public bool IsUnlockedForPlayer(IPlayer player);
    }

    public abstract record class UnlockedAttributeModifierDefinition<D, PD> : AttributeModifierDefinition<D, PD>, IUnlockedAttributeModifierDefinition where PD : UnlockedAttributeModifierProgressData<D, PD> where D : UnlockedAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        public required string Name { get; init; }
        public required string ExtraTraitKey { get; init; }
        public required string UnlockedKey { get; init; }
        public string NotifyLangKey { get; init; } = null;

        public bool IsUnlockedForPlayer(IPlayer player)
        {
            return GetDict(player).IsUnlocked;
        }

        public virtual void GetTraitAllCommandLine(IPlayer player, StringBuilder sb) {
            var progress = GetDict(player);
            sb.AppendLine($"{Name}: {(progress.IsUnlocked ? "UNLOCKED" : "locked")}");
        }

        public virtual void ResetProgress(IServerPlayer player)
        {
            var progress = GetDict(player);
            progress.IsUnlocked = false;
            MarkForSave(true);
            ApplyUnlock(player, progress);
        }

        public override void CheckUnlocks(IServerPlayer player)
        {
            // Prerequisite checks are performed at the trait level, not attribute
            Unlock(player, true);
        }

        public override void Unlock(IServerPlayer player, bool notify = false)
        {
            var progress = GetDict(player);

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Perform the unlock
            progress.IsUnlocked = true;
            MarkForSave(true);
            ApplyUnlock(player, progress);

            // Notify player
            if (notify && !string.IsNullOrWhiteSpace(NotifyLangKey))
            {
                SeraphLevelingModSystem.NotifyLevelUp(player, Lang.Get(NotifyLangKey));
            }
        }

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

        public override void CollectStatus(IPlayer player, StringBuilder sb)
        {
            var progress = GetDict(player);
            sb.AppendLine($"{Name} trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
        }
    }

    public interface IUnlockedAttributeModifierProgressData
    {
        public bool IsUnlocked { get; }
    }

    public class UnlockedAttributeModifierProgressData<D, PD>(D definition) : AttributeModifierProgressData<D, PD>(definition), IUnlockedAttributeModifierProgressData where PD : UnlockedAttributeModifierProgressData<D, PD> where D : UnlockedAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
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
