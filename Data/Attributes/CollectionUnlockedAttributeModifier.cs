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
    public abstract record class CollectionUnlockedAttributeModifierDefinition<D, PD> : UnlockedAttributeModifierDefinition<D, PD> where PD : CollectionUnlockedAttributeModifierProgressData<D, PD> where D : CollectionUnlockedAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        public required int RequiredCollectionSize { get; init; }
        public required string CollectedItemDescription { get; init; }
        public required string CollectedItemCountKey { get; init; }

        protected abstract bool IsItemValid(string itemCode);

        public virtual void AddCollectedItem(IServerPlayer player, string toAdd)
        {
            // Added item is invalid - abort
            if (string.IsNullOrWhiteSpace(toAdd) || !IsItemValid(toAdd)) return;

            var progress = GetDict(player);

            // Already unlocked - no more progress needed
            if (progress.IsUnlocked) return;

            if (progress.CollectedItems.Add(toAdd))
            {
                MarkForSave(true);

                if (SeraphLevelingModSystem.DebugLoggingEnabled)
                {
                    SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} made progress towards {Name} by collecting {toAdd} ({progress.CollectedItems.Count} / {RequiredCollectionSize})");
                }

                if (progress.CollectedItems.Count >= RequiredCollectionSize)
                {
                    bool oldUnlock = progress.IsUnlocked;
                    progress.IsUnlocked = true;
                    if (oldUnlock != progress.IsUnlocked)
                    {
                        FireUnlockChangedEvent(player, oldUnlock, progress.IsUnlocked);
                    }
                    ApplyUnlock(player, progress);
                    SeraphLevelingModSystem.NotifyLevelUp(player, Lang.Get(NotifyLangKey));
                }
            }
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

        public override void GetTraitAllCommandLine(IPlayer player, StringBuilder sb) {
            var progress = GetDict(player);
            sb.AppendLine($"{Name}: {progress.CollectedItems.Count}/{RequiredCollectionSize} {CollectedItemDescription} ({(progress.IsUnlocked ? "UNLOCKED" : "locked")})");
        }
    }

    public class CollectionUnlockedAttributeModifierProgressData<D, PD>(D definition) : UnlockedAttributeModifierProgressData<D, PD>(definition) where PD : CollectionUnlockedAttributeModifierProgressData<D, PD> where D : CollectionUnlockedAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        public HashSet<string> CollectedItems { get; private set; } = [];

        public override void ReadVersion(byte version, BinaryReader reader)
        {
            switch (version)
            {
                case 1:
                    IsUnlocked = reader.ReadBoolean();
                    CollectedItems = [.. reader.ReadStringArray()];
                    break;
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }

        public override void WriteOut(BinaryWriter writer)
        {
            writer.Write(IsUnlocked);
            writer.WriteArray(CollectedItems.ToArray());
        }
    }
}
