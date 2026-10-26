using System;
using System.IO;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using SeraphLeveling.Data.Traits;

namespace SeraphLeveling.Data.Attributes
{
    public class ProspectorAttributeModifierDefinition : ScoredUnlockedAttributeModifierDefinition<ProspectorAttributeModifierDefinition, ProspectorAttributeModifierProgressData>, IConstructable<ProspectorAttributeModifierDefinition, ProspectorAttributeModifierProgressData>
    {
        public static ProspectorAttributeModifierProgressData Create(ProspectorAttributeModifierDefinition def)
        {
            return new ProspectorAttributeModifierProgressData(def);
        }

        public override void GetTraitUnlockableCommandLine(IPlayer player, StringBuilder sb) {
            var progress = GetDict(player);
            sb.AppendLine($"{Name}: {progress.TotalCredits}/{GlobalMaxCredits} blocks panned ({(progress.IsUnlocked ? "UNLOCKED" : "locked")})");
        }
    }

    public class ProspectorAttributeModifierProgressData(ProspectorAttributeModifierDefinition definition) : ScoredUnlockedAttributeModifierProgressData<ProspectorAttributeModifierDefinition, ProspectorAttributeModifierProgressData>(definition)
    {
    }
}
