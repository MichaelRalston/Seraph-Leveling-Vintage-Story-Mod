using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using SeraphLeveling.Data.Tools;
using SeraphLeveling.Util;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class GenericToolAttributeModifierProgressData(GenericToolAttributeModifierDefinition definition) : LeveledToolAttributeModifierProgressData<GenericToolAttributeModifierDefinition, GenericToolAttributeModifierProgressData, SimpleToolProgress>(definition)
    {
        
    }
    
    public class GenericToolAttributeModifierDefinition : LeveledToolAttributeModifierDefinition<GenericToolAttributeModifierDefinition, GenericToolAttributeModifierProgressData, SimpleToolProgress>, 
        IConstructable<GenericToolAttributeModifierDefinition, GenericToolAttributeModifierProgressData>,
        IHasBlockBrokenTrigger<GenericToolAttributeModifierDefinition, GenericToolAttributeModifierProgressData, SimpleToolProgress>,
        IHasDamageDealtTrigger<GenericToolAttributeModifierDefinition, GenericToolAttributeModifierProgressData, SimpleToolProgress>
    {
        public GenericToolAttributeModifierDefinition()
        {
            SeraphLevelingModSystem.BlockBrokenTrigger += ((IHasBlockBrokenTrigger<GenericToolAttributeModifierDefinition, GenericToolAttributeModifierProgressData, SimpleToolProgress>)this).OnTriggerBlockBroken;
            SeraphLevelingModSystem.DamageDealtTrigger += ((IHasDamageDealtTrigger<GenericToolAttributeModifierDefinition, GenericToolAttributeModifierProgressData, SimpleToolProgress>)this).OnTriggerDamageDealt;
        }

        ~GenericToolAttributeModifierDefinition()
        {
            SeraphLevelingModSystem.BlockBrokenTrigger -= ((IHasBlockBrokenTrigger<GenericToolAttributeModifierDefinition, GenericToolAttributeModifierProgressData, SimpleToolProgress>)this).OnTriggerBlockBroken;
            SeraphLevelingModSystem.DamageDealtTrigger -= ((IHasDamageDealtTrigger<GenericToolAttributeModifierDefinition, GenericToolAttributeModifierProgressData, SimpleToolProgress>)this).OnTriggerDamageDealt;
        }

        public virtual ConcurrentDictionary<IAssetLocationMatcher, float> BrokenBlockScores { get; init; } = [];
        public virtual List<ToolDefinition> Weapons { get; init; } = [];

        public static GenericToolAttributeModifierProgressData Create(GenericToolAttributeModifierDefinition definition) { return new GenericToolAttributeModifierProgressData(definition); }
    }
}
