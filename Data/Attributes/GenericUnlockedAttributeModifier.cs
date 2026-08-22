using System.Text;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using SeraphLeveling.Data.Legacy;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public class GenericUnlockedAttributeModifierDefinition : UnlockedAttributeModifierDefinition<GenericUnlockedAttributeModifierDefinition, GenericUnlockedAttributeModifierProgressData>, IConstructable<GenericUnlockedAttributeModifierDefinition, GenericUnlockedAttributeModifierProgressData>
    {
        public static GenericUnlockedAttributeModifierProgressData Create(GenericUnlockedAttributeModifierDefinition def)
        {
            return new GenericUnlockedAttributeModifierProgressData(def);
        }
    }

    public class GenericUnlockedAttributeModifierProgressData(GenericUnlockedAttributeModifierDefinition definition) : UnlockedAttributeModifierProgressData<GenericUnlockedAttributeModifierDefinition, GenericUnlockedAttributeModifierProgressData>(definition)
    {
        
    }
}
