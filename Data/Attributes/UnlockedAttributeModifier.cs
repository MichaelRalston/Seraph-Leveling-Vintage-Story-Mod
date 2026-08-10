using System;
using System.IO;
using System.Text;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public record class UnlockedAttributeModifierDefinition : AttributeModifierDefinition<UnlockedAttributeModifierDefinition, UnlockedAttributeModifierProgressData>, IConstructable<UnlockedAttributeModifierDefinition, UnlockedAttributeModifierProgressData>
    {
        public required string Name { get; init; }
        public required string ExtraTraitKey { get; init; }

        public static UnlockedAttributeModifierProgressData Create(UnlockedAttributeModifierDefinition definition)
        {
            return new UnlockedAttributeModifierProgressData(definition);
        }

        public void GetTraitAllCommandLine(IPlayer player, StringBuilder sb) {
            var progress = GetDict(player);
            sb.AppendLine($"{Name}: {(progress.IsUnlocked ? "UNLOCKED" : "locked")}");
        }
    }

    public class UnlockedAttributeModifierProgressData(UnlockedAttributeModifierDefinition definition) : AttributeModifierProgressData<UnlockedAttributeModifierDefinition, UnlockedAttributeModifierProgressData>(definition)
    {
        /// <summary>Whether the trait has been unlocked.</summary>
        public bool IsUnlocked { get; set; } = false;

        public override void ReadVersion(byte version, BinaryReader reader)
        {
            switch (version)
            {
                case 1:
                    IsUnlocked = reader.ReadBoolean();
                    break;
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }

        public override void WriteOut(BinaryWriter writer)
        {
            writer.Write(IsUnlocked);
        }
    }
}
