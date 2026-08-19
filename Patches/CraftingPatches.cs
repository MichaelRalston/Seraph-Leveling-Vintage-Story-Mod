using System;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SeraphLeveling.Patches
{
    public static class CraftingPatches
    {
        public static void GridRecipeConsumeInput_Postfix(GridRecipe __instance, IPlayer byPlayer, ItemSlot[] inputSlots, int gridWidth, bool __result)
        {
            if (__result && byPlayer?.Entity?.Api?.Side == EnumAppSide.Server)
            {
                if (byPlayer is not IServerPlayer serverPlayer) return;

                string outputCode = __instance?.Output?.Code?.ToString();
                string playerName = byPlayer?.PlayerName;
                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[Verdus] Player {playerName} crafted item code {outputCode}, success={__result}, side={byPlayer?.Entity?.Api?.Side}");

                if (outputCode.Contains("plank-", StringComparison.OrdinalIgnoreCase))
                {
                    float quantity = __instance?.Output?.Quantity ?? 0;
                    SeraphLevelingModSystem.ServerApi.Logger.Debug($"[Verdus] Granting {playerName} credit for crafting {quantity} boards");
                    AttributeModifierDefinitions.Carpenter.AddCredits(serverPlayer, quantity);
                }
            }
        }
    }
}
