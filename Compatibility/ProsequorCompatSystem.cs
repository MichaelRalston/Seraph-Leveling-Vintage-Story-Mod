using System;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Config;

namespace SeraphLeveling
{
    public class ProsequorCompatSystem : ModSystem
    {
        // Pushes this system to run slightly after standard systems (default is 0)
        public override double ExecuteOrder() => 0.2;

        public override void StartClientSide(ICoreClientAPI api)
        {
            // Always run the base framework method first
            base.StartClientSide(api);

            // Abort instantly if the user doesn't have Prosequor installed
            if (!api.ModLoader.IsModEnabled("prosequor")) return;

            try
            {
                // Target: ModConfig/prosequor/client.json
                string targetPath = Path.Combine(GamePaths.ModConfig, "prosequor", "client.json");

                if (File.Exists(targetPath))
                {
                    string jsonText = File.ReadAllText(targetPath);
                    JsonObject configJson = JsonObject.FromJson(jsonText);

                    // Check if it's currently null or explicitly set to false
                    if (configJson["ShowVanillaTraitsTab"]?.AsBool() != true)
                    {
                        if (configJson.Token is Newtonsoft.Json.Linq.JObject jObject)
                        {
                            // Now you can assign the boolean directly!
                            jObject["ShowVanillaTraitsTab"] = true;

                            // Write the modified JSON text back to the file system
                            File.WriteAllText(targetPath, configJson.ToString());
                            api.Logger.Notification("[SeraphLeveling] Successfully configured Prosequor to display the Vanilla Traits Tab.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[SeraphLeveling] Failed to adjust Prosequor's client config: {ex.Message}");
            }
        }
    }
}