using System;
using System.IO;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using System.Collections.Generic;

namespace SeraphLeveling.Data.Attributes
{
    public abstract class ScoredUnlockedAttributeModifierDefinition<D, PD> : UnlockedAttributeModifierDefinition<D, PD> where PD : ScoredUnlockedAttributeModifierProgressData<D, PD> where D : ScoredUnlockedAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        public required int GlobalMaxCredits { get; set; }
        public required string CreditDescription { get; init; }
        public string WatchedCreditsAttributeKey { get; init; } = null;
        public override void ReadConfigData(Dictionary<string, int> dict)
        {
            if (dict.TryGetValue("requirement", out var max)) GlobalMaxCredits = max;
        }
        public override Dictionary<string, int> GetConfigData()
        {
            return new() { ["requirement"] = GlobalMaxCredits };
        }

        public virtual void AddCredits(IServerPlayer player, float toAdd)
        {
            var progress = GetDict(player);

            // Already unlocked - no more progress needed
            if (progress.IsUnlocked) return;

            // Apply sleep buff multiplier to points
            var modifiedPoints = SeraphLevelingModSystem.ApplyXPMultiplier(player.PlayerUID, toAdd);

            progress.TotalCredits += modifiedPoints;
            PendingSave = true;

            if (!string.IsNullOrWhiteSpace(WatchedCreditsAttributeKey))
            {
                player.Entity.WatchedAttributes.SetFloat(WatchedCreditsAttributeKey, progress.TotalCredits);
            }

            if (SeraphLevelingModSystem.DebugLoggingEnabled)
            {
                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} made progress towards {Name} ({progress.TotalCredits:F0} / {GlobalMaxCredits:F0})");
            }

            if (progress.TotalCredits >= GlobalMaxCredits)
            {
                FireUnlockChangedEvent(player, false, true);
            }
        }

        public override bool IsUnlockableForPlayer(IPlayer player)
        {
            var progress = GetDict(player);
            return progress.IsUnlocked || progress.TotalCredits >= GlobalMaxCredits;
        }


        public override void ResetProgress(IServerPlayer player)
        {
            var progress = GetDict(player);
            progress.TotalCredits = 0;
            base.ResetProgress(player);
        }

        protected override void UnlockInner(IServerPlayer player, PD progress)
        {
            base.UnlockInner(player, progress);
            progress.TotalCredits = GlobalMaxCredits;
        }

        public override void GetTraitUnlockableCommandLine(IPlayer player, StringBuilder sb)
        {
            var progress = GetDict(player);
            sb.AppendLine($"{Name}: {progress.TotalCredits}/{GlobalMaxCredits} {CreditDescription} ({(progress.IsUnlocked ? "UNLOCKED" : "locked")})");
        }
        public override void CollectStatus(IPlayer player, StringBuilder sb)
        {
            base.CollectStatus(player, sb);

            var progress = GetDict(player);
            sb.AppendLine($"{Name}: {progress.TotalCredits}/{GlobalMaxCredits} {CreditDescription} ({(progress.IsUnlocked ? "UNLOCKED" : "locked")})");
        }

    }

    public class ScoredUnlockedAttributeModifierProgressData<D, PD>(D definition) : UnlockedAttributeModifierProgressData<D, PD>(definition) where PD : ScoredUnlockedAttributeModifierProgressData<D, PD> where D : ScoredUnlockedAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        public float TotalCredits { get; set; } = 0;

        public override void ReadVersion(byte version, BinaryReader reader)
        {
            switch (version)
            {
                case 1:
                    TotalCredits = reader.ReadSingle();
                    IsUnlocked = reader.ReadBoolean();
                    break;
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }

        public override void WriteOut(BinaryWriter writer)
        {
            writer.Write(TotalCredits);
            writer.Write(IsUnlocked);
        }
    }
}
