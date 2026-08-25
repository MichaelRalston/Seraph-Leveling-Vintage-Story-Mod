using System;
using System.Collections.Generic;
using System.IO;
using SeraphLeveling.Data.Tools;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class DamageAttributeModifierProgressData(DamageAttributeModifierDefinition definition) : LeveledToolAttributeModifierProgressData<DamageAttributeModifierDefinition, DamageAttributeModifierProgressData, SimpleToolProgress>(definition)
    {
        
    }

    public class DamageAttributeModifierDefinition : LeveledToolAttributeModifierDefinition<DamageAttributeModifierDefinition, DamageAttributeModifierProgressData, SimpleToolProgress>, 
        IConstructable<DamageAttributeModifierDefinition, DamageAttributeModifierProgressData>,
        IHasDamageDealtTrigger<DamageAttributeModifierDefinition, DamageAttributeModifierProgressData, SimpleToolProgress>
    {
        public DamageAttributeModifierDefinition()
        {
            SeraphLevelingModSystem.DamageDealtTrigger += ((IHasDamageDealtTrigger<DamageAttributeModifierDefinition, DamageAttributeModifierProgressData, SimpleToolProgress>)this).OnTriggerDamageDealt;
        }

        ~DamageAttributeModifierDefinition()
        {
            SeraphLevelingModSystem.DamageDealtTrigger -= ((IHasDamageDealtTrigger<DamageAttributeModifierDefinition, DamageAttributeModifierProgressData, SimpleToolProgress>)this).OnTriggerDamageDealt;
        }

        public required List<ToolDefinition> Weapons { get; init; }

        public static DamageAttributeModifierProgressData Create(DamageAttributeModifierDefinition definition) { return new DamageAttributeModifierProgressData(definition); }
    }
}
