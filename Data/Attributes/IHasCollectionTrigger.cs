using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public delegate void TriggerItemCollectedDelegate(IServerPlayer player, AssetLocation itemCode);

    public interface IHasCollectionTrigger<D, PD> : ISaveableAttribute where PD : UnlockedAttributeModifierProgressData<D, PD> where D : UnlockedAttributeModifierDefinition<D, PD>, IConstructable<D, PD>
    {
        public abstract void AddCollectedItem(IServerPlayer player, AssetLocation toAdd);
        public void OnTriggerItemCollected(IServerPlayer player, AssetLocation itemCode)
        {
            if (player == null || !SeraphLevelingModSystem.LoadedAttributes.Contains(this))
            {
                return;
            }
            else
            {
                AddCollectedItem(player, itemCode);
            }
        }
    }
}
