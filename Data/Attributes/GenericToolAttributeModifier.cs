using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class GenericToolAttributeModifierProgressData(GenericToolAttributeModifierDefinition definition) : LeveledToolAttributeModifierProgressData<GenericToolAttributeModifierDefinition, GenericToolAttributeModifierProgressData, SimpleToolProgress>(definition)
    {
        
    }
    public class GenericToolAttributeModifierDefinition : LeveledToolAttributeModifierDefinition<GenericToolAttributeModifierDefinition, GenericToolAttributeModifierProgressData, SimpleToolProgress>, IConstructable<GenericToolAttributeModifierDefinition, GenericToolAttributeModifierProgressData>
    {
        public static GenericToolAttributeModifierProgressData Create(GenericToolAttributeModifierDefinition definition) { return new GenericToolAttributeModifierProgressData(definition); }
    }
}
