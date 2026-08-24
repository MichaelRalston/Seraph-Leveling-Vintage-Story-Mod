using System;
using System.Collections.Generic;
using System.IO;
using SeraphLeveling.Patches;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{

    public enum RepairableToolProgress { Usage, Repair };
    public class GenericRepairableToolAttributeModifierProgressData(GenericRepairableToolAttributeModifierDefinition definition) : LeveledToolAttributeModifierProgressData<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData, RepairableToolProgress>(definition)
    {
        
    }
    public class GenericRepairableToolAttributeModifierDefinition : LeveledToolAttributeModifierDefinition<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData, RepairableToolProgress>, IConstructable<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData>, IHasToolRepairTrigger<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData>
    {
        public GenericRepairableToolAttributeModifierDefinition()
        {
            CraftingPatches.TriggerToolRepair += ((IHasToolRepairTrigger<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData>)this).OnTriggerToolRepair;
        }

        ~GenericRepairableToolAttributeModifierDefinition()
        {
            CraftingPatches.TriggerToolRepair -= ((IHasToolRepairTrigger<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData>)this).OnTriggerToolRepair;
        }

        public static GenericRepairableToolAttributeModifierProgressData Create(GenericRepairableToolAttributeModifierDefinition definition) { return new GenericRepairableToolAttributeModifierProgressData(definition); }
    }
}
