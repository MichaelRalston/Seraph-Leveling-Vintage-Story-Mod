using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using System.Reflection;
using HarmonyLib;
using SeraphLeveling.Data.Attributes;

namespace SeraphLeveling.Patches
{
    public static class MeditationPatches
    {
        private static FieldInfo drainFieldInfo = null;
        public static void ServerPacketPlayerTemporalStabilityDrain_Prefix(IPlayer fromPlayer, object networkMessage)
        {
            IServerPlayer serverPlayer = fromPlayer as IServerPlayer;
            if (networkMessage == null || serverPlayer == null) return;
            if (serverPlayer.Entity == null) return;
            try
            {
                if (drainFieldInfo == null)
                {
                    Type msgType = networkMessage.GetType();
                    drainFieldInfo = AccessTools.Field(msgType, "inputDoubleTSToDrain");
                }

                if (drainFieldInfo != null)
                {
                    double originalDrain = (double)drainFieldInfo.GetValue(networkMessage);
                    string playerUid = serverPlayer.PlayerUID;

                    // double modifiedDrain = CalculateNewDrain(fromPlayer, originalDrain);
                    var pd = AttributeModifierDefinitions.Enlightenment.GetForPlayer(playerUid);
                    
                    pd.DoEvent(serverPlayer, 1);
                    var stat = serverPlayer.Entity.Stats.GetBlended(AttributeModifierDefinitions.Enlightenment.StatName);
                    double adjustment = (1-stat)/20f;
                    double modifiedDrain = Math.Max(0.01, originalDrain-adjustment); // Cap your reduction in case you stack it with the resonator or whatever it is.

                    // Modifies the reference object in place before the original method sees it
                    drainFieldInfo.SetValue(networkMessage, modifiedDrain);
                }
            }
            catch (Exception ex)
            {
                // Fail silently or log to avoid crashing the server if the other mod updates
                fromPlayer.Entity?.Api?.Logger?.Error($"[SeraphLeveling] Error in RM meditation patch: {ex.Message}");
            }

        }
    }
}
