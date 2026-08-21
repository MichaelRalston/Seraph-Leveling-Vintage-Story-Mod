using System.Text;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public class DetonatorAttributeModifierDefinition : ScoredUnlockedAttributeModifierDefinition<DetonatorAttributeModifierDefinition, DetonatorAttributeModifierProgressData>, IConstructable<DetonatorAttributeModifierDefinition, DetonatorAttributeModifierProgressData>
    {
        public static DetonatorAttributeModifierProgressData Create(DetonatorAttributeModifierDefinition def)
        {
            return new DetonatorAttributeModifierProgressData(def);
        }
    }
    
    public class DetonatorAttributeModifierProgressData(DetonatorAttributeModifierDefinition definition) : ScoredUnlockedAttributeModifierProgressData<DetonatorAttributeModifierDefinition, DetonatorAttributeModifierProgressData>(definition)
    {
    }
}
