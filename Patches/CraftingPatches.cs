using System;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace SeraphLeveling.Patches
{
    public static class CraftingPatches
    {
        public static event TriggerToolRepairDelegate TriggerToolRepair;

        public static void GridRecipeConsumeInput_Postfix(GridRecipe __instance, IPlayer byPlayer, ItemSlot[] inputSlots, int gridWidth, bool __result)
        {
            if (__result && byPlayer?.Entity?.Api?.Side == EnumAppSide.Server)
            {
                if (byPlayer is not IServerPlayer serverPlayer) return;

                string outputPath = __instance?.Output?.Code?.Path;
                int quantity = __instance?.Output?.Quantity ?? 0;

                if (outputPath == null || quantity <= 0) return;

                string playerName = byPlayer?.PlayerName;
#if SPAMMYDEBUG
                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Player {playerName} crafted item code {outputCode} in grid, success={__result}, side={byPlayer?.Entity?.Api?.Side}");
#endif

                // Process boards
                if (outputPath.StartsWith("plank-"))
                {
                    SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Granting {playerName} credit for crafting {quantity} boards");
                    AttributeModifierDefinitions.Carpenter.AddCredits(serverPlayer, quantity);
                }

                if (outputPath.StartsWith("table-") || outputPath.StartsWith("chair-"))
                {
                    SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Granting {playerName} credit for crafting {quantity} furniture");
                    AttributeModifierDefinitions.InteriorDesigner.AddCredits(serverPlayer, quantity);
                }

                // Process ashlar blocks
                if (outputPath.StartsWith("stonebrick-"))
                {
                    SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Granting {playerName} credit for crafting {quantity} ashlar blocks");
                    AttributeModifierDefinitions.Mason.AddCredits(serverPlayer, quantity);
                }

                if (outputPath == "linen-normal-down") {
                    SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Granting {playerName} credit for crafting {quantity} linen cloth");
                    AttributeModifierDefinitions.Weaver.AddCredits(serverPlayer, quantity);
                }

                // Process large gears
                if (outputPath.Contains("largegear3"))
                {
                    SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Granting {playerName} credit for crafting {quantity} large gears");
                    AttributeModifierDefinitions.Technician.AddCredits(serverPlayer, quantity);
                }

                if (outputPath.StartsWith("bomb-"))
                {
                    SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Granting {playerName} credit for crafting {quantity} bombs");
                    AttributeModifierDefinitions.Detonator.AddCredits(serverPlayer, quantity);
                }

                // Fire event for all attributes listening for tool repairs
                TriggerToolRepair?.Invoke(serverPlayer, outputPath, quantity);
            }
        }

        public static void BlockEntityBarrelOnReceivedClientPacket_Postfix(BlockEntityBarrel __instance, IPlayer player, int packetid, byte[] data)
        {
            const int SEAL_BARREL_PACKET_ID = 1337;
            if (player?.Entity?.Api?.Side == EnumAppSide.Server && packetid == SEAL_BARREL_PACKET_ID)
            {
                if (player is not IServerPlayer serverPlayer) return;
                
                string outputPath = __instance?.CurrentRecipe?.Output?.Code?.Path;
                int quantity = __instance?.CurrentRecipe?.Output?.Quantity ?? 0;

                if (outputPath == null || quantity <= 0) return;

                string playerName = serverPlayer?.PlayerName;
#if SPAMMYDEBUG
                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Player {playerName} crafted {quantity} of item code {outputCode} in a barrel");
#endif

                // Process compost
                if (outputPath.Contains("compost"))
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
                var outputCode = __state.recipe.Output?.ResolvedItemstack?.Collectible?.Code;
                int quantity = __state.recipe.Output?.ResolvedItemstack?.StackSize ?? 0;

                if (outputCode == null || quantity <= 0) return;

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

        public static void BlockEntityAnvil_CheckIfFinished_Postfix(BlockEntityAnvil __instance, AnvilCheckIfFinishedState __state, IPlayer byPlayer, ItemStack ___workItemStack)
        {
            if (byPlayer?.Entity?.Api?.Side == EnumAppSide.Server)
            {
                if (byPlayer is not IServerPlayer serverPlayer || ___workItemStack != null || __state.recipe == null) return;

                string playerName = serverPlayer?.PlayerName;
                var outputCode = __state.recipe.Output?.ResolvedItemstack?.Collectible?.Code;
                int quantity = __state.recipe.Output?.ResolvedItemstack?.StackSize ?? 0;

                if (outputCode == null || quantity <= 0) return;

                SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Granting {playerName} credit for smithing {quantity} of {outputCode}");
                AttributeModifierDefinitions.MasterCraftsman.AddCollectedItem(serverPlayer, outputCode);
            }
        }

        public static void BlockEntityAnvil_OnUseOver_Prefix(BlockEntityAnvil __instance, IPlayer byPlayer, Vec3i voxelPos, BlockSelection blockSel)
        {
            if (byPlayer?.Entity?.Api?.Side == EnumAppSide.Server)
            {
                if (byPlayer is not IServerPlayer serverPlayer) return;

                // If the event handling would have aborted, abort here as well
                ItemSlot slot = byPlayer?.InventoryManager?.ActiveHotbarSlot;
                if (voxelPos == null || __instance.SelectedRecipe == null || slot == null || !__instance.CanWorkCurrent) return;

                int toolMode = slot?.Itemstack?.Collectible?.GetToolMode(slot, byPlayer, blockSel) ?? -1;
                byte voxelVal = __instance.Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z];
                bool validHit = toolMode switch
                {
                    // OnHit
                    0 => voxelVal == (byte)EnumVoxelMaterial.Metal && voxelPos.Y > 0,
                    // OnUpset
                    >= 1 and <= 4 => IsValidSmithingUpset(__instance, voxelPos),
                    // OnSplit
                    5 => voxelVal == (byte)EnumVoxelMaterial.Metal || voxelVal == (byte)EnumVoxelMaterial.Slag,
                    // default
                    _ => false
                };

                if (validHit)
                {
                    string playerUid = byPlayer?.PlayerUID;
                    string playerName = serverPlayer?.PlayerName;
                    var toolCode = slot?.Itemstack?.Collectible?.Code;
                    SeraphLevelingModSystem.ServerApi.Logger.Debug($"[SeraphLeveling] Granting {playerName} credit for striking a voxel on an anvil with {toolCode}");
                    AttributeModifierDefinitions.SmithingSpeed.GetForPlayer(playerUid).DoEvent(serverPlayer, toolCode, 1, RepairableToolProgress.Usage);
                    AttributeModifierDefinitions.BitRecoveryRate.GetForPlayer(playerUid).DoEvent(serverPlayer, toolCode, 1, RepairableToolProgress.Usage);
                }
            }
        }

        private static bool IsValidSmithingUpset(BlockEntityAnvil instance, Vec3i voxelPos)
        {
            // Can only move metal
            if (instance.Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z] != (byte)EnumVoxelMaterial.Metal) return false;
            // Can't move if metal is above
            if (voxelPos.Y < 5 && instance.Voxels[voxelPos.X, voxelPos.Y + 1, voxelPos.Z] != (byte)EnumVoxelMaterial.Empty) return false;

            return true;
        }
    }
}
