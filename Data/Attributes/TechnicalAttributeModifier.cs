using System;
using System.IO;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public record class TechnicalAttributeModifierDefinition : UnlockedAttributeModifierDefinition<TechnicalAttributeModifierDefinition, TechnicalAttributeModifierProgressData>, IConstructable<TechnicalAttributeModifierDefinition, TechnicalAttributeModifierProgressData>
    {
        public static TechnicalAttributeModifierProgressData Create(TechnicalAttributeModifierDefinition def)
        {
            return new TechnicalAttributeModifierProgressData(def);
        }

        public override void ResetProgress(IServerPlayer player)
        {
            var progress = GetDict(player);
            progress.TranslocatorsRepaired = 0;
            base.ResetProgress(player);
        }

        public override void GetTraitAllCommandLine(IPlayer player, StringBuilder sb) {
            var progress = GetDict(player);
            sb.AppendLine($"{Name}: {progress.TranslocatorsRepaired}/{SeraphLevelingModSystem.TechnicalRequiredTranslocatorRepairs} translocators ({(progress.IsUnlocked ? "UNLOCKED" : "locked")})");
        }
    }

    public class TechnicalAttributeModifierProgressData(TechnicalAttributeModifierDefinition definition) : UnlockedAttributeModifierProgressData<TechnicalAttributeModifierDefinition, TechnicalAttributeModifierProgressData>(definition)
    {
        /// <summary>Number of translocators repaired.</summary>
        public int TranslocatorsRepaired { get; set; } = 0;

        public override void ReadVersion(byte version, BinaryReader reader)
        {
            switch (version)
            {
                case 1:
                    TranslocatorsRepaired = reader.ReadInt32();
                    IsUnlocked = reader.ReadBoolean();
                    break;
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }

        public override void WriteOut(BinaryWriter writer)
        {
            writer.Write(TranslocatorsRepaired);
            writer.Write(IsUnlocked);
        }
    }
}
