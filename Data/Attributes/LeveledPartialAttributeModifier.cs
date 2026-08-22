using System;
using System.IO;
using System.Text;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using System.Collections.Generic;

namespace SeraphLeveling.Data.Attributes
{
    public abstract class LeveledPartialAttributeModifierDefinition : LeveledAttributeModifierDefinition<LeveledPartialAttributeModifierDefinition, LeveledPartialAttributeModifierProgressData>, IConstructable<LeveledPartialAttributeModifierDefinition, LeveledPartialAttributeModifierProgressData>
    {
        public virtual required int BaseIncrement { get; set; }
        public virtual required int IncrementStep { get; set; }
        public virtual required string IncrementUnits { get; init; }
        public override void ReadConfigData(Dictionary<string, int> dict)
        {
            base.ReadConfigData(dict);
            if (dict.TryGetValue("baseIncrement", out var baseInc)) BaseIncrement = baseInc;
            if (dict.TryGetValue("step", out var step)) IncrementStep = step;
        }
        public override Dictionary<string, int> GetConfigData()
        {
            return new(base.GetConfigData())
            {
                ["baseIncrement"] = BaseIncrement,
                ["step"] = IncrementStep,
            };
        }
        public static LeveledPartialAttributeModifierProgressData Create(LeveledPartialAttributeModifierDefinition definition) { return new LeveledPartialAttributeModifierProgressData(definition); }
        public override void ResetProgress(IServerPlayer player)
        {
            var progress = GetDict(player);
            progress.TotalCredits = 0;
            progress.PartialCredit = 0;
            progress.CurrentIncrementSize = BaseIncrement;
            progress.LastActivityDay = 0;
            PendingSave = true;
            ApplyBonus(player, progress);
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

                return TextCommandResult.Success($"Base {IncrementUnits} per increment set to {BaseIncrement}. New progress will require this many {IncrementUnits} for the first 1%.");
            }
            else
            {
                return TextCommandResult.Success($"Current base {IncrementUnits} per increment: {BaseIncrement}\nIncrement step: +{IncrementStep} per credit");
            }
        }
    }

    public class LeveledPartialAttributeModifierProgressData(LeveledPartialAttributeModifierDefinition definition) : LeveledAttributeModifierProgressData<LeveledPartialAttributeModifierDefinition, LeveledPartialAttributeModifierProgressData>(definition)
    {
        /// <summary>Action taken toward the next credit.</summary>
        public float PartialCredit { get; set; } = 0; // formerly known as BlocksInIncrement
        /// <summary>Actions needed for the next credit (1000, 2000, 3000, etc.).</summary>
        public int CurrentIncrementSize { get; set; } = definition.BaseIncrement;
        public void DoEvent(IServerPlayer player, float score)
        {
            if (SeraphLevelingModSystem.IsAttributeModifierDisabled(Definition)) return;
            // Skip all processing if already at max - completely invisible
            var maxCredits = Definition.GetMaxCredits(player.Entity);
            if (TotalCredits >= maxCredits) return;

            int oldCredits = TotalCredits;

            // Apply sleep buff multiplier to score
            float modifiedScore = SeraphLevelingModSystem.ApplyXPMultiplier(player.PlayerUID, float.CreateTruncating(score));

            // Add distance to progress
            PartialCredit += float.CreateTruncating(modifiedScore);

            // Check if we've earned any new credits
            var incrementStep = Definition.IncrementStep;
            var units = Definition.IncrementUnits;
            while (PartialCredit >= float.CreateTruncating(CurrentIncrementSize) && TotalCredits < maxCredits)
            {
                // Earn a credit
                TotalCredits++;
                PartialCredit -= float.CreateTruncating(CurrentIncrementSize);
                CurrentIncrementSize += incrementStep;

                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Player {player.PlayerName} earned {Definition.LongDescription} credit {TotalCredits}, next requires {CurrentIncrementSize} {units}");
            }

            // Mark for saving if any progress was made
            if (PartialCredit > 0f || TotalCredits > oldCredits)
            {
                Definition.PendingSave = true;
            }

            // If credits increased, update the stat and notify player
            if (TotalCredits > oldCredits)
            {
                UpdateSkillActivityDay();
                Definition.ApplyBonus(player, this);

                // Notify player of level up with raw improvement (shows progress even when capped)
                SeraphLevelingModSystem.NotifyLevelUp(player,
                    Lang.Get($"seraphleveling:message-{Definition.SkillKey}-level-up", TotalCredits, TotalCredits));
            }
        }
        public override void WriteIncrementLine(StringBuilder sb)
        {
            sb.AppendLine($"Progress: {PartialCredit:F1}/{CurrentIncrementSize} {Definition.IncrementUnits}");
        }
        public override void ZeroPartialCredit()
        {
            PartialCredit = 0;
        }
        public override int ApplyStatPenalty(double rawPenalty, StringBuilder sb, StringBuilder verboseSb)
        {
            int oldCredits = TotalCredits;
            float oldAcc = PartialCredit; int oldInc = CurrentIncrementSize;
            var (newCr, newAcc, newInc, lost) = SeraphLevelingModSystem.ApplySingleAccumulatorDecay(
                oldAcc, oldInc, oldCredits,
                rawPenalty, Definition.BaseIncrement, Definition.IncrementStep, verboseSb, Definition.SkillKey);
            TotalCredits = newCr; PartialCredit = (float)newAcc; CurrentIncrementSize = newInc;
            sb.AppendLine($"  {Definition.Name}: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc:F0}/{oldInc} \u2192 {(int)newAcc}/{newInc}");
            Definition.PendingSave = true;
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
}
