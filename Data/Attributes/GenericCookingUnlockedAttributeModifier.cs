using System.Text;
using SeraphLeveling.Patches;
using SeraphLeveling.Util;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public class GenericCookingUnlockedAttributeModifierDefinition : ScoredUnlockedAttributeModifierDefinition<GenericCookingUnlockedAttributeModifierDefinition, GenericCookingUnlockedAttributeModifierProgressData>, IConstructable<GenericCookingUnlockedAttributeModifierDefinition, GenericCookingUnlockedAttributeModifierProgressData>, IHasCookingResultTrigger
    {
        public required string CookedItemName { get; init; }
        public required IAssetLocationMatcher ResultAllowList { get; init; }
        public virtual IAssetLocationMatcher ResultBanList { get; init; } = null;

        public GenericCookingUnlockedAttributeModifierDefinition()
        {
            CookingPatches.TriggerCookingResult += ((IHasCookingResultTrigger)this).OnTriggerCookingResult;
        }

        ~GenericCookingUnlockedAttributeModifierDefinition()
        {
            CookingPatches.TriggerCookingResult -= ((IHasCookingResultTrigger)this).OnTriggerCookingResult;
        }

        public static GenericCookingUnlockedAttributeModifierProgressData Create(GenericCookingUnlockedAttributeModifierDefinition def)
        {
            return new GenericCookingUnlockedAttributeModifierProgressData(def);
        }
    }

    public class GenericCookingUnlockedAttributeModifierProgressData(GenericCookingUnlockedAttributeModifierDefinition definition) : ScoredUnlockedAttributeModifierProgressData<GenericCookingUnlockedAttributeModifierDefinition, GenericCookingUnlockedAttributeModifierProgressData>(definition)
    {
    }
}
