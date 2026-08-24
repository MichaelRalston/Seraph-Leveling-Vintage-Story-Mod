using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace SeraphLeveling.Patches
{
    /// <summary>
    /// Server-side Harmony patches for poultice and bandage use (Medic and Army Medic traits).
    /// </summary>
    public static class PoulticePatches
    {
        // Track which players have recently had healing to avoid duplicate credits
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> LastHealTime = [];

        // Minimum interval between healing credits (in ticks, 20 ticks = 1 second)
        private const long MIN_HEALING_INTERVAL = 10;

        /// <summary>
        /// Postfix for ItemPoultice.OnHeldInteractStop - tracks when poultice/bandage use completes.
        /// </summary>
        public static void OnHeldInteractStop_Postfix(
            ItemPoultice __instance,
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel)
        {
            try
            {
                // Only count if the healing actually happened (at least some time was spent)
                if (secondsUsed < 0.25f) return;

                // Get the player
                if (byEntity is not EntityPlayer playerEntity) return;

                if (playerEntity.Player is not IServerPlayer player) return;

                // Check cooldown to avoid duplicate credits
                long currentTick = playerEntity.World?.ElapsedMilliseconds ?? 0;
                string playerKey = player.PlayerUID;

                if (LastHealTime.TryGetValue(playerKey, out long lastTime) &&
                    currentTick - lastTime < MIN_HEALING_INTERVAL * 50)
                {
                    return; // Too soon since last credit
                }

                // Update last repair time and give credit
                LastHealTime[playerKey] = currentTick;
                SeraphLevelingModSystem.ProcessPoulticeHeal(player, __instance.Code);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in PoulticePatches.OnHeldInteractStop_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for CollectibleObject.OnHeldInteractStep - tracks poultice healing during use.
        /// This is a fallback for when the poultice is used in a world interaction context.
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

                // Check if this is a poultice or bandage
                var itemCode = __instance.Code;
                if (itemCode == null || (!itemCode.Path.StartsWith("poultice-") && !itemCode.Path.StartsWith("bandage-"))) return;

                // Get the player
                if (byEntity is not EntityPlayer playerEntity) return;

                if (playerEntity.Player is not IServerPlayer player) return;

                // Give credit every 0.5 seconds of repair (rate-limited)
                long currentTick = playerEntity.World?.ElapsedMilliseconds ?? 0;
                string playerKey = player.PlayerUID + "_step";

                if (LastHealTime.TryGetValue(playerKey, out long lastTime) &&
                    currentTick - lastTime < 500) // 500ms cooldown
                {
                    return;
                }

                // Update last repair time and give credit
                LastHealTime[playerKey] = currentTick;
                SeraphLevelingModSystem.ProcessPoulticeHeal(player, itemCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in PoulticePatches.OnHeldInteractStep_Postfix: {ex.Message}");
            }
        }
    }
}
