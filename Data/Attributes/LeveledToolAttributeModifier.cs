using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using SeraphLeveling.Data.Tools;
using System.Collections.Concurrent;
using System.Numerics;
using System.ComponentModel;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using SeraphLeveling.Data.Mods;
using Vintagestory.GameContent;

namespace SeraphLeveling.Data.Attributes
{
    public enum SimpleToolProgress { SimpleToolProgress };
    public struct IncrementData
    {
        public required int BaseIncrement { get; set; }
        public required int IncrementStep { get; set; }
        public required string IncrementUnits { get; set; }
    };

    public abstract class LeveledToolAttributeModifierDefinition<D, PD, E> : LeveledAttributeModifierDefinition<D, PD> where PD : LeveledToolAttributeModifierProgressData<D, PD, E> where D : LeveledToolAttributeModifierDefinition<D, PD, E>, IConstructable<D, PD> where E : Enum
    {
        public override byte PersistenceVersion { get; init; } = 3;
        public required ConcurrentDictionary<E, IncrementData> IncrementData { get; init; }
        public int BaseIncrement
        {
            get => IncrementData[default].BaseIncrement; set { var d = IncrementData.GetOrAdd(default, _ => new IncrementData() { BaseIncrement = value, IncrementStep = value, IncrementUnits = "" }); d.BaseIncrement = value; }
        }
        public int IncrementStep
        {
            get => IncrementData[default].IncrementStep; set { var d = IncrementData.GetOrAdd(default, _ => new IncrementData() { BaseIncrement = value, IncrementStep = value, IncrementUnits = "" }); d.IncrementStep = value; }
        }
        public string IncrementUnits
        {
            get => IncrementData[default].IncrementUnits; set { var d = IncrementData.GetOrAdd(default, _ => new IncrementData() { BaseIncrement = 0, IncrementStep = 0, IncrementUnits = value }); d.IncrementUnits = value; }
        }
        public required ToolDefinition Tool { get; init; }
        public override void ResetProgress(IServerPlayer player)
        {
            var progress = GetDict(player);
            progress.TotalCredits = 0;
            progress.ToolProgress.Clear();
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
                    var rawPenalty = Math.Floor(SeraphLevelingModSystem.DeathPenaltyFraction * Math.Sqrt(Math.Max(1, progress.TotalCredits)));

                    return progress.ApplyStatPenalty(rawPenalty, sb, null);
                }
            }
            return 0;
        }
        public override TextCommandResult HandleIncrementCommand(TextCommandCallingArgs args, int indexOffset)
        {
            int? newValue = (int?)args[0 + indexOffset];

            if (newValue.HasValue)
            {
                if (newValue.Value < 0)
                {
                    return TextCommandResult.Error("Increment step cannot be negative");
                }

                IncrementStep = newValue.Value;
                SeraphLevelingModSystem.pendingConfigSave = true;

                return TextCommandResult.Success($"{Name} increment step set to +{IncrementStep} per credit.\nProgression: {BaseIncrement}, {BaseIncrement + IncrementStep}, {BaseIncrement + IncrementStep * 2}...");
            }
            else
            {
                return TextCommandResult.Success($"Current {LongDescription} increment step: +{IncrementStep} per credit\nProgression: {BaseIncrement}, {BaseIncrement + IncrementStep}, {BaseIncrement + IncrementStep * 2}...");
            }
        }
        public override TextCommandResult HandleBaseCommand(TextCommandCallingArgs args, int indexOffset)
        {
            int? newValue = (int?)args[0 + indexOffset];

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

    public class CreditData
    {
        public float Amount { get; set; }
        public int IncrementSize { get; set; }
    }

    public class LevelableTool<D, PD, E> where PD : LeveledToolAttributeModifierProgressData<D, PD, E> where D : LeveledToolAttributeModifierDefinition<D, PD, E>, IConstructable<D, PD> where E : Enum
    {
        public virtual void WriteOut(BinaryWriter writer)
        {
            var snapshot = PartialCredit.ToArray();
            writer.Write(HasBeenUsed);
            writer.Write(snapshot.Length);
            foreach (var kvp in snapshot)
            {
                writer.Write(Convert.ToInt32(kvp.Key));
                writer.Write(kvp.Value.Amount);
                writer.Write(kvp.Value.IncrementSize);
            }
        }
        public ConcurrentDictionary<E, CreditData> PartialCredit { get; set; } = [];
        public bool HasBeenUsed { get; set; }
        public required D Definition { get; init; }

        public CreditData GetPartialCredit(E e)
        {
            return PartialCredit.GetOrAdd(e, _ => new CreditData
            {
                IncrementSize = Definition.IncrementData[e].BaseIncrement,
                Amount = 0,
            });
        }
        public int GetLevel(E e)
        {
            return (PartialCredit[e].IncrementSize - Definition.IncrementData[e].BaseIncrement) / Definition.IncrementData[e].IncrementStep;
        }
    }

    public abstract class LeveledToolAttributeModifierProgressData<D, PD, E>(D def) : LeveledAttributeModifierProgressData<D, PD>(def) where PD : LeveledToolAttributeModifierProgressData<D, PD, E> where D : LeveledToolAttributeModifierDefinition<D, PD, E>, IConstructable<D, PD> where E : Enum
    {
        public ConcurrentDictionary<string, LevelableTool<D, PD, E>> ToolProgress { get; init; } = [];
        public LevelableTool<D, PD, E> GetToolProgress(string toolCode)
        {
            if (!ToolProgress.TryGetValue(toolCode, out var progress))
            {
                progress = new LevelableTool<D, PD, E>()
                {
                    Definition = Definition
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
                // Per-tool mode: set credits on a specific tool without clearing others
                int oldToolCredits = 0;
                if (ToolProgress.TryGetValue(toolName, out var existingTool))
                    oldToolCredits = SeraphLevelingModSystem.CalculateToolCredits(existingTool.PartialCredit[default].IncrementSize, Definition.BaseIncrement, Definition.IncrementStep);

                int projectedTotal = TotalCredits - oldToolCredits + level;
                if (projectedTotal > maxCredits)
                    return TextCommandResult.Error($"Setting {level} credits on {toolName} would result in {projectedTotal} total credits, exceeding max ({maxCredits}).");

                if (level == 0)
                {
                    ToolProgress.TryRemove(toolName, out var _);
                }
                else
                {
                    var toolProgress = GetToolProgress(toolName);
                    var pc = toolProgress.GetPartialCredit(default);
                    pc.IncrementSize = Definition.BaseIncrement + (level * Definition.IncrementStep);
                    pc.Amount = 0;
                }

                int totalCredits = 0;
                foreach (var tp in ToolProgress)
                {
                    foreach (var pc in tp.Value.PartialCredit)
                    {
                        totalCredits += tp.Value.GetLevel(pc.Key);
                    }
                }
                TotalCredits = totalCredits;

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
        public override TextCommandResult SetLevelFromCommand(IServerPlayer player, int level, TextCommandCallingArgs args, int indexOffset)
        {
            string toolName = (string)args[1 + indexOffset];
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
                        var partialCredit = reader.ReadSingle();
                        var incrementSize = reader.ReadInt32();
                        var toolProgressRecord = new LevelableTool<D, PD, E>
                        {
                            Definition = Definition,
                            PartialCredit = new ConcurrentDictionary<E, CreditData>
                            {
                                [default] = new CreditData { Amount = partialCredit, IncrementSize = incrementSize }
                            },
                            HasBeenUsed = false,
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
                        var partialCredit = reader.ReadSingle();
                        var incrementSize = reader.ReadInt32();
                        var toolProgressRecord = new LevelableTool<D, PD, E>
                        {
                            Definition = Definition,
                            PartialCredit = new ConcurrentDictionary<E, CreditData>
                            {
                                [default] = new CreditData { Amount = partialCredit, IncrementSize = incrementSize }
                            },
                            HasBeenUsed = false,
                        };
                        ToolProgress[toolCode] = toolProgressRecord;
                    }
                    break;
                case 3:
                    TotalCredits = reader.ReadInt32();
                    LastActivityDay = reader.ReadDouble();

                    toolCount = reader.ReadInt32();
                    for (int i = 0; i < toolCount; i++)
                    {
                        var toolCode = reader.ReadString();
                        var hasBeenUsed = reader.ReadBoolean();
                        var length = reader.ReadInt32();
                        var toolProgressRecord = new LevelableTool<D, PD, E>
                        {
                            Definition = Definition,
                            PartialCredit = [],
                            HasBeenUsed = hasBeenUsed,
                        };
                        for (int j = 0; j < length; j++)
                        {
                            E key = (E)Enum.ToObject(typeof(E), reader.ReadInt32());

                            var partialCredit = reader.ReadSingle();
                            var incrementSize = reader.ReadInt32();
                            toolProgressRecord.PartialCredit[key] = new CreditData { Amount = partialCredit, IncrementSize = incrementSize };
                        }
                        ToolProgress[toolCode] = toolProgressRecord;
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
                foreach (var kvp in ToolProgress.OrderBy(p => p.Value.PartialCredit.Sum(t => p.Value.GetLevel(t.Key))))
                {
                    string toolName = kvp.Key;
                    // Simplify the display name (remove "game:" prefix if present)
                    if (toolName.StartsWith("game:"))
                        toolName = toolName.Substring(5);

                    foreach (var pcKvp in kvp.Value.PartialCredit)
                    {
                        sb.AppendLine($"  {toolName}: {pcKvp.Value.Amount}/{pcKvp.Value.IncrementSize} {Definition.IncrementData[pcKvp.Key].IncrementUnits}");
                    }
                }
            }
            else
            {
                sb.AppendLine($"\nNo {Definition.Tool.Name} progress yet.");
            }
        }

        public static double DrainAccumulatorsLeveling(List<(string key, E e, double value)> accumulators, double penalty)
        {
            if (accumulators == null || accumulators.Count == 0 || penalty <= 0) return penalty;

            // Sort descending by value
            accumulators.Sort((a, b) => b.value.CompareTo(a.value));

            double remaining = penalty;

            while (remaining > 0)
            {
                // Find the current top value
                double topValue = accumulators[0].value;
                if (topValue <= 0) break; // All accumulators are drained

                // Find how many entries share the top tier and the next level down
                int topCount = 1;
                double nextLevel = 0;
                for (int i = 1; i < accumulators.Count; i++)
                {
                    if (accumulators[i].value >= topValue - 0.001)
                    {
                        topCount++;
                    }
                    else
                    {
                        nextLevel = accumulators[i].value;
                        break;
                    }
                }

                // Cost to bring all top entries down to nextLevel
                double dropPerEntry = topValue - nextLevel;
                double totalCost = dropPerEntry * topCount;

                if (remaining >= totalCost)
                {
                    // Fully drain this tier to the next level
                    for (int i = 0; i < topCount; i++)
                    {
                        accumulators[i] = (accumulators[i].key, accumulators[i].e, nextLevel);
                    }
                    remaining -= totalCost;
                }
                else
                {
                    // Partially drain: distribute remaining evenly among top entries
                    double drainPerEntry = remaining / topCount;
                    for (int i = 0; i < topCount; i++)
                    {
                        accumulators[i] = (accumulators[i].key, accumulators[i].e, accumulators[i].value - drainPerEntry);
                    }
                    remaining = 0;
                }
            }

            // Clamp any negative values from floating point
            for (int i = 0; i < accumulators.Count; i++)
            {
                if (accumulators[i].value < 0)
                    accumulators[i] = (accumulators[i].key, accumulators[i].e, 0);
            }

            return remaining;
        }


        public static (int newTotalCredits, int creditsLost) ApplyAbsolutePositionDecay(
            List<(string key, E e, double accumulator, int incrementSize)> toolEntries,
            double rawPenalty, int baseIncrement, int incrementStep, int oldTotalCredits,
            Action<string, E, double, int> writeBack,
            Action<string> removeEntry,
            StringBuilder verboseLog, string skillName)
        {
            // Step 1: Convert to absolute positions
            var absPositions = new List<(string key, E e, double value)>();
            foreach (var (key, e, accumulator, incrementSize) in toolEntries)
            {
                double absPos = SeraphLevelingModSystem.ToolToAbsolutePosition(accumulator, incrementSize, baseIncrement, incrementStep);
                absPositions.Add((key, e, absPos));
            }

            // Step 2: Water-level drain
            double remaining = DrainAccumulatorsLeveling(absPositions, rawPenalty);

            // Step 3: Convert back and write
            int newTotalCredits = 0;
            var toRemove = new List<string>();
            foreach (var (key, e, value) in absPositions)
            {
                var (credits, accum, incSize) = SeraphLevelingModSystem.AbsolutePositionToToolState(value, baseIncrement, incrementStep);
                if (credits == 0 && accum < 0.001)
                {
                    toRemove.Add(key);
                }
                else
                {
                    writeBack(key, e, accum, incSize);
                    newTotalCredits += credits;
                }

                if (verboseLog != null)
                    verboseLog.AppendLine($"  [{skillName}] {key}: absPos={value:F1} -> cr={credits}, acc={accum:F1}, inc={incSize}");
            }

            foreach (var key in toRemove)
                removeEntry(key);

            // If there's remaining penalty after all tools drained to zero, subtract from credits directly
            if (remaining > 0.001 && newTotalCredits > 0)
            {
                // This shouldn't normally happen since absolute positions encompass credits,
                // but handle edge case of oldTotalCredits > sum of per-tool credits
                int extraLoss = (int)Math.Floor(remaining / baseIncrement);
                newTotalCredits = Math.Max(0, newTotalCredits - extraLoss);
            }

            int creditsLost = Math.Max(0, oldTotalCredits - newTotalCredits);
            return (newTotalCredits, creditsLost);
        }

        public override int ApplyStatPenalty(double rawPenalty, StringBuilder sb, StringBuilder verboseSb)
        {
            int oldCredits = TotalCredits;
            var toolEntries = ToolProgress.SelectMany(kvp => kvp.Value.PartialCredit.Select(innerKvp => (kvp.Key, innerKvp.Key, (double)innerKvp.Value.Amount, innerKvp.Value.IncrementSize))).ToList();

            if (toolEntries.Count > 0)
            {
                var (newCr, lost) = ApplyAbsolutePositionDecay(toolEntries, rawPenalty,
                    Definition.BaseIncrement, Definition.IncrementStep, oldCredits,
                    (k, e, a, s) =>
                    {
                        if (ToolProgress.TryGetValue(k, out var p))
                        {
                            var pc = p.GetPartialCredit(e);
                            pc.Amount = (float)Math.Floor(a);
                            pc.IncrementSize = s;
                        }
                    },
                    k => ToolProgress.TryRemove(k, out var _), verboseSb, Definition.Name);
                TotalCredits = newCr;
                sb.AppendLine($"  {Definition.LongDescription}: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts)");
                foreach (var entry in toolEntries)
                {
                    int oldToolCr = Definition.IncrementData[entry.Item2].IncrementStep > 0 ? (entry.IncrementSize - Definition.IncrementData[entry.Item2].BaseIncrement) / Definition.IncrementData[entry.Item2].IncrementStep : 0;
                    if (ToolProgress.TryGetValue(entry.Item1, out var after))
                    {
                        var pc = after.GetPartialCredit(entry.Item2);
                        int newToolCr = Definition.IncrementData[entry.Item2].IncrementStep > 0 ? (pc.IncrementSize - Definition.IncrementData[entry.Item2].BaseIncrement) / Definition.IncrementStep : 0;
                        int toolLost = oldToolCr - newToolCr;
                        sb.AppendLine($"    {entry.Item1}: {(int)entry.Item3}/{entry.Item3} \u2192 {after.PartialCredit:F0}/{pc.IncrementSize}{(toolLost > 0 ? $" (-{toolLost} cr)" : "")}");
                    }
                    else
                        sb.AppendLine($"    {entry.Item1}: {(int)entry.Item3}/{entry.Item3} \u2192 removed (-{oldToolCr} cr)");
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
        public void DoEvent(IServerPlayer player, string toolCode, float score, E scoreType = default)
        {
            // Get the player-specific max credits (accounts for Weak/Claustrophobic penalties)
            int maxCredits = Definition.GetMaxCredits(player.Entity);

            // Skip all processing if already at max - completely invisible
            if (TotalCredits >= maxCredits) return;

            // Get or create progress for this specific tool type
            var toolProgress = GetToolProgress(toolCode);

            int oldCredits = TotalCredits;

            // Apply sleep buff multiplier to points
            float modifiedPoints = SeraphLevelingModSystem.ApplyXPMultiplier(player.PlayerUID, score);

            // Add points to THIS tool's progress
            var partialCredit = toolProgress.GetPartialCredit(scoreType);
            partialCredit.Amount += modifiedPoints;

            // Check if we've earned any new credits with this tool
            while ((partialCredit.Amount >= partialCredit.IncrementSize) && TotalCredits < maxCredits)
            {
                // Earn a credit
                TotalCredits++;
                partialCredit.Amount -= partialCredit.IncrementSize;
                partialCredit.IncrementSize += Definition.IncrementStep;

                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned credit {TotalCredits} with {toolCode}, next requires {partialCredit.IncrementSize} points");
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
