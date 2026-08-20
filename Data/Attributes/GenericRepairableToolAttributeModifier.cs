using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{

    public enum RepairableToolProgress { Usage, Repair };
    public class GenericRepairableToolAttributeModifierProgressData(GenericRepairableToolAttributeModifierDefinition definition) : LeveledToolAttributeModifierProgressData<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData, RepairableToolProgress>(definition)
    {
        
    }
    public class GenericRepairableToolAttributeModifierDefinition : LeveledToolAttributeModifierDefinition<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData, RepairableToolProgress>, IConstructable<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData>
    {
        public static GenericRepairableToolAttributeModifierProgressData Create(GenericRepairableToolAttributeModifierDefinition definition) { return new GenericRepairableToolAttributeModifierProgressData(definition); }
    }
}
