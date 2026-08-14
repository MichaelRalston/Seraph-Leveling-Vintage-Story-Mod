using System.Text;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using SeraphLeveling.Data.Legacy;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public class TinkererAttributeModifierDefinition : UnlockedAttributeModifierDefinition<TinkererAttributeModifierDefinition, TinkererAttributeModifierProgressData>, IConstructable<TinkererAttributeModifierDefinition, TinkererAttributeModifierProgressData>
    {
        public static TinkererAttributeModifierProgressData Create(TinkererAttributeModifierDefinition def)
        {
            return new TinkererAttributeModifierProgressData(def);
        }
    }

    public class TinkererAttributeModifierProgressData(TinkererAttributeModifierDefinition definition) : UnlockedAttributeModifierProgressData<TinkererAttributeModifierDefinition, TinkererAttributeModifierProgressData>(definition)
    {
        
    }
}
