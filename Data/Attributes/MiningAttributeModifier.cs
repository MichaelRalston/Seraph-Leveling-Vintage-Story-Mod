using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class MiningAttributeModifierProgressData(MiningAttributeModifierDefinition definition) : LeveledToolAttributeModifierProgressData<MiningAttributeModifierDefinition, MiningAttributeModifierProgressData, int>(definition)
    {
        public override void ReadVersion(byte version, BinaryReader reader)
        {
            int pickaxeCount;
            switch (version)
            {
                case 1:
                    long blocksMined = reader.ReadInt64();

                    // Convert old blocks to credits using legacy formula
                    int legacyLevel = 0;
                    if (blocksMined >= 100)
                    {
                        double discriminant = 1.0 + (8.0 * blocksMined / 100);
                        legacyLevel = (int)((-1.0 + Math.Sqrt(discriminant)) / 2.0);
                    }

                    TotalCredits = Math.Min(legacyLevel, Definition.GlobalMaxCredits);
                    break;
                case 2:
                    TotalCredits = reader.ReadInt32();
                    string currentPickaxeCode = reader.ReadString();
                    int partialCredit = reader.ReadInt32();
                    int currentIncrementSize = reader.ReadInt32();

                    // Migrate single pickaxe progress if it exists
                    if (!string.IsNullOrEmpty(currentPickaxeCode))
                    {
                        ToolProgress[currentPickaxeCode] = new LevelableTool<int>
                        {
                            PartialCredit = partialCredit,
                            CurrentIncrementSize = currentIncrementSize
                        };
                    }
                    break;
                case 3:
                    TotalCredits = reader.ReadInt32();

                    pickaxeCount = reader.ReadInt32();
                    for (int j = 0; j < pickaxeCount; j++)
                    {
                        string pickaxeCode = reader.ReadString();
                        var pickaxeProgress = new LevelableTool<int>
                        {
                            PartialCredit = reader.ReadInt32(),
                            CurrentIncrementSize = reader.ReadInt32()
                        };
                        ToolProgress[pickaxeCode] = pickaxeProgress;
                    }
                    break;
                case 4:
                    TotalCredits = reader.ReadInt32();
                    LastActivityDay = reader.ReadDouble();

                    pickaxeCount = reader.ReadInt32();
                    for (int j = 0; j < pickaxeCount; j++)
                    {
                        string pickaxeCode = reader.ReadString();
                        var pickaxeProgress = new LevelableTool<int>
                        {
                            PartialCredit = reader.ReadInt32(),
                            CurrentIncrementSize = reader.ReadInt32()
                        };
                        ToolProgress[pickaxeCode] = pickaxeProgress;
                    }
                    break;
                default:
                    throw new NotSupportedException($"Version {version} is not supported");
            }
        }
    }
    public class MiningAttributeModifierDefinition : LeveledToolAttributeModifierDefinition<MiningAttributeModifierDefinition, MiningAttributeModifierProgressData, int>, IConstructable<MiningAttributeModifierDefinition, MiningAttributeModifierProgressData>
    {
        public static MiningAttributeModifierProgressData Create(MiningAttributeModifierDefinition definition) { return new MiningAttributeModifierProgressData(definition); }
    }
}
