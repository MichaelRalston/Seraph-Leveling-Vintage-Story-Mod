using System;
using System.IO;
using System.Collections.Generic;
using Vintagestory.API.Server;
using System.Text;
using Vintagestory.API.Common;
using System.Linq;


namespace SeraphLeveling
{
    public interface IAttributeModifierProgressData
    {

    }
    public abstract class AAttributeModifierProgressData<D, PD> : IAttributeModifierProgressData where PD : AAttributeModifierProgressData<D, PD> where D : AttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        protected D Definition { get; init; }

        public AAttributeModifierProgressData(D definition)
        {
            Definition = definition;
        }

        public abstract void ReadVersion(byte version, BinaryReader reader);
        public abstract void WriteOut(BinaryWriter writer);
    }

    public abstract class LeveledAttributeModifierProgressData<D, PD>(D definition) : AAttributeModifierProgressData<D, PD>(definition) where PD : LeveledAttributeModifierProgressData<D, PD> where D : LeveledAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        /// <summary>Total credits earned (each credit = 1% bonus).</summary>
        public int TotalCredits { get; set; }
        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }

        public void UpdateSkillActivityDay()
        {
            if (!SeraphLevelingModSystem.EnableSkillDecay) return;
            if (SeraphLevelingModSystem.ServerApi == null) return;

            LastActivityDay = SeraphLevelingModSystem.ServerApi.World.Calendar.TotalDays;
        }

        public virtual TextCommandResult SetLevelFromCommand(IServerPlayer player, int newCredits, TextCommandCallingArgs args)
        {
            // Set the player's progress
            TotalCredits = newCredits;
            ZeroPartialCredit();
            CalculateIncrementSize();

            Definition.MarkForSave();
            int bonusPercent = Definition.ApplyBonus(player, (PD)this);
            UpdateSkillActivityDay();

            return TextCommandResult.Success($"{Definition.Name} credits set to {newCredits} (+{bonusPercent}{Definition.Stat}).");
        }
        public virtual void ZeroPartialCredit()
        {
        }
        public virtual void CalculateIncrementSize()
        {
        }
        public virtual void WriteIncrementLine(StringBuilder sb)
        {
            // Empty.
        }

    }

    public class LeveledPartialAttributeModifierProgressData(LeveledPartialAttributeModifierDefinition definition) : LeveledAttributeModifierProgressData<LeveledPartialAttributeModifierDefinition, LeveledPartialAttributeModifierProgressData>(definition)
    {
        public override void WriteIncrementLine(StringBuilder sb)
        {
            sb.AppendLine($"Progress: {PartialCredit:F1}/{CurrentIncrementSize} {Definition.IncrementUnits}");
        }
        /// <summary>Action taken toward the next credit.</summary>
        public float PartialCredit { get; set; } = 0; // formerly known as BlocksInIncrement
        /// <summary>Actions needed for the next credit (1000, 2000, 3000, etc.).</summary>
        public int CurrentIncrementSize { get; set; }
        public override void ZeroPartialCredit()
        {
            PartialCredit = 0;
        }
        public int ApplyStatPenalty(double rawPenalty, StringBuilder sb, StringBuilder verboseSb)
        {
            int oldCredits = TotalCredits;
            float oldAcc = PartialCredit; int oldInc = CurrentIncrementSize;
            var (newCr, newAcc, newInc, lost) = SeraphLevelingModSystem.ApplySingleAccumulatorDecay(
                oldAcc, oldInc, oldCredits,
                rawPenalty, Definition.BaseIncrement, Definition.IncrementStep, verboseSb, Definition.SkillKey);
            TotalCredits = newCr; PartialCredit = (float)newAcc; CurrentIncrementSize = newInc;
            sb.AppendLine($"  {Definition.Name}: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc:F0}/{oldInc} \u2192 {(int)newAcc}/{newInc}");
            Definition.MarkForSave();
            if (lost > 0) return lost;
            return 0;
        }
        public override void ReadVersion(byte version, BinaryReader reader)
        {
            switch (version)
            {
                case 1:
                    TotalCredits = reader.ReadInt32();
                    PartialCredit = reader.ReadSingle();
                    CurrentIncrementSize = reader.ReadInt32();
                    break;
                case 2:
                    TotalCredits = reader.ReadInt32();
                    PartialCredit = reader.ReadSingle();
                    CurrentIncrementSize = reader.ReadInt32();
                    LastActivityDay = reader.ReadDouble();
                    break;
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }
        public override void WriteOut(BinaryWriter writer)
        {
            writer.Write(TotalCredits);
            writer.Write(PartialCredit);
            writer.Write(CurrentIncrementSize);
            writer.Write(LastActivityDay);
        }
        public override void CalculateIncrementSize()
        {
            // Calculate what the increment size should be at this level
            CurrentIncrementSize = Definition.BaseIncrement + (TotalCredits * Definition.IncrementStep);
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
                progress = new LevelableTool();
                ToolProgress[toolCode] = progress;
            }
            return progress;
        }
        public override TextCommandResult SetLevelFromCommand(IServerPlayer player, int level, TextCommandCallingArgs args)
        {
            string toolName = (string)args[1];
            string playerUid = player.PlayerUID;
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
                    var pickaxeProgress = GetToolProgress(toolName);
                    pickaxeProgress.CurrentIncrementSize = Definition.Tool.BaseIncrement + (level * Definition.Tool.IncrementStep);
                    pickaxeProgress.PartialCredit = 0;
                }

                TotalCredits = SeraphLevelingModSystem.RecalculateTotalCreditsFromTools(
                    ToolProgress, p => p.CurrentIncrementSize,
                    Definition.Tool.BaseIncrement, Definition.Tool.IncrementStep);

                Definition.MarkForSave();
                int bonusPercent = Definition.ApplyBonus(player, (PD)this);
                Definition.CheckUnlocks(player);
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

                Definition.MarkForSave();
                int bonusPercent = Definition.ApplyBonus(player, (PD)this);
                Definition.CheckUnlocks(player);
                UpdateSkillActivityDay();

                return TextCommandResult.Success($"Mining credits set to {level} (+{bonusPercent}% mining speed). Per-pickaxe progress reset.");
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
                Definition.MarkForSave();
                if (lost > 0) return lost;
            }
            else
            {
                int lost = Math.Min((int)rawPenalty, oldCredits);
                TotalCredits -= lost;
                if (lost > 0) { sb.AppendLine($"  {Definition.Name}: {oldCredits} \u2192 {TotalCredits} (-{lost} credits)"); }
                Definition.MarkForSave();
                return lost;
            }
            return 0;
        }
    }
}
