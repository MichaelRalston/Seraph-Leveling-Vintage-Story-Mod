using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class DamageAttributeModifierProgressData(DamageAttributeModifierDefinition definition) : LeveledToolAttributeModifierProgressData<DamageAttributeModifierDefinition, DamageAttributeModifierProgressData, float>(definition)
    {
        
    }
    public class DamageAttributeModifierDefinition : LeveledToolAttributeModifierDefinition<DamageAttributeModifierDefinition, DamageAttributeModifierProgressData, float>, IConstructable<DamageAttributeModifierDefinition, DamageAttributeModifierProgressData>
    {
        public static DamageAttributeModifierProgressData Create(DamageAttributeModifierDefinition definition) { return new DamageAttributeModifierProgressData(definition); }
    }
}
