using ProtoBuf;

namespace SeraphLeveling.Messages
{
    /// <summary>
    /// Client to server: the handbook progress page wants the current report
    /// now. Force = send it even if nothing changed since the last push.
    /// </summary>
    [ProtoContract]
    public class ProgressReportRequestMessage
    {
        [ProtoMember(1)]
        public bool ListeningToCharacter { get; set; }
        [ProtoMember(2)]
        public bool ListeningToHandbook { get; set; }
    }
}