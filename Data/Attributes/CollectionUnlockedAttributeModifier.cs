using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace SeraphLeveling.Data.Attributes
{
    public abstract class CollectionUnlockedAttributeModifierDefinition<D, PD> : UnlockedAttributeModifierDefinition<D, PD> where PD : CollectionUnlockedAttributeModifierProgressData<D, PD> where D : CollectionUnlockedAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        public required int RequiredCollectionSize { get; set; }
        public required string CollectedItemDescription { get; init; }
        public required string CollectedItemCountKey { get; init; }
        public HashSet<string> TokenBanList { get; set; } = [];
        public HashSet<string> TokenAllowList { get; set; } = [];

        public virtual bool IsItemValid(AssetLocation itemCode)
        {
            if (itemCode == null || !itemCode.Valid) return false;

            // DENY if the item code path contains any token in the ban list
            foreach (string pattern in TokenBanList)
            {
                if (!string.IsNullOrEmpty(pattern) && itemCode.Path.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // If any allow list token is set to a wildcard, then ALLOW if no tokens are in the ban list
            if (TokenAllowList.Any(pattern => pattern == "*"))
            {
                return true;
            }

            // ALLOW if the item code path contains any token in the allow list
            foreach (string pattern in TokenAllowList)
            {
                if (!string.IsNullOrEmpty(pattern) && itemCode.Path.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // DENY by default
            return false;
        }

        public virtual void AddCollectedItem(IServerPlayer player, AssetLocation toAdd)
        {
            // Added item is invalid - abort
            if (toAdd == null || !toAdd.Valid || !IsItemValid(toAdd)) return;

            var progress = GetDict(player);

            if (progress.CollectedItems.Add(toAdd))
            {
                PendingSave = true;

                if (SeraphLevelingModSystem.DebugLoggingEnabled)
                {
                    SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} made progress towards {Name} by collecting {toAdd} ({progress.CollectedItems.Count} / {RequiredCollectionSize})");
                }

                if (progress.CollectedItems.Count >= RequiredCollectionSize && !progress.IsUnlocked)
                {
                    FireUnlockChangedEvent(player, false, true);
                }
            }
        }

        public override bool IsUnlockableForPlayer(IPlayer player)
        {
            var progress = GetDict(player);
            return progress.IsUnlocked || progress.CollectedItems.Count >= RequiredCollectionSize;
        }

        public override void ApplyUnlock(IServerPlayer player, PD progress)
        {
            if (player?.Entity == null) return;

            base.ApplyUnlock(player, progress);
            player.Entity.WatchedAttributes.SetInt(CollectedItemCountKey, progress.CollectedItems.Count);
            player.Entity.WatchedAttributes.MarkPathDirty(CollectedItemCountKey);
        }

        public override void ResetProgress(IServerPlayer player)
        {
            var progress = GetDict(player);
            progress.CollectedItems.Clear();
            base.ResetProgress(player);
        }

        public override void GetTraitUnlockableCommandLine(IPlayer player, StringBuilder sb) {
            var progress = GetDict(player);
            sb.AppendLine($"{Name}: {progress.CollectedItems.Count}/{RequiredCollectionSize} unique {CollectedItemDescription} ({(progress.IsUnlocked ? "UNLOCKED" : "locked")})");
        }

        public override void CollectStatus(IPlayer player, StringBuilder sb)
        {
            var progress = GetDict(player);
            sb.AppendLine($"{Name} trait: {progress.CollectedItems.Count} / {RequiredCollectionSize} unique {CollectedItemDescription} ({(progress.IsUnlocked ? "UNLOCKED" : "Locked")})");

            if (!progress.IsUnlocked && progress.CollectedItems.Count > 0)
            {
                sb.AppendLine($"Collected:");
                progress.CollectedItems.Foreach(item => sb.AppendLine($"  * {item}"));
            }
        }

        public override TextCommandResult HandleLevelCommand(TextCommandCallingArgs args, int indexOffset)
        {
            var player = args.Caller.Player as IServerPlayer;
            if (player?.Entity == null)
            {
                return TextCommandResult.Error("Could not find player entity");
            }

            var progress = GetDict(player);

            int? newCredits = (int?)args[0+indexOffset];

            // If no value provided, show current level
            if (!newCredits.HasValue)
            {
                string status = progress.IsUnlocked ? "UNLOCKED!" : $"{RequiredCollectionSize - progress.CollectedItems.Count} more needed to unlock.";
                return TextCommandResult.Success($"Current {LongDescription} level: {progress.CollectedItems.Count}/{RequiredCollectionSize} ({status})");
            }

            if (newCredits.Value < 0)
            {
                return TextCommandResult.Error("Level cannot be negative");
            }

            return progress.SetLevelFromCommand(player, newCredits.Value, args, indexOffset);
        }

        public virtual TextCommandResult HandleRequiredLevelCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1) return TextCommandResult.Error($"Required {CollectedItemDescription} must be at least 1.");
                RequiredCollectionSize = newValue.Value;
                PendingSave = true;
                return TextCommandResult.Success($"{Name} required unique {CollectedItemDescription} set to {RequiredCollectionSize}.");
            }

            return TextCommandResult.Success($"Current {LongDescription} required: {RequiredCollectionSize} unique {CollectedItemDescription}.");
        }
    }

    public class CollectionUnlockedAttributeModifierProgressData<D, PD>(D definition) : UnlockedAttributeModifierProgressData<D, PD>(definition) where PD : CollectionUnlockedAttributeModifierProgressData<D, PD> where D : CollectionUnlockedAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        public HashSet<AssetLocation> CollectedItems { get; private set; } = [];

        public override void ReadVersion(byte version, BinaryReader reader)
        {
            switch (version)
            {
                case 1:
                    IsUnlocked = reader.ReadBoolean();
                    CollectedItems = reader.ReadStringArray().Select(str => AssetLocation.Create(str)).ToHashSet();
                    break;
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }

        public override void WriteOut(BinaryWriter writer)
        {
            writer.Write(IsUnlocked);
            writer.WriteArray(CollectedItems.Select(loc => loc.ToString()).ToArray());
        }

        public virtual TextCommandResult SetLevelFromCommand(IServerPlayer player, int newLevel, TextCommandCallingArgs args, int indexOffset)
        {
            // Clear the existing collection set
            CollectedItems.Clear();

            // Add placeholder entries up to the desired level
            for (int i = 0; i < newLevel; i++)
            {
                CollectedItems.Add(AssetLocation.Create($"__placeholder_{i}"));
            }

            // Set unlock status based on whether we've reached the required amount
            IsUnlocked = newLevel >= Definition.RequiredCollectionSize;

            Definition.PendingSave = true;
            Definition.ApplyUnlock(player, (PD)this);

            string newStatus = IsUnlocked ? "UNLOCKED!" : $"{Definition.RequiredCollectionSize - newLevel} more needed to unlock.";
            return TextCommandResult.Success($"{Definition.LongDescription} level set to {newLevel}/{Definition.RequiredCollectionSize}. {newStatus}");
        }
    }
}
