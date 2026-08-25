using System;
using System.Collections.Generic;
using System.Linq;
using SeraphLeveling.Data.Tools;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data.Attributes
{
    public delegate void TriggerDamageDealtDelegate(IServerPlayer player, AssetLocation weaponCode, float damage);
    
    public interface IHasDamageDealtTrigger<D, PD, E> : ISaveableAttribute where PD : LeveledToolAttributeModifierProgressData<D, PD, E> where D : LeveledToolAttributeModifierDefinition<D, PD, E>, IConstructable<D, PD> where E : Enum
    {
        public List<ToolDefinition> Weapons { get; }

        public PD GetForPlayer(string playerUid);

        public void OnTriggerDamageDealt(IServerPlayer player, AssetLocation weaponCode, float damage)
        {
            if (player == null || damage <= 0 || !Weapons.Any(w => w.Matches(weaponCode)) || !SeraphLevelingModSystem.LoadedAttributes.Contains(this))
            {
                return;
            }
            else
            {
                GetForPlayer(player.PlayerUID).DoEvent(player, weaponCode, damage, default);
            }
        }        
    }
}
