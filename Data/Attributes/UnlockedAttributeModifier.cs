using System;
using System.IO;
using System.Text;
using SeraphLeveling.Data.Traits;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public interface IUnlockedAttributeModifierDefinition : ISaveableAttribute
    {
        public string Name { get; }
        public string UnlockedKey { get; }
        public Lazy<TraitDefinition> Trait { get; }
        public bool IsUnlockedForPlayer(IPlayer player);
        public bool IsUnlockableForPlayer(IPlayer player);

        /// <summary>
        /// Registers a method to be called every time the unlock status for this attribute changes for a player
        /// </summary>
        public event UnlockChangedDelegate UnlockChanged;
    }

    public delegate void UnlockChangedDelegate(IServerPlayer player, bool oldUnlock, bool newUnlock);

    public abstract class UnlockedAttributeModifierDefinition<D, PD> : AttributeModifierDefinition<D, PD>, IUnlockedAttributeModifierDefinition where PD : UnlockedAttributeModifierProgressData<D, PD> where D : UnlockedAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        public virtual string UnlockedKey { get => field ??= $"sit{Name}Unlocked"; init; }
        public string NotifyLangKey { get => field ??= $"seraphleveling:message-{SkillKey}-unlock"; init; } = null;
        public required Lazy<TraitDefinition> Trait { get; init; }

        public event UnlockChangedDelegate UnlockChanged;

        protected void FireUnlockChangedEvent(IServerPlayer player, bool oldUnlock, bool newUnlock)
        {
            UnlockChanged?.Invoke(player, oldUnlock, newUnlock);
        }

        public virtual bool IsUnlockableForPlayer(IPlayer player)
        {
            return GetDict(player).IsUnlocked;
        }
        public virtual bool IsUnlockedForPlayer(IPlayer player)
        {
            return GetDict(player).IsUnlocked;
        }

        public override IChatCommand RegisterCommands(ICoreServerAPI api, IChatCommand command)
        {
            return base.RegisterCommands(api, command)
            .BeginSubCommand(SkillKey)
                .WithDescription($"View your {SkillKey} trait progress")
                .RequiresPrivilege(Privilege.chat)
                .RequiresPlayer()
                .HandleWith(Trait.Value.HandleTraitCommand)
            .EndSubCommand();
        }

        public override void GetTraitUnlockableCommandLine(IPlayer player, StringBuilder sb) {
            var progress = GetDict(player);
            sb.AppendLine($"{Name}: {(progress.IsUnlocked ? "UNLOCKED" : "locked")}");
        }

        public override void ResetProgress(IServerPlayer player)
        {
            var progress = GetDict(player);
            bool oldUnlock = progress.IsUnlocked;
            progress.IsUnlocked = false;
            if (oldUnlock != progress.IsUnlocked)
            {
                FireUnlockChangedEvent(player, oldUnlock, progress.IsUnlocked);
            }
            PendingSave = true;
            ApplyUnlock(player, progress);
        }

        public override void MaxStat(IServerPlayer player)
        {
            Unlock(player);
        }
        public override void Unlock(IServerPlayer player, bool notify = false)
        {
            var progress = GetDict(player);

            // Already unlocked
            if (progress.IsUnlocked) return;

            // Perform the unlock
            UnlockInner(player, progress);
            PendingSave = true;
            ApplyUnlock(player, progress);

            // Notify player
            if (notify && !string.IsNullOrWhiteSpace(NotifyLangKey))
            {
                SeraphLevelingModSystem.NotifyLevelUp(player, Lang.Get(NotifyLangKey));
            }
        }

        protected virtual void UnlockInner(IServerPlayer player, PD progress)
        {
            bool oldUnlock = progress.IsUnlocked;
            progress.IsUnlocked = true;
            if (oldUnlock != progress.IsUnlocked)
            {
                FireUnlockChangedEvent(player, oldUnlock, progress.IsUnlocked);
            }
        }

        public virtual void ApplyUnlock(IServerPlayer player, PD progress)
        {
            if (player?.Entity == null) return;

            player.Entity.WatchedAttributes.SetBool(UnlockedKey, progress.IsUnlocked);

            // Update extraTraits to show trait if unlocked (for UI display)
            SeraphLevelingModSystem.UpdateExtraTraitStatic(player.Entity, TraitCode, progress.IsUnlocked);

            // IMPORTANT: Add ID to extraTraits to unlock tuning spear etc recipes
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
                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Applied {LongDescription} unlock to player {player.PlayerName}");
            }
        }

        public override TextCommandResult HandleUnlockCommand(TextCommandCallingArgs args, int indexOffset)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null) return TextCommandResult.Error("Player not found.");

            bool unlock = (bool)args[0+indexOffset];

            var progress = GetDict(player);
            bool oldUnlock = progress.IsUnlocked;
            progress.IsUnlocked = unlock;
            if (oldUnlock != progress.IsUnlocked)
            {
                FireUnlockChangedEvent(player, oldUnlock, progress.IsUnlocked);
            }

            PendingSave = true;
            ApplyUnlock(player, progress);

            return TextCommandResult.Success($"{Name} trait {(unlock ? "unlocked" : "locked")}.");
        }

        public override void CollectStatus(IPlayer player, StringBuilder sb)
        {
            var progress = GetDict(player);
            sb.AppendLine($"{Name} trait: {(progress.IsUnlocked ? "UNLOCKED" : "Locked")}");
        }

        public override bool ShouldDisplay(EntityPlayer player)
        {
            return player.WatchedAttributes.GetBool(UnlockedKey, false);
        }

        public override object GetLocalizedTraitTextParam(EntityPlayer player)
        {
            // Unlocked attributes don't have numeric parameters to be included dynamically in the trait text
            return null;
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
