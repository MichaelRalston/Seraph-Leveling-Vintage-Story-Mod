using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace SeraphLeveling
{
    /// <summary>
    /// Server-side Harmony patches for entity death (death penalty).
    /// </summary>
    public static class EntityDeathPatches
    {
        public static void Die_Postfix(Entity __instance)
        {
            if (!SeraphLevelingModSystem.EnableDeathPenalty) return;
            var playerEntity = __instance as EntityPlayer;
            if (playerEntity == null) return;

            if (SeraphLevelingModSystem.DebugLoggingEnabled)
            {
                SeraphLevelingModSystem.ServerApi?.Logger.Debug(
                    $"[SeraphLeveling] Die_Postfix: Entity is EntityPlayer with UID={playerEntity.PlayerUID}");
            }

            // Use ServerApi.World to ensure we get the server-side player reference
            var player = SeraphLevelingModSystem.ServerApi?.World?.PlayerByUid(playerEntity.PlayerUID) as IServerPlayer;
            if (player == null)
            {
                if (SeraphLevelingModSystem.DebugLoggingEnabled)
                {
                    SeraphLevelingModSystem.ServerApi?.Logger.Debug(
                        $"[SeraphLeveling] Die_Postfix: Could not get IServerPlayer for UID={playerEntity.PlayerUID}");
                }
                return;
            }

            if (SeraphLevelingModSystem.DebugLoggingEnabled)
            {
                SeraphLevelingModSystem.ServerApi?.Logger.Debug(
                    $"[SeraphLeveling] Die_Postfix: Applying death penalty to {player.PlayerName}");
            }

            SeraphLevelingModSystem.ApplyDeathPenalty(player);
        }
    }
}