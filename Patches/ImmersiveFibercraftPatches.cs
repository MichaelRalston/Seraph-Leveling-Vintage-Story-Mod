using System;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using SeraphLeveling.Data.Mods;
using HarmonyLib;
using System.Reflection;

namespace SeraphLeveling.Patches
{
    public static class ImmersiveFibercraftPatches
    {

        private static FieldInfo mountedByField = null;
        private static FieldInfo outputSlotField = null;
        private static FieldInfo apiField = null;
        private static MethodInfo firstInputSlotMethod = null;
        private static MethodInfo getWeaveResultMethod = null;
        private static MethodInfo getMatchingPatternRecipeMethod = null;
        private static MethodInfo recipeGetOutputMethod = null;

        private static void PopulateFields(object __instance)
        {
            if (mountedByField == null)
            {
                mountedByField = AccessTools.Field(__instance.GetType(), "MountedBy");
            }
            if (outputSlotField == null)
            {
                outputSlotField = AccessTools.Field(__instance.GetType(), "OutputSlot");
            }
            if (apiField == null)
            {
                apiField = AccessTools.Field(__instance.GetType(), "Api");
            }
            if (firstInputSlotMethod == null)
            {
                firstInputSlotMethod = AccessTools.Method(__instance.GetType(), "GetFirstInputSlotWithItem");
            }
            if (getWeaveResultMethod == null)
            {
                getWeaveResultMethod = AccessTools.Method(__instance.GetType(), "GetWeaveResult");
            }
            if (getMatchingPatternRecipeMethod == null)
            {
                getMatchingPatternRecipeMethod = AccessTools.Method(__instance.GetType(), "GetMatchingPatternRecipe");
            }
        }
        private static void PopulateRecipeFields(object recipe)
        {
            if (recipeGetOutputMethod == null)
            {
                recipeGetOutputMethod = AccessTools.Method(recipe.GetType(), "GetOutput");
            }
        }
        public static void WeaveInputNormal_Prefix(object __instance)
        {
            if (!ModDefinitions.ImmersiveFibercraft.IsActive) return;
            PopulateFields(__instance);

            if (firstInputSlotMethod.Invoke(__instance, null) is not ItemSlot firstInputSlot) return;
            if (getWeaveResultMethod.Invoke(__instance, [firstInputSlot.Itemstack]) is not ItemStack resultStack) return;
            if (mountedByField.GetValue(__instance) is not EntityAgent entityAgent) return;
            if (entityAgent is not EntityPlayer entityPlayer || entityPlayer.Player is not IServerPlayer player) return;

            CraftingPatches.TriggerGridCraftingResult(player, resultStack.Collectible.Code, resultStack.StackSize);
        }

        public static void WeaveInputPattern_Prefix(object __instance)
        {
            if (!ModDefinitions.ImmersiveFibercraft.IsActive) return;

            PopulateFields(__instance);

            if (getMatchingPatternRecipeMethod.Invoke(__instance, null) is not object recipe) return;
            PopulateRecipeFields(recipe);

            if (recipeGetOutputMethod.Invoke(recipe, [apiField.GetValue(__instance)]) is not ItemStack resultStack) return;
            if (mountedByField.GetValue(__instance) is not EntityAgent entityAgent) return;
            if (entityAgent is not EntityPlayer entityPlayer || entityPlayer.Player is not IServerPlayer player) return;

            CraftingPatches.TriggerGridCraftingResult(player, resultStack.Collectible.Code, resultStack.StackSize);
        }
    }
}