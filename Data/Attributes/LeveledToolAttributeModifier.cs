using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using SeraphLeveling.Data.Tools;
using System.Numerics;
using System.ComponentModel;

namespace SeraphLeveling.Data.Attributes
{
    public abstract record class LeveledToolAttributeModifierDefinition<D, PD, N> : LeveledAttributeModifierDefinition<D, PD> where PD : LeveledToolAttributeModifierProgressData<D, PD, N> where D : LeveledToolAttributeModifierDefinition<D, PD, N>, IConstructable<D, PD> where N : INumber<N>
    {
        public required ToolDefinition Tool { get; init; }
        public override void ResetProgress(IServerPlayer player)
        {
            var progress = GetDict(player);
            progress.TotalCredits = 0;
            var toolEntries = progress.ToolProgress.Select(kvp =>
                (kvp.Key, double.CreateTruncating<N>(kvp.Value.PartialCredit), kvp.Value.CurrentIncrementSize)).ToList();
            progress.LastActivityDay = 0;
            PendingSave = true;
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
                        (kvp.Key, double.CreateTruncating<N>(kvp.Value.PartialCredit), kvp.Value.CurrentIncrementSize)).ToList();
                    double rawPenalty;
                    if (toolEntries.Count > 0)
                    {
                        rawPenalty = BaseIncrement * SeraphLevelingModSystem.DeathPenaltyFraction * Math.Sqrt(Math.Max(1, progress.TotalCredits));
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
        public new TextCommandResult OnTraitBaseCommand(TextCommandCallingArgs args)
        {
            int? newValue = (int?)args[0];

            if (newValue.HasValue)
            {
                if (newValue.Value < 1)
                {
                    return TextCommandResult.Error($"Base {IncrementUnits} per increment must be at least 1");
                }

                BaseIncrement = newValue.Value;
                SeraphLevelingModSystem.pendingConfigSave = true;

                return TextCommandResult.Success($"Base {IncrementUnits} per increment set to {BaseIncrement}. New {Tool.Name} will require this many {IncrementUnits} for the first 1%.");
            }
            else
            {
                return TextCommandResult.Success($"Current base {IncrementUnits} per increment: {BaseIncrement}\nIncrement step: +{IncrementStep} per credit");
            }
        }

    }

    public class LevelableTool<N> where N : INumber<N>
    {
        public virtual void WriteOut(BinaryWriter writer)
        {
            switch (PartialCredit)
            {
                case int i:
                    writer.Write(i);
                    break;
                case float f:
                    writer.Write(f);
                    break;
                default:
                    throw new NotSupportedException($"Tools with increments of type {typeof(N)} are not supported");
            }
            writer.Write(CurrentIncrementSize);
        }
        /// <summary>Points accumulated toward the next credit with this tool.</summary>
        public N PartialCredit { get; set; }

        /// <summary>Points needed for the next credit with this tool (100, 200, 300, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

    }

    public abstract class LeveledToolAttributeModifierProgressData<D, PD, N>(D def) : LeveledAttributeModifierProgressData<D, PD>(def) where PD : LeveledToolAttributeModifierProgressData<D, PD, N> where D : LeveledToolAttributeModifierDefinition<D, PD, N>, IConstructable<D, PD> where N : INumber<N>
    {
        public Dictionary<string, LevelableTool<N>> ToolProgress { get; init; } = new Dictionary<string, LevelableTool<N>>();
        public LevelableTool<N> GetToolProgress(string toolCode)
        {
            if (!ToolProgress.TryGetValue(toolCode, out var progress))
            {
                progress = new LevelableTool<N>()
                {
                    CurrentIncrementSize = Definition.BaseIncrement
                };
                ToolProgress[toolCode] = progress;
            }
            return progress;
        }

        public TextCommandResult SetLevel(IServerPlayer player, int level, string toolName)
        {
            int maxCredits = Definition.GetMaxCredits(player.Entity);
            int oldCredits = TotalCredits;
            if (level < 0)
                return TextCommandResult.Error("Credits cannot be negative.");

            if (toolName != null)
            {
                // Per-tool mode: set credits on a specific pickaxe without clearing others
                int oldToolCredits = 0;
                if (ToolProgress.TryGetValue(toolName, out var existingTool))
                    oldToolCredits = SeraphLevelingModSystem.CalculateToolCredits(existingTool.CurrentIncrementSize, Definition.BaseIncrement, Definition.IncrementStep);

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
                    toolProgress.CurrentIncrementSize = Definition.BaseIncrement + (level * Definition.IncrementStep);
                    toolProgress.PartialCredit = N.Zero;
                }

                TotalCredits = SeraphLevelingModSystem.RecalculateTotalCreditsFromTools(
                    ToolProgress, p => p.CurrentIncrementSize,
                    Definition.BaseIncrement, Definition.IncrementStep);

                Definition.PendingSave = true;
                int bonusPercent = Definition.ApplyBonus(player, (PD)this);
                Definition.OnCreditsChanged(player, oldCredits, (PD)this);
                UpdateSkillActivityDay();

                return TextCommandResult.Success($"Set {level} credits on {toolName}. Total: {TotalCredits}/{maxCredits} ({Definition.Direction}{bonusPercent}{Definition.Stat}).");
            }
            else
            {
                // Total mode: set TotalCredits directly and clear per-tool progress
                if (level > maxCredits)
                    return TextCommandResult.Error($"Credits cannot exceed max ({maxCredits}).");

                TotalCredits = level;
                ToolProgress.Clear();

                Definition.PendingSave = true;
                int bonusPercent = Definition.ApplyBonus(player, (PD)this);
                Definition.OnCreditsChanged(player, oldCredits, (PD)this);
                UpdateSkillActivityDay();

                return TextCommandResult.Success($"{Definition.Name} credits set to {level} (+{bonusPercent}{Definition.Stat}). Per-tool progress reset.");
            }
        }
        public override TextCommandResult SetLevelFromCommand(IServerPlayer player, int level, TextCommandCallingArgs args)
        {
            string toolName = (string)args[1];
            return SetLevel(player, level, toolName);
        }
        public override void ReadVersion(byte version, BinaryReader reader)
        {
            int toolCount;
            switch (version)
            {
                case 1:
                    TotalCredits = reader.ReadInt32();
                    toolCount = reader.ReadInt32();
                    for (int i = 0; i < toolCount; i++)
                    {
                        var toolCode = reader.ReadString();
                        var partialCredit = typeof(N) switch
                        {
                            var t when t == typeof(int) => N.CreateTruncating(reader.ReadInt32()),
                            var t when t == typeof(float) => N.CreateTruncating(reader.ReadSingle()),
                            _ => throw new NotSupportedException($"Tools with increments of type {typeof(N)} are not supported")
                        };
                        var toolProgressRecord = new LevelableTool<N>
                        {
                            PartialCredit = partialCredit,
                            CurrentIncrementSize = reader.ReadInt32()
                        };
                        ToolProgress[toolCode] = toolProgressRecord;
                    }
                    break;
                case 2:
                    TotalCredits = reader.ReadInt32();
                    LastActivityDay = reader.ReadDouble();

                    toolCount = reader.ReadInt32();
                    for (int i = 0; i < toolCount; i++)
                    {
                        var toolCode = reader.ReadString();
                        var partialCredit = typeof(N) switch
                        {
                            var t when t == typeof(int) => N.CreateTruncating(reader.ReadInt32()),
                            var t when t == typeof(float) => N.CreateTruncating(reader.ReadSingle()),
                            _ => throw new NotSupportedException($"Tools with increments of type {typeof(N)} are not supported")
                        };
                        var toolProgress = new LevelableTool<N>
                        {
                            PartialCredit = partialCredit,
                            CurrentIncrementSize = reader.ReadInt32()
                        };
                        ToolProgress[toolCode] = toolProgress;
                    }
                    break;
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
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
                (kvp.Key, double.CreateTruncating(kvp.Value.PartialCredit), kvp.Value.CurrentIncrementSize)).ToList();

            if (toolEntries.Count > 0)
            {
                var (newCr, lost) = SeraphLevelingModSystem.ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                    Definition.BaseIncrement, Definition.IncrementStep, oldCredits,
                    (k, a, s) =>
                    {
                        if (ToolProgress.TryGetValue(k, out var p))
                        {
                            p.PartialCredit = N.CreateTruncating(Math.Floor(a)); p.CurrentIncrementSize = s;
                        }
                    },
                    k => ToolProgress.Remove(k), verboseSb, Definition.Name);
                TotalCredits = newCr;
                sb.AppendLine($"  {Definition.Description}: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts)");
                foreach (var entry in toolEntries)
                {
                    int oldToolCr = Definition.IncrementStep > 0 ? (entry.Item3 - Definition.BaseIncrement) / Definition.IncrementStep : 0;
                    if (ToolProgress.TryGetValue(entry.Item1, out var after))
                    {
                        int newToolCr = Definition.IncrementStep > 0 ? (after.CurrentIncrementSize - Definition.BaseIncrement) / Definition.IncrementStep : 0;
                        int toolLost = oldToolCr - newToolCr;
                        sb.AppendLine($"    {entry.Item1}: {(int)entry.Item2}/{entry.Item3} \u2192 {after.PartialCredit:F0}/{after.CurrentIncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                    }
                    else
                        sb.AppendLine($"    {entry.Item1}: {(int)entry.Item2}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
                }
                Definition.PendingSave = true;
                if (lost > 0) return lost;
            }
            else
            {
                int lost = Math.Min((int)rawPenalty, oldCredits);
                TotalCredits -= lost;
                if (lost > 0) { sb.AppendLine($"  {Definition.Name}: {oldCredits} \u2192 {TotalCredits} (-{lost} credits)"); }
                Definition.PendingSave = true;
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

            // Get or create progress for this specific tool type
            var toolProgress = GetToolProgress(toolCode);

            int oldCredits = TotalCredits;

            // Apply sleep buff multiplier to points
            N modifiedPoints = N.CreateTruncating(SeraphLevelingModSystem.ApplyXPMultiplier(player.PlayerUID, score));

            // Add points to THIS tool's progress
            toolProgress.PartialCredit += modifiedPoints;

            // Check if we've earned any new credits with this tool
            while (toolProgress.PartialCredit >= N.CreateTruncating(toolProgress.CurrentIncrementSize) && TotalCredits < maxCredits)
            {
                // Earn a credit
                TotalCredits++;
                toolProgress.PartialCredit -= N.CreateTruncating(toolProgress.CurrentIncrementSize);
                toolProgress.CurrentIncrementSize += Definition.IncrementStep;

                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned credit {TotalCredits} with {toolCode}, next requires {toolProgress.CurrentIncrementSize} points");
            }

            Definition.PendingSave = true;

            // Update last activity day for skill decay
            UpdateSkillActivityDay();

            // If credits increased, update the stat and notify player
            if (TotalCredits > oldCredits)
            {
                Definition.ApplyBonus(player, (PD)this);

                // Notify player of level up with the level as the bonus (the raw mining speed improvement, etc)
                // This shows the true progress even when negative traits are still being cancelled
                ;
                SeraphLevelingModSystem.NotifyLevelUp(player,
                    Lang.Get($"seraphleveling:message-{Definition.SkillKey}-level-up", TotalCredits, TotalCredits));

                // Check for trait unlocks that depend on trait level
                Definition.OnCreditsChanged(player, oldCredits, (PD)this);
            }
        }
    }
}
