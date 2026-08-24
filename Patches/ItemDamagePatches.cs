using System;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SeraphLeveling.Patches
{
    /// <summary>
    /// Server-side Harmony patches for item damage tracking.
    /// </summary>
    public static class ItemDamagePatches
    {
        public static event TriggerToolDamagedDelegate TriggerToolDamaged;

        public static void DamageItem_Prefix(CollectibleObject __instance, IWorldAccessor world, Entity byEntity, ItemSlot itemSlot, int amount = 1, bool destroyOnZeroDurability = true)
        {
            if (__instance.Tool == null) return;
            if (byEntity is not EntityPlayer byPlayer) return;
            if (byPlayer.Player is not IServerPlayer player) return;
            var toolCode = __instance.Code;
#if SPAMMYDEBUG
            ServerApi.Logger.Debug($"[SeraphLeveling] Checking item damage points for tool code {toolCode}.");
#endif

            // Fire event for all attributes listening for tool usage
            TriggerToolDamaged?.Invoke(player, toolCode, amount);
        }
        
    }
}
