using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class ShovelAttributeModifierDefinition : LeveledToolAttributeModifierDefinition<ShovelAttributeModifierDefinition, ShovelAttributeModifierProgressData, SimpleToolProgress>, IConstructable<ShovelAttributeModifierDefinition, ShovelAttributeModifierProgressData>
    {
        public static ShovelAttributeModifierProgressData Create(ShovelAttributeModifierDefinition definition) { return new ShovelAttributeModifierProgressData(definition); }
    }

    public class ShovelAttributeModifierProgressData(ShovelAttributeModifierDefinition definition) : LeveledToolAttributeModifierProgressData<ShovelAttributeModifierDefinition, ShovelAttributeModifierProgressData, SimpleToolProgress>(definition)
    {
        
    }
}
