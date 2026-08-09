using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using System;
using System.IO;
using System.Numerics;
using System.Collections.Concurrent;
using System.Text;

namespace SeraphLeveling {
    public interface ILeveledTraitContract<T> where T:ILeveledTraitContract<T>
    {
        public static virtual string Name { get; }
        public static virtual string Stat { get; }

    }
    // This class will be very unhappy if V is type anything other than int or float. Fortunately, I don't anticipate that being an issue.
    public abstract class LeveledTraitProgressData<T, V> : ProgressData<T> 
    where T : LeveledTraitProgressData<T, V>, IProgressDataContract<T>, ILeveledTraitContract<T>, new() // 'new()' goes at the end
    where V : INumber<V>
    {
        protected LeveledTraitProgressData() {
            TotalCredits = 0;
            PartialCredit = V.Zero;
            LastActivityDay = 0;
        }

        /// <summary>
        /// Create a copy of this data.
        /// </summary>
        public T Clone()
        {
            return new T
            {
                TotalCredits = this.TotalCredits,
                PartialCredit = this.PartialCredit,
                CurrentIncrementSize = this.CurrentIncrementSize,
                LastActivityDay = this.LastActivityDay
            };
        }

        /// <summary>Total credits earned (each credit = 1% bonus).</summary>
        public int TotalCredits { get; set; }
        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }
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

        public abstract int GetMaxCredits(EntityPlayer player);
        public abstract int GetIncrementStep();
        public abstract string GetIncrementUnits();
        public abstract void ApplyBonus(IServerPlayer player);
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

        private static T GetDict(IPlayer player) {
            return T.ProgressDictionary().GetOrAdd(player.PlayerUID, _ => new T());
        }

        public static void ApplyTraitTestSuite1Command(IServerPlayer player) {
            var progress = GetDict(player);
            progress.TotalCredits = 1;
            progress.PartialCredit = V.Zero;
            progress.CurrentIncrementSize = progress.GetBaseIncrement();
            T.MarkForSave();
            progress.ApplyBonus(player);
        }

        public static void GetTraitAllCommandLine(IPlayer player, StringBuilder sb) {
            var progress = GetDict(player);
            sb.AppendLine($"{T.Name}: {progress.TotalCredits}/{progress.GetMaxCredits(player.Entity)} (+{progress.TotalCredits}{T.Stat})");
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
        public static void MaxStat(IServerPlayer player) {
            var progress = GetDict(player);
            int maxCredits = progress.GetMaxCredits(player.Entity);
            progress.TotalCredits = maxCredits;
            progress.PartialCredit = V.Zero;
            T.MarkForSave();
            progress.ApplyBonus(player);
        }

        public void UpdateSkillActivityDay() {
            if (!SeraphLevelingModSystem.EnableSkillDecay) return;
            if (SeraphLevelingModSystem.ServerApi == null) return;

            LastActivityDay = SeraphLevelingModSystem.ServerApi.World.Calendar.TotalDays;
        }

        public static TextCommandResult SetLevel(IServerPlayer player, int level) {
            var progress = GetDict(player);
            int maxLevel = progress.GetMaxCredits(player.Entity);
            if (level > maxLevel) return TextCommandResult.Error($"Level cannot exceed max ({maxLevel}).");
            progress.TotalCredits = level;
            T.MarkForSave();
            progress.ApplyBonus(player);
            progress.UpdateSkillActivityDay();
            return TextCommandResult.Success($"{T.Name} level set to {level} (+{level}{T.Stat}) for {player.PlayerName}.");
        }
    }
}