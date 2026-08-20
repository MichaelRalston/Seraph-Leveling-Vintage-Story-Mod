using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class PoulticeAttributeModifierDefinition : LeveledToolAttributeModifierDefinition<PoulticeAttributeModifierDefinition, PoulticeAttributeModifierProgressData, SimpleToolProgress>, IConstructable<PoulticeAttributeModifierDefinition, PoulticeAttributeModifierProgressData>
    {
        public static PoulticeAttributeModifierProgressData Create(PoulticeAttributeModifierDefinition definition) { return new PoulticeAttributeModifierProgressData(definition); }
    }

    public class PoulticeAttributeModifierProgressData(PoulticeAttributeModifierDefinition definition) : LeveledToolAttributeModifierProgressData<PoulticeAttributeModifierDefinition, PoulticeAttributeModifierProgressData, SimpleToolProgress>(definition)
    {
        
    }
}
