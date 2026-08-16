using System;
using System.IO;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public abstract class ScoredUnlockedAttributeModifierDefinition<D, PD> : UnlockedAttributeModifierDefinition<D, PD> where PD : ScoredUnlockedAttributeModifierProgressData<D, PD> where D : ScoredUnlockedAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        public required int GlobalMaxCredits { get; set; }
        public required string CreditDescription { get; init; }
        public string WatchedCreditsAttributeKey { get; init; } = null;

        public virtual void AddCredits(IServerPlayer player, float toAdd)
        {
            var progress = GetDict(player);

            // Already unlocked - no more progress needed
            if (progress.IsUnlocked) return;

            progress.TotalCredits += toAdd;
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
                progress.IsUnlocked = true;
                FireUnlockChangedEvent(player, false, true);
                ApplyUnlock(player, progress);
                SeraphLevelingModSystem.NotifyLevelUp(player, Lang.Get(NotifyLangKey));
            }
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
    }

    public class ScoredUnlockedAttributeModifierProgressData<D, PD>(D definition) : UnlockedAttributeModifierProgressData<D, PD>(definition) where PD : ScoredUnlockedAttributeModifierProgressData<D, PD> where D : ScoredUnlockedAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        public float TotalCredits { get; set; } = 0;

        public override void ReadVersion(byte version, BinaryReader reader)
        {
            switch (version)
            {
                case 1:
                    TotalCredits = reader.ReadInt32();
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
