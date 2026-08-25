using System;
using System.Collections.Generic;
using System.IO;
using SeraphLeveling.Data.Tools;
using SeraphLeveling.Patches;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public enum RepairableToolProgress { Usage, Repair };

    public class GenericRepairableToolAttributeModifierProgressData(GenericRepairableToolAttributeModifierDefinition definition) : LeveledToolAttributeModifierProgressData<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData, RepairableToolProgress>(definition)
    {
        
    }

    public class GenericRepairableToolAttributeModifierDefinition : 
        LeveledToolAttributeModifierDefinition<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData, RepairableToolProgress>, 
        IConstructable<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData>, 
        IHasToolRepairTrigger<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData>,
        IHasToolDamagedTrigger<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData>,
        IHasDamageDealtTrigger<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData, RepairableToolProgress>
    {
        public GenericRepairableToolAttributeModifierDefinition()
        {
            CraftingPatches.TriggerToolRepair += ((IHasToolRepairTrigger<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData>)this).OnTriggerToolRepair;
            ItemDamagePatches.TriggerToolDamaged += ((IHasToolDamagedTrigger<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData>)this).OnTriggerToolDamaged;
            SeraphLevelingModSystem.DamageDealtTrigger += ((IHasDamageDealtTrigger<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData, RepairableToolProgress>)this).OnTriggerDamageDealt;
        }

        ~GenericRepairableToolAttributeModifierDefinition()
        {
            CraftingPatches.TriggerToolRepair -= ((IHasToolRepairTrigger<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData>)this).OnTriggerToolRepair;
            ItemDamagePatches.TriggerToolDamaged -= ((IHasToolDamagedTrigger<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData>)this).OnTriggerToolDamaged;
            SeraphLevelingModSystem.DamageDealtTrigger -= ((IHasDamageDealtTrigger<GenericRepairableToolAttributeModifierDefinition, GenericRepairableToolAttributeModifierProgressData, RepairableToolProgress>)this).OnTriggerDamageDealt;
        }

        public virtual List<ToolDefinition> Weapons { get; init; } = [];

        public static GenericRepairableToolAttributeModifierProgressData Create(GenericRepairableToolAttributeModifierDefinition definition) { return new GenericRepairableToolAttributeModifierProgressData(definition); }
    }
}
