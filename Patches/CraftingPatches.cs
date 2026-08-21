using System;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

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
                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Player {playerName} crafted item code {outputCode} in grid, success={__result}, side={byPlayer?.Entity?.Api?.Side}");
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

                // Process large gears
                if (outputCode.Contains("largegear3"))
                {
                    SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Granting {playerName} credit for crafting {quantity} large gears");
                    AttributeModifierDefinitions.Technician.AddCredits(serverPlayer, quantity);
                }

                var toolType = __instance?.Output?.ResolvedItemStack?.Item?.Tool;
                if (toolType != null && byPlayer != null)
                {
                    string playerUid = byPlayer?.PlayerUID;
                    switch (toolType)
                    {
                        case EnumTool.Axe:
                            AttributeModifierDefinitions.AxeDurability.GetForPlayer(playerUid).DoEvent(serverPlayer, outputCode, quantity, RepairableToolProgress.Repair);
                            break;
                        case EnumTool.Bow:
                            AttributeModifierDefinitions.BowDurability.GetForPlayer(playerUid).DoEvent(serverPlayer, outputCode, quantity, RepairableToolProgress.Repair);
                            AttributeModifierDefinitions.BowDamage.GetForPlayer(playerUid).DoEvent(serverPlayer, outputCode, quantity, RepairableToolProgress.Repair);
                            break;
                        case EnumTool.Pickaxe:
                            AttributeModifierDefinitions.PickaxeDurability.GetForPlayer(playerUid).DoEvent(serverPlayer, outputCode, quantity, RepairableToolProgress.Repair);
                            break;
                        case EnumTool.Hoe:
                            AttributeModifierDefinitions.HoeDurability.GetForPlayer(playerUid).DoEvent(serverPlayer, outputCode, quantity, RepairableToolProgress.Repair);
                            break;
                        case EnumTool.Scythe:
                            AttributeModifierDefinitions.ScytheDurability.GetForPlayer(playerUid).DoEvent(serverPlayer, outputCode, quantity, RepairableToolProgress.Repair);
                            break;
                        case EnumTool.Hammer:
                            AttributeModifierDefinitions.HammerDurability.GetForPlayer(playerUid).DoEvent(serverPlayer, outputCode, quantity, RepairableToolProgress.Repair);
                            break;
                    }
                }
            }
        }

        public static void BlockEntityBarrelOnReceivedClientPacket_Postfix(BlockEntityBarrel __instance, IPlayer player, int packetid, byte[] data)
        {
            const int SEAL_BARREL_PACKET_ID = 1337;
            if (player?.Entity?.Api?.Side == EnumAppSide.Server && packetid == SEAL_BARREL_PACKET_ID)
            {
                if (player is not IServerPlayer serverPlayer) return;
                
                string outputCode = __instance?.CurrentRecipe?.Output?.Code?.ToString();
                int quantity = __instance?.CurrentRecipe?.Output?.Quantity ?? 0;

                if (quantity <= 0) return;

                string playerName = serverPlayer?.PlayerName;
#if SPAMMYDEBUG
                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Player {playerName} crafted {quantity} of item code {outputCode} in a barrel");
#endif

                // Process compost
                if (outputCode.Contains("compost"))
                {
                    SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Granting {playerName} credit for crafting {quantity} compost");
                    AttributeModifierDefinitions.Propagator.AddCredits(serverPlayer, quantity);
                }
            }
        }

        /// <summary>
        /// Stores the state of the clayform CheckIfFinished method.
        /// </summary>
        public class ClayFormCheckIfFinishedState
        {
            public ItemStack workItemStack;
            public ClayFormingRecipe recipe;
        }
        
        public static void BlockEntityClayForm_CheckIfFinished_Prefix(BlockEntityClayForm __instance, out ClayFormCheckIfFinishedState __state, IPlayer byPlayer, int layer, ItemStack ___workItemStack)
        {
            // Pass necessary state on to postfix method
            __state = new()
            {
                workItemStack = ___workItemStack,
                recipe = __instance.SelectedRecipe
            };
        }

        public static void BlockEntityClayForm_CheckIfFinished_Postfix(BlockEntityClayForm __instance, ClayFormCheckIfFinishedState __state, IPlayer byPlayer, int layer, ItemStack ___workItemStack)
        {
            if (byPlayer?.Entity?.Api?.Side == EnumAppSide.Server)
            {
                if (byPlayer is not IServerPlayer serverPlayer || ___workItemStack != null || __state.recipe == null) return;

                string playerName = serverPlayer?.PlayerName;
                string outputCode = __state.recipe.Output?.ResolvedItemstack?.Collectible?.Code?.ToString();
                int quantity = __state.recipe.Output?.ResolvedItemstack?.StackSize ?? 0;

                if (quantity <= 0) return;

                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Granting {playerName} credit for clayforming {quantity} of {outputCode}");
                AttributeModifierDefinitions.Potter.AddCollectedItem(serverPlayer, outputCode);
            }
        }

        /// <summary>
        /// Stores the state of the anvil CheckIfFinished method.
        /// </summary>
        public class AnvilCheckIfFinishedState
        {
            public ItemStack workItemStack;
            public SmithingRecipe recipe;
        }
        
        public static void BlockEntityAnvil_CheckIfFinished_Prefix(BlockEntityAnvil __instance, out AnvilCheckIfFinishedState __state, IPlayer byPlayer, ItemStack ___workItemStack)
        {
            // Pass necessary state on to postfix method
            __state = new()
            {
                workItemStack = ___workItemStack,
                recipe = __instance.SelectedRecipe
            };
        }

        public static void BlockEntityAnvil_CheckIfFinished_Postfix(BlockEntityClayForm __instance, AnvilCheckIfFinishedState __state, IPlayer byPlayer, ItemStack ___workItemStack)
        {
            if (byPlayer?.Entity?.Api?.Side == EnumAppSide.Server)
            {
                if (byPlayer is not IServerPlayer serverPlayer || ___workItemStack != null || __state.recipe == null) return;

                string playerName = serverPlayer?.PlayerName;
                string outputCode = __state.recipe.Output?.ResolvedItemstack?.Collectible?.Code?.ToString();
                int quantity = __state.recipe.Output?.ResolvedItemstack?.StackSize ?? 0;

                if (quantity <= 0) return;

                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Granting {playerName} credit for smithing {quantity} of {outputCode}");
                AttributeModifierDefinitions.MasterCraftsman.AddCollectedItem(serverPlayer, outputCode);
            }
        }
    }
}
