using ProtoBuf;

namespace SeraphLeveling.Messages
{
    /// <summary>
    /// Network message sent from server to client to play a level-up sound.
    /// </summary>
    [ProtoContract]
    public class LevelUpSoundMessage
    {
        [ProtoMember(1)]
        public string SoundName { get; set; }

        [ProtoMember(2)]
        public float Volume { get; set; }

        /// <summary>
        /// True when this packet originates from /trait testsound. The client logs and prints a chat
        /// confirmation showing the volume it actually received, so we can verify the network path.
        /// </summary>
        [ProtoMember(3)]
        public bool IsTest { get; set; }
    }
}
