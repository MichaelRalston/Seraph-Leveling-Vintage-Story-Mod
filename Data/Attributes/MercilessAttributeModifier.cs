using System.Text;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using SeraphLeveling.Data.Legacy;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public class MercilessAttributeModifierDefinition : UnlockedAttributeModifierDefinition<MercilessAttributeModifierDefinition, MercilessAttributeModifierProgressData>, IConstructable<MercilessAttributeModifierDefinition, MercilessAttributeModifierProgressData>
    {
        public static MercilessAttributeModifierProgressData Create(MercilessAttributeModifierDefinition def)
        {
            return new MercilessAttributeModifierProgressData(def);
        }
    }

    public class MercilessAttributeModifierProgressData(MercilessAttributeModifierDefinition definition) : UnlockedAttributeModifierProgressData<MercilessAttributeModifierDefinition, MercilessAttributeModifierProgressData>(definition)
    {
        
    }
}
