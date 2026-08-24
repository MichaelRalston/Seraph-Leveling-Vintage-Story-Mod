using SeraphLeveling.Util;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public delegate void TriggerGridCraftingResultDelegate(IServerPlayer player, AssetLocation resultCode, int quantity);

    public interface IHasGridCraftingResultTrigger : ISaveableAttribute
    {
        public IAssetLocationMatcher ResultAllowList { get; }
        public IAssetLocationMatcher ResultBanList { get; }

        public void AddCredits(IServerPlayer player, float toAdd);

        public void OnTriggerGridCraftingResult(IServerPlayer player, AssetLocation resultCode, int quantity)
        {
            if (player == null || quantity <= 0 || !SeraphLevelingModSystem.LoadedAttributes.Contains(this) || (ResultBanList?.Matches(resultCode) ?? false))
            {
                return;
            }
            else if (ResultAllowList?.Matches(resultCode) ?? false)
            {
                AddCredits(player, quantity);
            }
        }
    }
}
