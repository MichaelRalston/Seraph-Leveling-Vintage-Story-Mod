using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class AxeAttributeModifierProgressData(AxeAttributeModifierDefinition definition) : LeveledToolAttributeModifierProgressData<AxeAttributeModifierDefinition, AxeAttributeModifierProgressData, SimpleToolProgress>(definition)
    {
        
    }
    public class AxeAttributeModifierDefinition : LeveledToolAttributeModifierDefinition<AxeAttributeModifierDefinition, AxeAttributeModifierProgressData, SimpleToolProgress>, IConstructable<AxeAttributeModifierDefinition, AxeAttributeModifierProgressData>
    {
        public static AxeAttributeModifierProgressData Create(AxeAttributeModifierDefinition definition) { return new AxeAttributeModifierProgressData(definition); }
    }
}
