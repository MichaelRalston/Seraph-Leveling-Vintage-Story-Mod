using System;
using System.Text;
using SeraphLeveling.Data.Traits;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public interface IAttributeRequirement
    {
        public string AttributeId { get; }
        public string CurrentValue(IPlayer player);
        public string RequiredValue { get; }
        public bool IsSatisfied(IPlayer player);
        public void CollectStatus(IPlayer player, StringBuilder sb);

        /// <summary>
        /// Registers a method to be called every time satisfaction of this requirement changes for a player
        /// </summary>
        public event SatisfactionChangedDelegate SatisfactionChanged;
    }

    public delegate void SatisfactionChangedDelegate(IServerPlayer player, bool oldValue, bool newValue);

    public abstract record class LeveledAttributeRequirement : IAttributeRequirement
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

        public string AttributeId => Attribute.Id;
        public string CurrentValue(IPlayer player)
        {
            return GetCombinedModifierPercent(player.Entity).ToString();
        }

        public string RequiredValue => ThresholdPercentage.ToString();

        public int ThresholdPercentage { get; init; } = 0;

        ~LeveledAttributeRequirement()
        {
            Attribute?.BonusChanged -= OnAttributeBonusChanged;
        }

        public event SatisfactionChangedDelegate SatisfactionChanged;

        public abstract bool IsSatisfied(IPlayer player);

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
                var percent = GetCombinedModifierPercent(player.Entity);
                sb.AppendLine($"  {Attribute.Name} level: {percent}% / {ThresholdPercentage}% ({(percent >= ThresholdPercentage ? "✓" : "✗")})");
            }
        }

        protected virtual int GetCombinedModifierPercent(EntityPlayer player)
        {
            int retVal = Attribute.GetBonusPercent(player);
            if (SeraphLevelingModSystem.TraitsForAttributes.TryGetValue(Attribute.Id, out var traitModList))
            {
                foreach (var (traitDef, modVal) in traitModList)
                {
                    if (SeraphLevelingModSystem.PlayerHasTrait(player, traitDef))
                    {
                        retVal += modVal;
                    }
                }
            }
            return retVal;
        }
    }

    public record class LeveledAttributeMinimumRequirement : LeveledAttributeRequirement
    {
        public override bool IsSatisfied(IPlayer player) => (player?.Entity != null) && GetCombinedModifierPercent(player.Entity) >= ThresholdPercentage;
    }

    public record class LeveledAttributeMaximumRequirement : LeveledAttributeRequirement
    {
        public override bool IsSatisfied(IPlayer player) => (player?.Entity != null) && GetCombinedModifierPercent(player.Entity) < ThresholdPercentage;
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

        public string AttributeId => Attribute.Id;
        public string CurrentValue(IPlayer player) => Attribute.IsUnlockedForPlayer(player).ToString();
        public string RequiredValue => true.ToString();

        ~UnlockedAttributeRequirement()
        {
            Attribute?.UnlockChanged -= OnRequirementSatisfactionChanged;
        }

        public event SatisfactionChangedDelegate SatisfactionChanged;

        public bool IsSatisfied(IPlayer player) => Attribute.IsUnlockableForPlayer(player);

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
