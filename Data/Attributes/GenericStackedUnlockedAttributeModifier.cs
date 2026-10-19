using System.Text;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using SeraphLeveling.Messages;
using System.Linq;
using Vintagestory.API.Util;
using System.Collections.Generic;



namespace SeraphLeveling.Data.Attributes
{
    public class GenericStackedUnlockedAttributeModifierDefinition : UnlockedAttributeModifierDefinition<GenericStackedUnlockedAttributeModifierDefinition, GenericStackedUnlockedAttributeModifierProgressData>, IConstructable<GenericStackedUnlockedAttributeModifierDefinition, GenericStackedUnlockedAttributeModifierProgressData>
    {
        public static GenericStackedUnlockedAttributeModifierProgressData Create(GenericStackedUnlockedAttributeModifierDefinition def)
        {
            return new GenericStackedUnlockedAttributeModifierProgressData(def);
        }

        public override ProgressReportContent CollectReportContent(IPlayer player)
        {
            var progress = GetDict(player);
            var instructionsSb = new StringBuilder();
            CollectInstructions(player, instructionsSb);
            List<PartialCredits> partialCredits = [];
            var attrs = Trait.Value.Attributes.Where(mod => mod.Attribute == this);
            if (attrs.Any())
            {
                attrs.Foreach(mod => partialCredits.AddRange(mod.CollectPartialCredit(player)));
            }
            return new ProgressReportContent()
            {
                Name = Name,
                Percentage = progress.IsUnlocked ? 1 : 0,
                Tooltip = $"{(progress.IsUnlocked ? "UNLOCKED" : "Locked")}",
                PartialCredits = [.. partialCredits],
                Instructions = instructionsSb.ToString(),
            };
        }
    }

    public class GenericStackedUnlockedAttributeModifierProgressData(GenericStackedUnlockedAttributeModifierDefinition definition) : UnlockedAttributeModifierProgressData<GenericStackedUnlockedAttributeModifierDefinition, GenericStackedUnlockedAttributeModifierProgressData>(definition)
    {

    }
}
