using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using System.Text;

namespace SeraphLeveling.Data.Attributes
{
    public class ArmorModifierProgressData<E>(ArmorModifierDefinition<E> definition) : LeveledToolAttributeModifierProgressData<ArmorModifierDefinition<E>, ArmorModifierProgressData<E>, E>(definition) where E: Enum
    {
    };
    public class ArmorModifierDefinition<E> : LeveledToolAttributeModifierDefinition<ArmorModifierDefinition<E>, ArmorModifierProgressData<E>, E>, IConstructable<ArmorModifierDefinition<E>, ArmorModifierProgressData<E>> where E:Enum
    {
        public static ArmorModifierProgressData<E> Create(ArmorModifierDefinition<E> definition) { return new ArmorModifierProgressData<E>(definition); }
        public override int ApplyDecay(IServerPlayer player, double currentDay, StringBuilder sb, StringBuilder verboseSb)
        {
            return 0;
        }
        public override int ApplyDeathPenalty(IServerPlayer player, StringBuilder sb)
        {
            return 0;
        }
    };
}
