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
                int quantity = __instance?.Output?.Quantity ?? 0;

                if (quantity <= 0) return;

                string playerName = byPlayer?.PlayerName;
#if SPAMMYDEBUG
                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Player {playerName} crafted item code {outputCode}, success={__result}, side={byPlayer?.Entity?.Api?.Side}");
#endif

                // Process boards
                if (outputCode.StartsWith("plank-"))
                {
                    SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Granting {playerName} credit for crafting {quantity} boards");
                    AttributeModifierDefinitions.Carpenter.AddCredits(serverPlayer, quantity);
                }

                // Process ashlar blocks
                if (outputCode.StartsWith("stonebrick-"))
                {
                    SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Granting {playerName} credit for crafting {quantity} ashlar blocks");
                    AttributeModifierDefinitions.Mason.AddCredits(serverPlayer, quantity);
                }
            }
        }
    }
}
