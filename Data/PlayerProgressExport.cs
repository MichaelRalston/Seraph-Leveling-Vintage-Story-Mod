using System.Collections.Generic;
using SeraphLeveling.Data.Attributes;
using SeraphLeveling.Data.Legacy;

namespace SeraphLeveling.Data
{
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
        public Dictionary<string, object> Attributes;
    }
}
