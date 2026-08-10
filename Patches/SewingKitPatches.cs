using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SeraphLeveling.Patches
{
    /// <summary>
    /// Server-side Harmony patches for sewing kit repairs (Mender trait).
    /// </summary>
    public static class SewingKitPatches
    {
        // Track which players have recently had repairs to avoid duplicate credits
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> LastRepairTime =
            new System.Collections.Concurrent.ConcurrentDictionary<string, long>();

        // Track item durabilities to detect repairs
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> ItemDurabilities =
            new System.Collections.Concurrent.ConcurrentDictionary<string, int>();

        // Minimum interval between repair credits (in ticks, 20 ticks = 1 second)
        private const long MIN_REPAIR_INTERVAL = 10;

        /// <summary>
        /// Postfix for ItemSewingKit.OnHeldInteractStop - tracks when sewing kit repair completes.
        /// </summary>
        public static void OnHeldInteractStop_Postfix(
            object __instance,
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel)
        {
            try
            {
                // Only count if the repair actually happened (at least some time was spent)
                if (secondsUsed < 0.25f) return;

                // Get the player
                var playerEntity = byEntity as EntityPlayer;
                if (playerEntity == null) return;

                var player = playerEntity.Player as IServerPlayer;
                if (player == null) return;

                // Check cooldown to avoid duplicate credits
                long currentTick = playerEntity.World?.ElapsedMilliseconds ?? 0;
                string playerKey = player.PlayerUID;

                if (LastRepairTime.TryGetValue(playerKey, out long lastTime) &&
                    currentTick - lastTime < MIN_REPAIR_INTERVAL * 50)
                {
                    return; // Too soon since last credit
                }

                // Update last repair time and give credit
                LastRepairTime[playerKey] = currentTick;
                SeraphLevelingModSystem.ProcessMenderRepair(player);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in OnHeldInteractStop_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for CollectibleObject.OnModifiedInInventorySlot - tracks durability changes.
        /// When a wearable item's durability increases, it's likely due to a repair.
        /// </summary>
        public static void OnModifiedInInventorySlot_Postfix(
            CollectibleObject __instance,
            IWorldAccessor world,
            ItemSlot slot,
            ItemStack extractedStack)
        {
            try
            {
                // Only process server-side
                if (world?.Side != EnumAppSide.Server) return;

                // Only track wearable items (clothing and armor)
                string itemCode = __instance.Code?.ToString();
                if (itemCode == null) return;
                if (!itemCode.Contains("clothes-") && !itemCode.Contains("armor-")) return;

                // Get the current durability
                var currentStack = slot?.Itemstack;
                if (currentStack == null) return;

                int currentDurability = currentStack.Collectible?.GetRemainingDurability(currentStack) ?? 0;
                int maxDurability = currentStack.Collectible?.GetMaxDurability(currentStack) ?? 1;

                // Create a unique key for this item instance
                string itemKey = $"{slot.Inventory?.InventoryID}_{slot.Inventory?.GetSlotId(slot)}_{itemCode}";

                // Check if durability increased (repair happened)
                if (ItemDurabilities.TryGetValue(itemKey, out int previousDurability))
                {
                    if (currentDurability > previousDurability)
                    {
                        // Durability increased - repair happened!
                        // Try to find which player owns this inventory
                        var inventory = slot.Inventory;
                        if (inventory != null)
                        {
                            // Find player by checking if this is a character or backpack inventory
                            foreach (var player in world.AllOnlinePlayers)
                            {
                                var serverPlayer = player as IServerPlayer;
                                if (serverPlayer?.InventoryManager == null) continue;

                                // Check if this inventory belongs to this player
                                var characterInv = serverPlayer.InventoryManager.GetOwnInventory(GlobalConstants.characterInvClassName);
                                var backpackInv = serverPlayer.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);

                                if (characterInv == inventory || backpackInv == inventory)
                                {
                                    // Check cooldown
                                    long currentTick = world.ElapsedMilliseconds;
                                    string playerKey = serverPlayer.PlayerUID + "_mod";

                                    if (!LastRepairTime.TryGetValue(playerKey, out long lastTime) ||
                                        currentTick - lastTime >= MIN_REPAIR_INTERVAL * 50)
                                    {
                                        LastRepairTime[playerKey] = currentTick;
                                        SeraphLevelingModSystem.ProcessMenderRepair(serverPlayer);
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                // Update tracked durability
                ItemDurabilities[itemKey] = currentDurability;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in OnModifiedInInventorySlot_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for CollectibleObject.OnHeldInteractStep - tracks sewing kit repairs during use.
        /// This is a fallback for when the sewing kit is used in a world interaction context.
        /// </summary>
        public static void OnHeldInteractStep_Postfix(
            CollectibleObject __instance,
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool __result)
        {
            try
            {
                // Only process if interaction is still ongoing
                if (!__result) return;

                // Check if this is a sewing kit
                string itemCode = __instance.Code?.ToString();
                if (itemCode == null || !itemCode.Contains("sewingkit")) return;

                // Get the player
                var playerEntity = byEntity as EntityPlayer;
                if (playerEntity == null) return;

                var player = playerEntity.Player as IServerPlayer;
                if (player == null) return;

                // Give credit every 0.5 seconds of repair (rate-limited)
                long currentTick = playerEntity.World?.ElapsedMilliseconds ?? 0;
                string playerKey = player.PlayerUID + "_step";

                if (LastRepairTime.TryGetValue(playerKey, out long lastTime) &&
                    currentTick - lastTime < 500) // 500ms cooldown
                {
                    return;
                }

                // Update last repair time and give credit
                LastRepairTime[playerKey] = currentTick;
                SeraphLevelingModSystem.ProcessMenderRepair(player);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in OnHeldInteractStep_Postfix: {ex.Message}");
            }
        }
    }
}
