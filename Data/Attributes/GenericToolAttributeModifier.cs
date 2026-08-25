using System;
using System.Collections.Concurrent;
using System.IO;
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
        IHasBlockBrokenTrigger<GenericToolAttributeModifierDefinition, GenericToolAttributeModifierProgressData, SimpleToolProgress>
    {
        public GenericToolAttributeModifierDefinition()
        {
            SeraphLevelingModSystem.BlockBrokenTrigger += ((IHasBlockBrokenTrigger<GenericToolAttributeModifierDefinition, GenericToolAttributeModifierProgressData, SimpleToolProgress>)this).OnTriggerBlockBroken;
        }

        ~GenericToolAttributeModifierDefinition()
        {
            SeraphLevelingModSystem.BlockBrokenTrigger -= ((IHasBlockBrokenTrigger<GenericToolAttributeModifierDefinition, GenericToolAttributeModifierProgressData, SimpleToolProgress>)this).OnTriggerBlockBroken;
        }

        public virtual ConcurrentDictionary<IAssetLocationMatcher, float> BrokenBlockScores { get; init; } = [];

        public static GenericToolAttributeModifierProgressData Create(GenericToolAttributeModifierDefinition definition) { return new GenericToolAttributeModifierProgressData(definition); }
    }
}
