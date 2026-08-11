using SeraphLeveling.Data.Attributes;
using SeraphLeveling.Data.Legacy;

namespace SeraphLeveling.Data
{
    /// <summary>
    /// Main mod system for Simple Improving Traits.
    /// Provides a progression system that improves player traits through gameplay.
    /// Currently implements mining speed progression based on blocks mined.
    /// </summary>
    /// <summary>
    /// A snapshot of one player's full progression across every system, used to
    /// transfer progress between worlds/servers via /trait export and /trait
    /// import. Each field is null when the player has no data for that system.
    /// Plain public fields/auto-properties so it round-trips cleanly through
    /// Newtonsoft JSON.
    /// </summary>
    public class PlayerProgressExport
    {
        public int FormatVersion = 2;
        public string SourcePlayerName;
        public string SourcePlayerUid;
        public double ExportedGameDay;

        public MiningAttributeModifierProgressData Mining;
        public MeleeProgressData Melee;
        public RangedProgressData Ranged;
        public LeveledPartialAttributeModifierProgressData Walking;
        public LeveledPartialAttributeModifierProgressData Hunger;
        public ArmorProgressData Armor;
        public ClothierProgressData Clothier;
        public MenderProgressData Mender;
        public PilfererProgressData Pilferer;
        public ResourcefulProgressData Resourceful;
        public ForagerProgressData Forager;
        public FurtiveProgressData Furtive;
        public PreciseProgressData Precise;
        public TechnicalProgressData Technical;
        public HardyHealthProgressData HardyHealth;
        public BowyerProgressData Bowyer;
        public ImproviserProgressData Improviser;
        public TinkererAttributeModifierProgressData Tinkerer;
        public MercilessProgressData Merciless;
        public ClaustrophobicRemovalProgressData ClaustrophobicRemoval;
        public HeavyFootedRemovalProgressData HeavyFootedRemoval;
        public COPlayerProgressData CombatOverhaul;
    }
}
