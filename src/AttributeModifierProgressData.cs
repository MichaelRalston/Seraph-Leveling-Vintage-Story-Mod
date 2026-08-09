using System;
using System.IO;
using System.Numerics;

namespace SeraphLeveling
{
    public abstract class AAttributeModifierProgressData
    {
        protected AttributeModifierDefinition Definition { get; init; }
        protected byte Version { get; init; }

        public AAttributeModifierProgressData(AttributeModifierDefinition definition, byte version)
        {
            Definition = definition;
            Version = version;
        }

        public abstract void ReadVersion(byte version, BinaryReader reader);
    }

    public class LeveledAttributeModifierProgressData<V>(AttributeModifierDefinition definition, byte version) : AAttributeModifierProgressData(definition, version) where V : INumber<V>
    {
        /// <summary>Total credits earned (each credit = 1% bonus).</summary>
        public int TotalCredits { get; set; }
        /// <summary>Last in-game day when this skill was used. Used for skill decay.</summary>
        public double LastActivityDay { get; set; }
        /// <summary>Action taken toward the next credit.</summary>
        public V PartialCredit { get; set; } = V.Zero; // formerly known as BlocksInIncrement
        /// <summary>Actions needed for the next credit (1000, 2000, 3000, etc.).</summary>
        public int CurrentIncrementSize { get; set; }

        public override void ReadVersion(byte version, BinaryReader reader)
        {
            switch (version) {
                case 1:
                    TotalCredits = reader.ReadInt32();
                    PartialCredit = (typeof(V) == typeof(int)?V.CreateTruncating(reader.ReadInt32()):(typeof(V) == typeof(float)?V.CreateTruncating(reader.ReadSingle()):throw new NotSupportedException($"Binary reading for type {typeof(V).Name} is not supported.")));
                    CurrentIncrementSize = reader.ReadInt32();
                    break;
                case 2:
                    TotalCredits = reader.ReadInt32();
                    PartialCredit = (typeof(V) == typeof(int)?V.CreateTruncating(reader.ReadInt32()):(typeof(V) == typeof(float)?V.CreateTruncating(reader.ReadSingle()):throw new NotSupportedException($"Binary reading for type {typeof(V).Name} is not supported.")));
                    CurrentIncrementSize = reader.ReadInt32();
                    LastActivityDay = reader.ReadDouble();
                    break;
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }
    }
}
