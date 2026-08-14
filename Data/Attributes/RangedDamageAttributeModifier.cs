using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class RangedDamageAttributeModifierProgressData(RangedDamageAttributeModifierDefinition definition) : LeveledToolAttributeModifierProgressData<RangedDamageAttributeModifierDefinition, RangedDamageAttributeModifierProgressData, float>(definition)
    {
        
    }
    public class RangedDamageAttributeModifierDefinition : LeveledToolAttributeModifierDefinition<RangedDamageAttributeModifierDefinition, RangedDamageAttributeModifierProgressData, float>, IConstructable<RangedDamageAttributeModifierDefinition, RangedDamageAttributeModifierProgressData>
    {
        public static RangedDamageAttributeModifierProgressData Create(RangedDamageAttributeModifierDefinition definition) { return new RangedDamageAttributeModifierProgressData(definition); }
    }
}
