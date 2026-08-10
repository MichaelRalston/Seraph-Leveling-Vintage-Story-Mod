using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace SeraphLeveling.Patches
{
    /// <summary>
    /// Server-side Harmony patches for bed sleeping (sleep buff).
    /// </summary>
    public static class BedSleepPatches
    {
        /// <summary>
        /// Postfix for BlockEntityBed.DidUnmount - applies sleep buff when player gets out of bed.
        /// DidUnmount(EntityAgent) is called when a player unmounts from the bed after sleeping.
        /// __instance is the BlockEntityBed itself (a BlockEntity subclass).
        /// </summary>
        public static void DidUnmount_Postfix(object __instance, EntityAgent entityAgent)
        {
            try
            {
                if (!SeraphLevelingModSystem.EnableSleepBuff) return;
                if (entityAgent == null) return;

                var playerEntity = entityAgent as EntityPlayer;
                if (playerEntity == null) return;

                var serverPlayer = SeraphLevelingModSystem.ServerApi?.World?.PlayerByUid(playerEntity.PlayerUID) as IServerPlayer;
                if (serverPlayer == null) return;

                string playerUid = serverPlayer.PlayerUID;
                if (string.IsNullOrEmpty(playerUid)) return;

                // Dedup: beds have two parts (head + foot), so DidUnmount fires twice.
                // Skip if we already applied the buff within the last 2 seconds (real time).
                long currentTick = Environment.TickCount64;
                if (SeraphLevelingModSystem.LastSleepBuffApplyTick.TryGetValue(playerUid, out long lastTick)
                    && (currentTick - lastTick) < 2000)
                {
                    return;
                }
                SeraphLevelingModSystem.LastSleepBuffApplyTick[playerUid] = currentTick;

                // Determine bed type from the block entity's block code
                float multiplier = SeraphLevelingModSystem.SleepBuffLinenBedMultiplier; // Default to linen bed multiplier
                bool isHayBed = false;

                try
                {
                    // __instance IS the BlockEntityBed, which inherits from BlockEntity
                    var blockEntity = __instance as BlockEntity;
                    if (blockEntity?.Block?.Code != null)
                    {
                        string blockCode = blockEntity.Block.Code.ToString().ToLowerInvariant();

                        // Check if it's a hay bed
                        if (blockCode.Contains("hay"))
                        {
                            isHayBed = true;
                            multiplier = SeraphLevelingModSystem.SleepBuffHayBedMultiplier;
                        }
                        // Linen and old beds use the default linen multiplier

                        if (SeraphLevelingModSystem.DebugLoggingEnabled)
                        {
                            SeraphLevelingModSystem.ServerApi?.Logger.Debug(
                                $"[SeraphLeveling] Sleep buff: {serverPlayer.PlayerName} slept in {blockCode}, multiplier: {multiplier}x");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // If we can't determine bed type, use default linen multiplier
                    System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Could not determine bed type: {ex.Message}");
                }

                // Calculate expiration time
                double currentDay = SeraphLevelingModSystem.ServerApi?.World?.Calendar?.TotalDays ?? 0;
                double expirationDay = currentDay + SeraphLevelingModSystem.SleepBuffDurationDays;

                // Apply the buff
                SeraphLevelingModSystem.SleepBuffExpiration[playerUid] = expirationDay;
                SeraphLevelingModSystem.SleepBuffMultiplier[playerUid] = multiplier;
                SeraphLevelingModSystem.pendingSleepBuffSave = true;

                // Notify the player
                string bedType = isHayBed ? "hay bed" : "comfortable bed";
                serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup,
                    $"Well rested! Skill XP x{multiplier} for the next {SeraphLevelingModSystem.SleepBuffDurationDays} day(s) from sleeping in a {bedType}.",
                    EnumChatType.Notification);

                SeraphLevelingModSystem.ServerApi?.Logger.Debug(
                    $"[SeraphLeveling] Applied sleep buff to {serverPlayer.PlayerName}: {multiplier}x until day {expirationDay:F2}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in DidUnmount_Postfix: {ex.Message}");
            }
        }
    }
}
