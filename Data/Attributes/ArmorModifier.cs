using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using System.Text;
using System.Collections.Generic;

namespace SeraphLeveling.Data.Attributes
{
    public enum ArmorDurabilityProgressTypes
    {
        DamageBlocked,
        RepairProgress,
    };

    public class ArmorDurabilityModifierDefinition : ArmorModifierDefinition<ArmorDurabilityProgressTypes>
    {
        public override void ReadConfigData(Dictionary<string, int> dict)
        {
            if (dict.TryGetValue("maxCredits", out var max)) GlobalMaxCredits = max;
            if (dict.TryGetValue("damageBlockedIncrement", out var baseInc))
            {
                var id = IncrementData[ArmorDurabilityProgressTypes.DamageBlocked];
                id.BaseIncrement = baseInc;
            }
            if (dict.TryGetValue("damageBlockedStep", out var step))
            {
                var id = IncrementData[ArmorDurabilityProgressTypes.DamageBlocked];
                id.IncrementStep = step;
            }
            if (dict.TryGetValue("repairIncrement", out var baseRepair))
            {
                var id = IncrementData[ArmorDurabilityProgressTypes.DamageBlocked];
                id.BaseIncrement = baseRepair;
            }
            if (dict.TryGetValue("repairStep", out var repairStep))
            {
                var id = IncrementData[ArmorDurabilityProgressTypes.DamageBlocked];
                id.IncrementStep = repairStep;
            }

        }
        public override Dictionary<string, int> GetConfigData()
        {
            return new() { ["maxCredits"] = GlobalMaxCredits, ["baseIncrement"] = BaseIncrement, ["step"] = IncrementStep };
        }

    };

    public class SimpleArmorModifierDefinition : ArmorModifierDefinition<SimpleToolProgress> { };
    public class ArmorModifierProgressData<E>(ArmorModifierDefinition<E> definition) : LeveledToolAttributeModifierProgressData<ArmorModifierDefinition<E>, ArmorModifierProgressData<E>, E>(definition) where E : Enum { };
    public class ArmorModifierDefinition<E> : LeveledToolAttributeModifierDefinition<ArmorModifierDefinition<E>, ArmorModifierProgressData<E>, E>, IConstructable<ArmorModifierDefinition<E>, ArmorModifierProgressData<E>> where E : Enum
    {
        public override void ReadConfigData(Dictionary<string, int> dict)
        {
            if (dict.TryGetValue("maxCredits", out var max)) GlobalMaxCredits = max;
            if (dict.TryGetValue("baseIncrement", out var baseInc)) BaseIncrement = baseInc;
            if (dict.TryGetValue("step", out var step)) IncrementStep = step;

        }
        public override Dictionary<string, int> GetConfigData()
        {
            return new() { ["maxCredits"] = GlobalMaxCredits, ["baseIncrement"] = BaseIncrement, ["step"] = IncrementStep };
        }
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
