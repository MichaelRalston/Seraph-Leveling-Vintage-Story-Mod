using ProtoBuf;

namespace SeraphLeveling.Messages
{
    /// <summary>
    /// Server to client: the full progression report shown on the handbook
    /// page "Seraph Leveling: My Progress". Sent on join, on every level-up,
    /// whenever the client asks for it (page open, and once a second while
    /// it stays open), and every 15 s when it changed.
    /// </summary>
    [ProtoContract]
    public class ProgressReportMessage
    {
        [ProtoMember(1)]
        public string Report { get; set; }
        [ProtoMember(2)]
        public ProgressReportContent[] Content { get; set; }
    }
}