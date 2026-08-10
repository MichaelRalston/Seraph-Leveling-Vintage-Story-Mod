using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using System;
using System.IO;
using System.Numerics;
using System.Text;
using SeraphLeveling.Data;

namespace SeraphLeveling.Data.Legacy
{

    public interface ILevelableToolContract<ToolT>
    {
        public abstract static string Name { get; }
        public abstract static int BaseIncrementSize { get; set; }
        public abstract static int IncrementStep { get; set; }
    }
    public abstract class LevelableTool<ToolT>
    {
        public abstract void WriteOut(BinaryWriter writer);
        /// <summary>Points accumulated toward the next credit with this tool.</summary>
        public int PartialCredit { get; set; }

        /// <summary>Points needed for the next credit with this tool (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

    }
    public abstract class LeveledToolTraitProgressData<T, ToolT>: LeveledTraitProgressData<T>
        where T : LeveledToolTraitProgressData<T, ToolT>, IProgressDataContract<T>, ILeveledTraitContract<T>, new()
        where ToolT: LevelableTool<ToolT>, ILevelableToolContract<ToolT>, IDeepCopyable<ToolT>, new()
    {
        /// <summary>Per-tool progress tracking. Key is item code (e.g., "game:pickaxe-copper").</summary>
        public Dictionary<string, ToolT> ToolProgress { get; set; }
        public LeveledToolTraitProgressData()
        {
            TotalCredits = 0;
            ToolProgress = new Dictionary<string, ToolT>();
            LastActivityDay = 0;
        }

        /// <summary>
        /// Get or create progress data for a specific tool.
        /// New tools start with the configured BaseBlocksPerIncrement.
        /// </summary>
        public ToolT GetToolProgress(string toolCode)
        {
            if (!ToolProgress.TryGetValue(toolCode, out var progress))
            {
                progress = new ToolT();
                ToolProgress[toolCode] = progress;
            }
            return progress;
        }
        /// <summary>
        /// Create a copy of this data.
        /// </summary>
        public T Clone()
        {
            var clone = new T
            {
                TotalCredits = this.TotalCredits,
                LastActivityDay = this.LastActivityDay,
                ToolProgress = new Dictionary<string, ToolT>()
            };
            foreach (var kvp in this.ToolProgress)
            {
                clone.ToolProgress[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }

        public override void WriteOut(BinaryWriter writer) {
            writer.Write(TotalCredits);
            writer.Write(LastActivityDay);

            // Snapshot inner dictionary to avoid concurrent modification
            var toolSnapshot = ToolProgress.ToArray();
            writer.Write(toolSnapshot.Length);
            foreach (var toolKvp in toolSnapshot)
            {
                writer.Write(toolKvp.Key); // Pickaxe code
                toolKvp.Value.WriteOut(writer);
            }
        }

        public static void ResetProgress(IServerPlayer player) {
            var progress = GetDict(player);
            progress.TotalCredits = 0;
            var toolEntries = progress.ToolProgress.Select(kvp =>
                (kvp.Key, (double)kvp.Value.PartialCredit, kvp.Value.CurrentIncrementSize)).ToList();
            progress.LastActivityDay = 0;
            T.MarkForSave();
            progress.ApplyBonus(player);
        }

        public override void WriteIncrementLine(StringBuilder sb)
        {
            if (ToolProgress.Count > 0)
            {
                sb.AppendLine($"\nPer-{ToolT.Name} progress:");
                foreach (var kvp in ToolProgress.OrderBy(p => p.Value.CurrentIncrementSize))
                {
                    string toolName = kvp.Key;
                    // Simplify the display name (remove "game:" prefix if present)
                    if (toolName.StartsWith("game:"))
                        toolName = toolName.Substring(5);

                    var toolProgress = kvp.Value;
                    sb.AppendLine($"  {toolName}: {toolProgress.PartialCredit}/{toolProgress.CurrentIncrementSize} points");
                }
            }
            else
            {
                sb.AppendLine("\nNo pickaxe progress yet. Mine stone or ore with a pickaxe to start!");
            }

        }
        private static int ApplyStatPenalty(T progress, double rawPenalty, StringBuilder sb, StringBuilder verboseSb) {
            int oldCredits = progress.TotalCredits;
            var toolEntries = progress.ToolProgress.Select(kvp =>
                (kvp.Key, (double)kvp.Value.PartialCredit, kvp.Value.CurrentIncrementSize)).ToList();

            if (toolEntries.Count > 0)
            {
                var (newCr, lost) = SeraphLevelingModSystem.ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                    ToolT.BaseIncrementSize, ToolT.IncrementStep, oldCredits,
                    (k, a, s) => { if (progress.ToolProgress.TryGetValue(k, out var p)) {
                        p.PartialCredit = (int)Math.Floor(a); p.CurrentIncrementSize = s; } },
                    k => progress.ToolProgress.Remove(k), verboseSb, "Mining");
                progress.TotalCredits = newCr;
                sb.AppendLine($"  Mining: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts)");
                foreach (var entry in toolEntries)
                {
                    int oldToolCr = ToolT.IncrementStep > 0 ? (entry.Item3 - ToolT.BaseIncrementSize) / ToolT.IncrementStep : 0;
                    if (progress.ToolProgress.TryGetValue(entry.Item1, out var after))
                    {
                        int newToolCr = ToolT.IncrementStep > 0 ? (after.CurrentIncrementSize - ToolT.BaseIncrementSize) / ToolT.IncrementStep : 0;
                        int toolLost = oldToolCr - newToolCr;
                        sb.AppendLine($"    {entry.Item1}: {(int)entry.Item2}/{entry.Item3} \u2192 {after.PartialCredit:F0}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                    }
                    else
                        sb.AppendLine($"    {entry.Item1}: {(int)entry.Item2}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                }
                T.MarkForSave();
                if (lost > 0) return lost;
            }
            else
            {
                int lost = Math.Min((int)rawPenalty, oldCredits);
                progress.TotalCredits -= lost;
                if (lost > 0) { sb.AppendLine($"  Mining: {oldCredits} \u2192 {progress.TotalCredits} (-{lost} credits)"); }
                T.MarkForSave();
                return lost;
            }
            return 0;
        }

        public static int ApplyDecay(IServerPlayer player, double currentDay, StringBuilder sb, StringBuilder verboseSb)
        {
            if (!SeraphLevelingModSystem.DecayExemptSkills.Contains(T.SkillKey) && !SeraphLevelingModSystem.DisabledSkills.Contains(T.SkillKey))
            {
                if (T.ProgressDictionary().TryGetValue(player.PlayerUID, out var progress) && (progress.TotalCredits > 0 || progress.ToolProgress.Count > 0))
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
                if (T.ProgressDictionary().TryGetValue(player.PlayerUID, out var progress) && (progress.TotalCredits > 0 || progress.ToolProgress.Count > 0))
                {
                    var toolEntries = progress.ToolProgress.Select(kvp =>
                        (kvp.Key, (double)kvp.Value.PartialCredit, kvp.Value.CurrentIncrementSize)).ToList();
                    double rawPenalty;
                    if (toolEntries.Count > 0) {
                        rawPenalty = ToolT.BaseIncrementSize * SeraphLevelingModSystem.DeathPenaltyFraction * Math.Sqrt(Math.Max(1, progress.TotalCredits));
                    } else {
                        rawPenalty = Math.Floor(SeraphLevelingModSystem.DeathPenaltyFraction * Math.Sqrt(Math.Max(1, progress.TotalCredits)));
                    }

                    return ApplyStatPenalty(progress, rawPenalty, sb, null);
                }
            }
            return 0;
        }

        public override TextCommandResult SetLevelFromCommand(IServerPlayer player, int level, TextCommandCallingArgs args)
        {
            string toolName = (string)args[1];
            string playerUid = player.PlayerUID;
            int maxCredits = GetMaxCredits(player.Entity);
            if (level < 0)
                return TextCommandResult.Error("Credits cannot be negative.");

            if (toolName != null)
            {
                // Per-tool mode: set credits on a specific pickaxe without clearing others
                int oldToolCredits = 0;
                if (ToolProgress.TryGetValue(toolName, out var existingTool))
                    oldToolCredits = SeraphLevelingModSystem.CalculateToolCredits(existingTool.CurrentIncrementSize, ToolT.BaseIncrementSize, ToolT.IncrementStep);

                int projectedTotal = TotalCredits - oldToolCredits + level;
                if (projectedTotal > maxCredits)
                    return TextCommandResult.Error($"Setting {level} credits on {toolName} would result in {projectedTotal} total credits, exceeding max ({maxCredits}).");

                if (level == 0)
                {
                    ToolProgress.Remove(toolName);
                }
                else
                {
                    var pickaxeProgress = GetToolProgress(toolName);
                    pickaxeProgress.CurrentIncrementSize = ToolT.BaseIncrementSize + (level * ToolT.IncrementStep);
                    pickaxeProgress.PartialCredit = 0;
                }

                TotalCredits = SeraphLevelingModSystem.RecalculateTotalCreditsFromTools(
                    ToolProgress, p => p.CurrentIncrementSize,
                    ToolT.BaseIncrementSize, ToolT.IncrementStep);

                T.MarkForSave();
                int bonusPercent = ApplyBonus(player);
                CheckUnlocks(player);
                UpdateSkillActivityDay();

                return TextCommandResult.Success($"Set {level} credits on {toolName}. Total: {TotalCredits}/{maxCredits} (+{bonusPercent}% mining speed).");
            }
            else
            {
                // Total mode: set TotalCredits directly and clear per-tool progress
                if (level > maxCredits)
                    return TextCommandResult.Error($"Credits cannot exceed max ({maxCredits}).");

                TotalCredits = level;
                ToolProgress.Clear();

                T.MarkForSave();
                int bonusPercent = ApplyBonus(player);
                CheckUnlocks(player);
                UpdateSkillActivityDay();

                return TextCommandResult.Success($"Mining credits set to {level} (+{bonusPercent}% mining speed). Per-pickaxe progress reset.");
            }
        }
    }
}
