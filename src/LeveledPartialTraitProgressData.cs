using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace SeraphLeveling {
    // This class will be very unhappy if V is type anything other than int or float. Fortunately, I don't anticipate that being an issue.
    public abstract class LeveledPartialTraitProgressData<T, V> : LeveledTraitProgressData<T> 
    where T : LeveledPartialTraitProgressData<T, V>, IProgressDataContract<T>, ILeveledTraitContract<T>, new()
    where V : INumber<V>
    {
        protected LeveledPartialTraitProgressData() {
            PartialCredit = V.Zero;
        }

        /// <summary>
        /// Create a copy of this data.
        /// </summary>
        public T Clone()
        {
            return new T
            {
                PartialCredit = this.PartialCredit,
                CurrentIncrementSize = this.CurrentIncrementSize,
                TotalCredits = this.TotalCredits,
                LastActivityDay = this.LastActivityDay
            };
        }

        /// <summary>Action taken toward the next credit.</summary>
        public V PartialCredit { get; set; } // formerly known as BlocksInIncrement

        /// <summary>Actions needed for the next credit (1000, 2000, 3000, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        public static byte GetVersion() {
            return (byte)2;
        }
        public override void WriteOut(BinaryWriter writer) {
            writer.Write(TotalCredits);
            switch (this.PartialCredit) {
                case int i:
                    writer.Write(i);
                    break;
                case float f:
                    writer.Write(f);
                    break;
                default:
                    // Fallback safety net for robustness
                    throw new NotSupportedException($"Binary writing for type {typeof(V).Name} is not supported.");
            }            
            writer.Write(CurrentIncrementSize);
            writer.Write(LastActivityDay);
        }
        public static T ReadVersion(byte version, BinaryReader reader) {
            switch (version) {
                case 1:
                    return new T {
                        TotalCredits = reader.ReadInt32(),
                        PartialCredit = (typeof(V) == typeof(int)?V.CreateTruncating(reader.ReadInt32()):(typeof(V) == typeof(float)?V.CreateTruncating(reader.ReadSingle()):throw new NotSupportedException($"Binary reading for type {typeof(V).Name} is not supported."))),
                        CurrentIncrementSize = reader.ReadInt32()
                    };
                case 2:
                    return new T {
                        TotalCredits = reader.ReadInt32(),
                        PartialCredit = (typeof(V) == typeof(int)?V.CreateTruncating(reader.ReadInt32()):(typeof(V) == typeof(float)?V.CreateTruncating(reader.ReadSingle()):throw new NotSupportedException($"Binary reading for type {typeof(V).Name} is not supported."))),
                        CurrentIncrementSize = reader.ReadInt32(),
                        LastActivityDay = reader.ReadDouble()
                    };
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }

        public abstract int GetIncrementStep();
        public abstract string GetIncrementUnits();
        public abstract int GetBaseIncrement();

        public void DoEvent(IServerPlayer player, V score) {
                // Skip all processing if already at max - completely invisible
                var maxCredits = GetMaxCredits(player.Entity);
                if (TotalCredits >= maxCredits) return;

                int oldCredits = TotalCredits;

                // Apply sleep buff multiplier to score
                float modifiedScore = SeraphLevelingModSystem.ApplyXPMultiplier(player.PlayerUID, float.CreateTruncating(score));

                // Add distance to progress
                PartialCredit += V.CreateTruncating(modifiedScore);

                // Check if we've earned any new credits
                var incrementStep = GetIncrementStep();
                var units = GetIncrementUnits();
                while (PartialCredit >= V.CreateTruncating(CurrentIncrementSize) && TotalCredits < maxCredits)
                {
                    // Earn a credit
                    TotalCredits++;
                    PartialCredit -= V.CreateTruncating(CurrentIncrementSize);
                    CurrentIncrementSize += incrementStep;

                    SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned {T.Description} credit {TotalCredits}, next requires {CurrentIncrementSize} {units}");
                }

                // Mark for saving if any progress was made
                if (PartialCredit > V.Zero || TotalCredits > oldCredits)
                {
                    T.MarkForSave();
                }

                // If credits increased, update the stat and notify player
                if (TotalCredits > oldCredits)
                {
                    ApplyBonus(player);

                    // Notify player of level up with raw improvement (shows progress even when capped)
                    SeraphLevelingModSystem.NotifyLevelUp(player,
                        Lang.Get($"seraphleveling:message-{T.Description}-level-up", TotalCredits, TotalCredits));
                }

        }


        public static void ResetProgress(IServerPlayer player) {
            var progress = GetDict(player);
            progress.TotalCredits = 0;
            progress.PartialCredit = V.Zero;
            progress.CurrentIncrementSize = progress.GetBaseIncrement();
            progress.LastActivityDay = 0;
            T.MarkForSave();
            progress.ApplyBonus(player);
        }
        public override string GetIncrementLine()
        {
            return $"Progress: {PartialCredit:F1}/{CurrentIncrementSize} {GetIncrementUnits()}";
        }

        public override void ZeroPartialCredit() {
            PartialCredit = V.Zero;
        }

        public override void CalculateIncrementSize()
        {
            // Calculate what the increment size should be at this level
            CurrentIncrementSize = GetBaseIncrement() + (TotalCredits * GetIncrementStep());
        }

        private static int ApplyStatPenalty(T progress, double rawPenalty, StringBuilder sb, StringBuilder verboseSb) {
            int oldCredits = progress.TotalCredits;
            float oldAcc = float.CreateTruncating(progress.PartialCredit); int oldInc = progress.CurrentIncrementSize;
            var (newCr, newAcc, newInc, lost) = SeraphLevelingModSystem.ApplySingleAccumulatorDecay(
                oldAcc, oldInc, oldCredits,
                rawPenalty, progress.GetBaseIncrement(), progress.GetIncrementStep(), verboseSb, T.SkillKey);
            progress.TotalCredits = newCr; progress.PartialCredit = V.CreateTruncating(newAcc); progress.CurrentIncrementSize = newInc;
            sb.AppendLine($"  {T.Name}: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc:F0}/{oldInc} \u2192 {(int)newAcc}/{newInc}");
            T.MarkForSave();
            if (lost > 0) return lost;
            return 0;
        }

        public static int ApplyDecay(IServerPlayer player, double currentDay, StringBuilder sb, StringBuilder verboseSb) {
            if (!SeraphLevelingModSystem.DecayExemptSkills.Contains(T.SkillKey) && !SeraphLevelingModSystem.DisabledSkills.Contains(T.SkillKey))
            {
                if (T.ProgressDictionary().TryGetValue(player.PlayerUID, out var progress) && (progress.TotalCredits > 0 || progress.PartialCredit > V.Zero))
                {
                    var (grace, basePoints, maxPoints) = SeraphLevelingModSystem.GetDecayParams(T.SkillKey);
                    int decayCredits = SeraphLevelingModSystem.CalculateDecayPoints(progress.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        return ApplyStatPenalty(progress, decayCredits, sb, verboseSb);
                    }
                }
            }
            return 0;
        }

        public static int ApplyDeathPenalty(IServerPlayer player, StringBuilder sb) {
            if (!SeraphLevelingModSystem.DeathPenaltyExemptSkills.Contains(T.SkillKey) && !SeraphLevelingModSystem.DisabledSkills.Contains(T.SkillKey))
            {
                if (T.ProgressDictionary().TryGetValue(player.PlayerUID, out var progress) && (progress.TotalCredits > 0 || progress.PartialCredit > V.Zero))
                {
                    double rawPenalty = progress.GetBaseIncrement() * SeraphLevelingModSystem.DeathPenaltyFraction * Math.Sqrt(Math.Max(1, progress.TotalCredits));
                    return ApplyStatPenalty(progress, rawPenalty, sb, null);
                }
            }
            return 0;
        }
    }
}