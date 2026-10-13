using ProtoBuf;
using System.Collections.Generic;

namespace SeraphLeveling.Messages
{

    [ProtoContract]
    public class PartialCredits
    {
        [ProtoMember(1)]
        public string Name { get; set; }
        [ProtoMember(2)]
        public float Percentage { get; set; }
        [ProtoMember(3)]
        public string Tooltip { get; set; }

        public bool IsSame(PartialCredits rhs)
        {
            if (rhs == null) return false;
            return (Name == rhs.Name) && (Percentage == rhs.Percentage) && (Tooltip == rhs.Tooltip);
        }
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
        [ProtoMember(7)]
        public bool CollapseExtraInfo { get; set; }

        public bool IsSame(ProgressReportContent rhs)
        {
            if (rhs == null) return false;
            if (!((Name == rhs.Name) && (Percentage == rhs.Percentage) && (Tooltip == rhs.Tooltip) && (ExtraInfo == rhs.ExtraInfo) && (Instructions == rhs.Instructions) && (CollapseExtraInfo == rhs.CollapseExtraInfo))) return false;
            if (PartialCredits != null)
            {
                if (rhs.PartialCredits == null) return false;
                if (PartialCredits.Length != rhs.PartialCredits.Length) return false;
                for (int i = 0; i < PartialCredits.Length; i++)
                {
                    if (!PartialCredits[i].IsSame(rhs.PartialCredits[i])) return false;
                }
            }
            else if (rhs.PartialCredits != null) return false;
            return true;
        }
    }

    public class ProgressReportComparer : IEqualityComparer<ProgressReportContent>
    {
        public bool Equals(ProgressReportContent lhs, ProgressReportContent rhs)
        {
            if (ReferenceEquals(lhs, rhs)) return true;
            if (lhs is null || rhs is null) return false;
            return lhs.IsSame(rhs);
        }
        public int GetHashCode(ProgressReportContent obj) => 0;
    }
}