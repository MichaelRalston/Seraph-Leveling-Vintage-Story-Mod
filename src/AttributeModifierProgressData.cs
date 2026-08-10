using System;
using System.IO;
using System.Numerics;
using Vintagestory.API.Server;
using System.Text;
using Vintagestory.API.Common;


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
        /// <summary>Action taken toward the next credit.</summary>
        public float PartialCredit { get; set; } = 0; // formerly known as BlocksInIncrement
        /// <summary>Actions needed for the next credit (1000, 2000, 3000, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

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

        public int ApplyStatPenalty(double rawPenalty, StringBuilder sb, StringBuilder verboseSb)
        {
            int oldCredits = TotalCredits;
            float oldAcc = PartialCredit; int oldInc = CurrentIncrementSize;
            var (newCr, newAcc, newInc, lost) = SeraphLevelingModSystem.ApplySingleAccumulatorDecay(
                oldAcc, oldInc, oldCredits,
                rawPenalty, Definition.BaseIncrement, Definition.IncrementStep, verboseSb, Definition.SkillKey);
            TotalCredits = newCr; PartialCredit = (float)newAcc; CurrentIncrementSize = newInc;
            sb.AppendLine($"  {Definition.Name}: {oldCredits} \u2192 {newCr} (-{lost} credits, {rawPenalty:F0} pts), {oldAcc:F0}/{oldInc} \u2192 {(int)newAcc}/{newInc}");
            Definition.MarkForSave(true);
            if (lost > 0) return lost;
            return 0;
        }
        public virtual void WriteIncrementLine(StringBuilder sb)
        {
            sb.AppendLine($"Progress: {PartialCredit:F1}/{CurrentIncrementSize} {Definition.IncrementUnits}");
        }

        public void UpdateSkillActivityDay()
        {
            if (!SeraphLevelingModSystem.EnableSkillDecay) return;
            if (SeraphLevelingModSystem.ServerApi == null) return;

            LastActivityDay = SeraphLevelingModSystem.ServerApi.World.Calendar.TotalDays;
        }
        public virtual void CalculateIncrementSize()
        {
            // Calculate what the increment size should be at this level
            CurrentIncrementSize = Definition.BaseIncrement + (TotalCredits * Definition.IncrementStep);
        }

        public virtual TextCommandResult SetLevelFromCommand(IServerPlayer player, int newCredits, TextCommandCallingArgs args)
        {
            // Set the player's progress
            TotalCredits = newCredits;
            PartialCredit = 0;
            CalculateIncrementSize();

            Definition.MarkForSave(true);
            int bonusPercent = Definition.ApplyBonus(player, (PD)this);
            UpdateSkillActivityDay();

            return TextCommandResult.Success($"{Definition.Name} credits set to {newCredits} (+{bonusPercent}{Definition.Stat}).");
        }
    }

    public class LeveledPartialAttributeModifierProgressData(LeveledPartialAttributeModifierDefinition definition) : LeveledAttributeModifierProgressData<LeveledPartialAttributeModifierDefinition, LeveledPartialAttributeModifierProgressData>(definition)
    {
    }

    public class LeveledToolAttributeModifierProgressData(LeveledToolAttributeModifierDefinition definition) : LeveledAttributeModifierProgressData<LeveledToolAttributeModifierDefinition, LeveledToolAttributeModifierProgressData>(definition)
    {
    }
}
