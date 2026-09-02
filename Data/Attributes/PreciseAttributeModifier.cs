using System;
using System.Collections.Generic;
using System.IO;
using SeraphLeveling.Data.Tools;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public class PreciseAttributeModifierProgressData(PreciseAttributeModifierDefinition definition) : LeveledToolAttributeModifierProgressData<PreciseAttributeModifierDefinition, PreciseAttributeModifierProgressData, SimpleToolProgress>(definition)
    {
        
    }

    public class PreciseAttributeModifierDefinition : LeveledToolAttributeModifierDefinition<PreciseAttributeModifierDefinition, PreciseAttributeModifierProgressData, SimpleToolProgress>, 
        IConstructable<PreciseAttributeModifierDefinition, PreciseAttributeModifierProgressData>,
    {
        public required List<ToolDefinition> Weapons { get; init; }

        public static PreciseAttributeModifierProgressData Create(PreciseAttributeModifierDefinition definition) { return new PreciseAttributeModifierProgressData(definition); }
    }
}
