using System;
using System.Text;
using SeraphLeveling.Data.Traits;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public interface IAttributeRequirement
    {
        public bool IsSatisfied(IPlayer player);
        public void CollectStatus(IPlayer player, StringBuilder sb);

        /// <summary>
        /// Registers a method to be called every time satisfaction of this requirement changes for a player
        /// </summary>
        public event SatisfactionChangedDelegate SatisfactionChanged;
    }

    public record class LeveledAttributeRequirement : IAttributeRequirement
    {
        public required ILeveledAttributeModifierDefinition Attribute
        {
            get;
            init
            {
                field = value;
                field?.BonusChanged += OnAttributeBonusChanged;
            }
        }

        public int ThresholdPercentage { get; init; } = 0;

        ~LeveledAttributeRequirement()
        {
            Attribute?.BonusChanged -= OnAttributeBonusChanged;
        }

        public event SatisfactionChangedDelegate SatisfactionChanged;

        public bool IsSatisfied(IPlayer player) => (player?.Entity != null) && Attribute.GetBonusPercent(player.Entity) >= ThresholdPercentage;

        protected void OnAttributeBonusChanged(IServerPlayer player, int oldBonusPercent, int newBonusPercent)
        {
            bool oldSatisfaction = oldBonusPercent >= ThresholdPercentage;
            bool newSatisfaction = newBonusPercent >= ThresholdPercentage;
            if (oldSatisfaction != newSatisfaction)
            {
                SatisfactionChanged?.Invoke(player, oldSatisfaction, newSatisfaction);
            }
        }

        public void CollectStatus(IPlayer player, StringBuilder sb)
        {
            if (player?.Entity != null)
            {
                sb.AppendLine($"  {Attribute.Name} level: {Attribute.GetBonusPercent(player.Entity)}% / {ThresholdPercentage}% ({(Attribute.GetBonusPercent(player.Entity) >= ThresholdPercentage ? "✓" : "✗")})");
            }
        }
    }

    public record class UnlockedAttributeRequirement : IAttributeRequirement
    {
        public required IUnlockedAttributeModifierDefinition Attribute
        {
            get;
            init
            {
                field = value;
                field?.UnlockChanged += OnRequirementSatisfactionChanged;
            }
        }

        ~UnlockedAttributeRequirement()
        {
            Attribute?.UnlockChanged -= OnRequirementSatisfactionChanged;
        }

        public event SatisfactionChangedDelegate SatisfactionChanged;

        public bool IsSatisfied(IPlayer player) => Attribute.IsUnlockedForPlayer(player);

        private void OnRequirementSatisfactionChanged(IServerPlayer player, bool oldUnlock, bool newUnlock)
        {
            if (newUnlock)
            {
                SatisfactionChanged?.Invoke(player, oldUnlock, newUnlock);
            }
        }

        public void CollectStatus(IPlayer player, StringBuilder sb)
        {
            sb.AppendLine($"  {Attribute.Name}: {(Attribute.IsUnlockedForPlayer(player) ? "UNLOCKED ✓" : "Locked ✗")}");
        }
    }
}
