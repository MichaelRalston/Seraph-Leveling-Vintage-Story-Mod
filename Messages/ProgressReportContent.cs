using ProtoBuf;

namespace SeraphLeveling.Messages {

    [ProtoContract]
    public class PartialCredits
    {
        [ProtoMember(1)]
        public string Name { get; set; }
        [ProtoMember(2)]
        public float Percentage { get; set; }
        [ProtoMember(3)]
        public string Tooltip { get; set; }
    }

    [ProtoContract]
    public class ProgressReportContent
    {
        [ProtoMember(1)]
        public string Name { get; set; }
        [ProtoMember(2)]
        public float Percentage { get; set; }
        [ProtoMember(3)]
        public string Tooltip { get; set; }
        [ProtoMember(4)]
        public PartialCredits[] PartialCredits { get; set; }
        [ProtoMember(5)]
        public string ExtraInfo { get; set; }
        [ProtoMember(6)]
        public string Instructions { get; set; }
    }
}