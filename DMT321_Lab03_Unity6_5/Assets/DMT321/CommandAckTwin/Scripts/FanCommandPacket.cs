using System;

namespace DMT321.CommandAckTwin
{
    /// <summary>
    /// JSON contract sent from Unity to the fan device.
    /// JSON keys must match these field names exactly.
    /// </summary>
    [Serializable]
    public class FanCommandPacket
    {
        public string deviceId;
        public string commandId;
        public bool fanOn;
    }
}
