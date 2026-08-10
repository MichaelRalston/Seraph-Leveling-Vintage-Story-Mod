namespace SeraphLeveling.Data
{
    /// <summary>
    /// Cached vanilla trait data for a player.
    /// Populated once on player join to avoid repeated GetStringArray calls.
    /// </summary>
    public class CachedVanillaTraits
    {
        public bool HasHardy { get; set; }
        public bool HasSoldier { get; set; }
        public bool HasFocused { get; set; }
        public bool HasFleetfooted { get; set; }
        public bool HasRavenous { get; set; }
        public bool HasFarsighted { get; set; }
        public bool HasNervous { get; set; }
        public bool HasNearsighted { get; set; }
        public bool HasFrail { get; set; }
        public bool HasCivil { get; set; }
        public bool HasWeak { get; set; }
        public bool HasKind { get; set; }
        public bool HasHeavyhanded { get; set; }
        public bool HasClaustrophobic { get; set; }
        public bool HasFurtive { get; set; }
        public bool HasPrecise { get; set; }
        public bool HasMender { get; set; }
        public bool HasPilferer { get; set; }
        public bool HasResourceful { get; set; }
        public bool HasForager { get; set; }

        // Combat Overhaul negative traits
        public bool HasCOTremblingAim { get; set; }
        public bool HasCOClumsyHands { get; set; }
        public bool HasCOFearOfMelee { get; set; }
        public bool HasCOWeakHand { get; set; }
        public bool HasCONervous { get; set; }

        // Combat Overhaul mixed/positive traits (Big Head, Thick Skull, Leg Day, Melee Expert, Self Defence)
        public bool HasCOBigHead { get; set; }
        public bool HasCOThickSkull { get; set; }
        public bool HasCOLegDay { get; set; }
        public bool HasCOMeleeExpert { get; set; }
        public bool HasCOSelfDefence { get; set; }
    }
}
