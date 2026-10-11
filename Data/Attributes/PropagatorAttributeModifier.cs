using System.Text;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public class PropagatorAttributeModifierDefinition : ScoredUnlockedAttributeModifierDefinition<PropagatorAttributeModifierDefinition, PropagatorAttributeModifierProgressData>, IConstructable<PropagatorAttributeModifierDefinition, PropagatorAttributeModifierProgressData>
    {
        public static PropagatorAttributeModifierProgressData Create(PropagatorAttributeModifierDefinition def)
        {
            return new PropagatorAttributeModifierProgressData(def);
        }
    }

    public class PropagatorAttributeModifierProgressData(PropagatorAttributeModifierDefinition definition) : ScoredUnlockedAttributeModifierProgressData<PropagatorAttributeModifierDefinition, PropagatorAttributeModifierProgressData>(definition)
    {
    }
}
