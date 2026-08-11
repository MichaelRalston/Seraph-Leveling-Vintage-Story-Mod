using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SeraphLeveling.Data.Attributes
{
    public record class ToolDefinition
    {
        public required string Name { get; init; }
        public required int BaseIncrement { get; init; }
        public required int IncrementStep { get; init; }
        public required string IncrementUnits { get; init; }
    }
    public abstract record class LeveledToolAttributeModifierDefinition<D, PD> : LeveledAttributeModifierDefinition<D, PD> where PD : LeveledToolAttributeModifierProgressData<D, PD> where D : LeveledToolAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        public required ToolDefinition Tool { get; init; }
        public void ResetProgress(IServerPlayer player)
        {
            var progress = GetDict(player);
            progress.TotalCredits = 0;
            var toolEntries = progress.ToolProgress.Select(kvp =>
                (kvp.Key, (double)kvp.Value.PartialCredit, kvp.Value.CurrentIncrementSize)).ToList();
            progress.LastActivityDay = 0;
            MarkForSave(true);
            ApplyBonus(player, progress);
        }
        public override int ApplyDecay(IServerPlayer player, double currentDay, StringBuilder sb, StringBuilder verboseSb)
        {
            if (!SeraphLevelingModSystem.DecayExemptSkills.Contains(SkillKey) && !SeraphLevelingModSystem.DisabledSkills.Contains(SkillKey))
            {
                if (ProgressDictionary.TryGetValue(player.PlayerUID, out var progress) && (progress.TotalCredits > 0 || progress.ToolProgress.Count > 0))
                {
                    var (grace, basePoints, maxPoints) = SeraphLevelingModSystem.GetDecayParams(SkillKey);
                    int decayCredits = SeraphLevelingModSystem.CalculateDecayPoints(progress.LastActivityDay, currentDay, grace, basePoints, maxPoints);
                    if (decayCredits > 0)
                    {
                        return progress.ApplyStatPenalty(decayCredits, sb, verboseSb);
                    }
                }
            }
            return 0;
        }

        public override int ApplyDeathPenalty(IServerPlayer player, StringBuilder sb)
        {
            if (!SeraphLevelingModSystem.DeathPenaltyExemptSkills.Contains(SkillKey) && !SeraphLevelingModSystem.DisabledSkills.Contains(SkillKey))
            {
                if (ProgressDictionary.TryGetValue(player.PlayerUID, out var progress) && (progress.TotalCredits > 0 || progress.ToolProgress.Count > 0))
                {
                    var toolEntries = progress.ToolProgress.Select(kvp =>
                        (kvp.Key, (double)kvp.Value.PartialCredit, kvp.Value.CurrentIncrementSize)).ToList();
                    double rawPenalty;
                    if (toolEntries.Count > 0)
                    {
                        rawPenalty = Tool.BaseIncrement * SeraphLevelingModSystem.DeathPenaltyFraction * Math.Sqrt(Math.Max(1, progress.TotalCredits));
                    }
                    else
                    {
                        rawPenalty = Math.Floor(SeraphLevelingModSystem.DeathPenaltyFraction * Math.Sqrt(Math.Max(1, progress.TotalCredits)));
                    }

                    return progress.ApplyStatPenalty(rawPenalty, sb, null);
                }
            }
            return 0;
        }
    }

    public class LevelableTool
    {
        public virtual void WriteOut(BinaryWriter writer)
        {
            writer.Write(PartialCredit);
            writer.Write(CurrentIncrementSize);
        }
        /// <summary>Points accumulated toward the next credit with this tool.</summary>
        public int PartialCredit { get; set; }

        /// <summary>Points needed for the next credit with this tool (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

    }

    public abstract class LeveledToolAttributeModifierProgressData<D, PD>(D def) : LeveledAttributeModifierProgressData<D, PD>(def) where PD : LeveledToolAttributeModifierProgressData<D, PD> where D : LeveledToolAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        public Dictionary<string, LevelableTool> ToolProgress { get; set; }
        public LevelableTool GetToolProgress(string toolCode)
        {
            if (!ToolProgress.TryGetValue(toolCode, out var progress))
            {
                progress = new LevelableTool()
                {
                    CurrentIncrementSize = Definition.Tool.BaseIncrement
                };
                ToolProgress[toolCode] = progress;
            }
            return progress;
        }

        public TextCommandResult SetLevel(IServerPlayer player, int level, string toolName)
        {
            int maxCredits = Definition.GetMaxCredits(player.Entity);
            if (level < 0)
                return TextCommandResult.Error("Credits cannot be negative.");

            if (toolName != null)
            {
                // Per-tool mode: set credits on a specific pickaxe without clearing others
                int oldToolCredits = 0;
                if (ToolProgress.TryGetValue(toolName, out var existingTool))
                    oldToolCredits = SeraphLevelingModSystem.CalculateToolCredits(existingTool.CurrentIncrementSize, Definition.Tool.BaseIncrement, Definition.Tool.IncrementStep);

                int projectedTotal = TotalCredits - oldToolCredits + level;
                if (projectedTotal > maxCredits)
                    return TextCommandResult.Error($"Setting {level} credits on {toolName} would result in {projectedTotal} total credits, exceeding max ({maxCredits}).");

                if (level == 0)
                {
                    ToolProgress.Remove(toolName);
                }
                else
                {
                    var toolProgress = GetToolProgress(toolName);
                    toolProgress.CurrentIncrementSize = Definition.Tool.BaseIncrement + (level * Definition.Tool.IncrementStep);
                    toolProgress.PartialCredit = 0;
                }

                TotalCredits = SeraphLevelingModSystem.RecalculateTotalCreditsFromTools(
                    ToolProgress, p => p.CurrentIncrementSize,
                    Definition.Tool.BaseIncrement, Definition.Tool.IncrementStep);

                Definition.MarkForSave(true);
                int bonusPercent = Definition.ApplyBonus(player, (PD)this);
                Definition.CheckUnlocks(player);
                UpdateSkillActivityDay();

                return TextCommandResult.Success($"Set {level} credits on {toolName}. Total: {TotalCredits}/{maxCredits} (+{bonusPercent}{Definition.Stat}).");
            }
            else
            {
                // Total mode: set TotalCredits directly and clear per-tool progress
                if (level > maxCredits)
                    return TextCommandResult.Error($"Credits cannot exceed max ({maxCredits}).");

                TotalCredits = level;
                ToolProgress.Clear();

                Definition.MarkForSave(true);
                int bonusPercent = Definition.ApplyBonus(player, (PD)this);
                Definition.CheckUnlocks(player);
                UpdateSkillActivityDay();

                return TextCommandResult.Success($"{Definition.Name} credits set to {level} (+{bonusPercent}{Definition.Stat}). Per-tool progress reset.");
            }
        }
        public override TextCommandResult SetLevelFromCommand(IServerPlayer player, int level, TextCommandCallingArgs args)
        {
            string toolName = (string)args[1];
            return SetLevel(player, level, toolName);
        }
        public override void WriteOut(BinaryWriter writer)
        {
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
        public override void WriteIncrementLine(StringBuilder sb)
        {
            if (ToolProgress.Count > 0)
            {
                sb.AppendLine($"\nPer-{Definition.Tool.Name} progress:");
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
                sb.AppendLine($"\nNo {Definition.Tool.Name} progress yet.");
            }
        }
        public int ApplyStatPenalty(double rawPenalty, StringBuilder sb, StringBuilder verboseSb)
        {
            int oldCredits = TotalCredits;
            var toolEntries = ToolProgress.Select(kvp =>
                (kvp.Key, (double)kvp.Value.PartialCredit, kvp.Value.CurrentIncrementSize)).ToList();

            if (toolEntries.Count > 0)
            {
                var (newCr, lost) = SeraphLevelingModSystem.ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                    Definition.Tool.BaseIncrement, Definition.Tool.IncrementStep, oldCredits,
                    (k, a, s) =>
                    {
                        if (ToolProgress.TryGetValue(k, out var p))
                        {
                            p.PartialCredit = (int)Math.Floor(a); p.CurrentIncrementSize = s;
                        }
                    },
                    k => ToolProgress.Remove(k), verboseSb, Definition.Name);
                TotalCredits = newCr;
                sb.AppendLine($"  Mining: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts)");
                foreach (var entry in toolEntries)
                {
                    int oldToolCr = Definition.Tool.IncrementStep > 0 ? (entry.Item3 - Definition.Tool.BaseIncrement) / Definition.Tool.IncrementStep : 0;
                    if (ToolProgress.TryGetValue(entry.Item1, out var after))
                    {
                        int newToolCr = Definition.Tool.IncrementStep > 0 ? (after.CurrentIncrementSize - Definition.Tool.BaseIncrement) / Definition.Tool.IncrementStep : 0;
                        int toolLost = oldToolCr - newToolCr;
                        sb.AppendLine($"    {entry.Item1}: {(int)entry.Item2}/{entry.Item3} \u2192 {after.PartialCredit:F0}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                    }
                    else
                        sb.AppendLine($"    {entry.Item1}: {(int)entry.Item2}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                }
                Definition.MarkForSave(true);
                if (lost > 0) return lost;
            }
            else
            {
                int lost = Math.Min((int)rawPenalty, oldCredits);
                TotalCredits -= lost;
                if (lost > 0) { sb.AppendLine($"  {Definition.Name}: {oldCredits} \u2192 {TotalCredits} (-{lost} credits)"); }
                Definition.MarkForSave(true);
                return lost;
            }
            return 0;
        }
        public void DoEvent(IServerPlayer player, string toolCode, float score)
        {
            // Get the player-specific max credits (accounts for Weak/Claustrophobic penalties)
            int maxCredits = Definition.GetMaxCredits(player.Entity);

            // Skip all processing if already at max - completely invisible
            if (TotalCredits >= maxCredits) return;

            // Get or create progress for this specific pickaxe type
            var toolProgress = GetToolProgress(toolCode);

            int oldCredits = TotalCredits;

            // Apply sleep buff multiplier to points
            int modifiedPoints = (int)SeraphLevelingModSystem.ApplyXPMultiplier(player.PlayerUID, score);

            // Add points to THIS pickaxe's progress
            toolProgress.PartialCredit += modifiedPoints;

            // Check if we've earned any new credits with this pickaxe
            while (toolProgress.PartialCredit >= toolProgress.CurrentIncrementSize && TotalCredits < maxCredits)
            {
                // Earn a credit
                TotalCredits++;
                toolProgress.PartialCredit -= toolProgress.CurrentIncrementSize;
                toolProgress.CurrentIncrementSize += Definition.Tool.IncrementStep;

                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned credit {TotalCredits} with {toolCode}, next requires {toolProgress.CurrentIncrementSize} points");
            }

            Definition.MarkForSave(true);

            // Update last activity day for skill decay
            UpdateSkillActivityDay();

            // If credits increased, update the stat and notify player
            if (TotalCredits > oldCredits)
            {
                Definition.ApplyBonus(player, (PD)this);

                // Notify player of level up with the level as the bonus (the raw mining speed improvement)
                // This shows the true progress even when negative traits are still being cancelled
                SeraphLevelingModSystem.NotifyLevelUp(player,
                    Lang.Get($"seraphleveling:message-{Definition.Description}-level-up", TotalCredits, TotalCredits));

                // Check for trait unlocks that depend on mining level
                Definition.CheckUnlocks(player);
            }
        }
    }
}
