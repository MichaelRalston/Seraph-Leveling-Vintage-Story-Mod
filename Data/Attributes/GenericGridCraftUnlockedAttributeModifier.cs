using System.Text;
using SeraphLeveling.Patches;
using SeraphLeveling.Util;
using Vintagestory.API.Common;

namespace SeraphLeveling.Data.Attributes
{
    public class GenericGridCraftUnlockedAttributeModifierDefinition : ScoredUnlockedAttributeModifierDefinition<GenericGridCraftUnlockedAttributeModifierDefinition, GenericGridCraftUnlockedAttributeModifierProgressData>, IConstructable<GenericGridCraftUnlockedAttributeModifierDefinition, GenericGridCraftUnlockedAttributeModifierProgressData>, IHasGridCraftingResultTrigger
    {
        public required string CraftedItemName { get; init; }
        public required IAssetLocationMatcher ResultAllowList { get; init; }
        public virtual IAssetLocationMatcher ResultBanList { get; init; } = null;

        public GenericGridCraftUnlockedAttributeModifierDefinition()
        {
            CraftingPatches.TriggerGridCrafted += ((IHasGridCraftingResultTrigger)this).OnTriggerGridCraftingResult;
        }

        ~GenericGridCraftUnlockedAttributeModifierDefinition()
        {
            CraftingPatches.TriggerGridCrafted -= ((IHasGridCraftingResultTrigger)this).OnTriggerGridCraftingResult;
        }

        public static GenericGridCraftUnlockedAttributeModifierProgressData Create(GenericGridCraftUnlockedAttributeModifierDefinition def)
        {
            return new GenericGridCraftUnlockedAttributeModifierProgressData(def);
        }
    }

    public class GenericGridCraftUnlockedAttributeModifierProgressData(GenericGridCraftUnlockedAttributeModifierDefinition definition) : ScoredUnlockedAttributeModifierProgressData<GenericGridCraftUnlockedAttributeModifierDefinition, GenericGridCraftUnlockedAttributeModifierProgressData>(definition)
    {
    }
}
