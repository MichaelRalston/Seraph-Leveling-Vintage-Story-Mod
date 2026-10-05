using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace SeraphLeveling.Behaviors
{
    public class InteractionTrackingBehavior(BlockEntity blockentity) : BlockEntityBehavior(blockentity)
    {
        public string lastInteractedBy = "";

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("lastInteractedBy", lastInteractedBy);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            lastInteractedBy = tree.GetString("lastInteractedBy", "");
        }
    }
}