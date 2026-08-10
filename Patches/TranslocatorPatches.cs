using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Patches
{
    /// <summary>
    /// Server-side Harmony patches for translocator repairs (Technical trait).
    /// </summary>
    public static class TranslocatorPatches
    {
        /// <summary>
        /// Postfix for BlockEntityStaticTranslocator.DoRepair - tracks when player repairs a translocator.
        /// </summary>
        public static void DoRepair_Postfix(object __instance, IPlayer byPlayer)
        {
            try
            {
                // Only process on server
                if (byPlayer == null) return;

                var serverPlayer = byPlayer as IServerPlayer;
                if (serverPlayer == null) return;

                // Get the repairState and RepairInteractionsRequired via reflection
                var instanceType = __instance.GetType();
                var repairStateField = instanceType.GetField("repairState",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var repairRequiredField = instanceType.GetField("RepairInteractionsRequired",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (repairStateField == null || repairRequiredField == null) return;

                int repairState = (int)repairStateField.GetValue(__instance);
                int repairRequired = (int)repairRequiredField.GetValue(__instance);

                // Check if this repair just completed (FullyRepaired is now true)
                if (repairState >= repairRequired)
                {
                    SeraphLevelingModSystem.ProcessTranslocatorRepair(serverPlayer);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SeraphLeveling] Error in DoRepair_Postfix: {ex.Message}");
            }
        }
    }
}
