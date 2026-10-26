namespace SeraphLeveling.Data.Attributes
{
    public abstract class CollectionUnlockedAttributeModifierDefinition<D, PD> : BaseCollectionUnlockedAttributeModifierDefinition<D, PD>
        where PD : CollectionUnlockedAttributeModifierProgressData<D, PD>
        where D : CollectionUnlockedAttributeModifierDefinition<D, PD>,
        IConstructable<D, PD>,
        IHasCollectionTrigger<D, PD>
    {
        public CollectionUnlockedAttributeModifierDefinition()
        {
            SeraphLevelingModSystem.ItemCollectedTrigger += ((IHasCollectionTrigger<D, PD>)this).OnTriggerItemCollected;
        }

        ~CollectionUnlockedAttributeModifierDefinition()
        {
            SeraphLevelingModSystem.ItemCollectedTrigger -= ((IHasCollectionTrigger<D, PD>)this).OnTriggerItemCollected;
        }
    }

    public class CollectionUnlockedAttributeModifierProgressData<D, PD>(D definition) : BaseCollectionUnlockedAttributeModifierProgressData<D, PD>(definition) where PD : CollectionUnlockedAttributeModifierProgressData<D, PD> where D : CollectionUnlockedAttributeModifierDefinition<D, PD>, IConstructable<D, PD>, IHasCollectionTrigger<D, PD>
    {
    }
}
