using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class DamageAttributeModifierProgressData(DamageAttributeModifierDefinition definition) : LeveledToolAttributeModifierProgressData<DamageAttributeModifierDefinition, DamageAttributeModifierProgressData, SimpleToolProgress>(definition)
    {
        
    }
    public class DamageAttributeModifierDefinition : LeveledToolAttributeModifierDefinition<DamageAttributeModifierDefinition, DamageAttributeModifierProgressData, SimpleToolProgress>, IConstructable<DamageAttributeModifierDefinition, DamageAttributeModifierProgressData>
    {
        public static DamageAttributeModifierProgressData Create(DamageAttributeModifierDefinition definition) { return new DamageAttributeModifierProgressData(definition); }
    }
}
