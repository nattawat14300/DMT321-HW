using System;

namespace DMT321.CommandAckTwin
{
    /// <summary>
    /// JSON acknowledgement returned by the fan device.
    /// reportedFanOn is the device-reported state after processing a command.
    /// </summary>
    [Serializable]
    public class FanAckPacket
    {
        public string deviceId;
        public string commandId;
        public bool accepted;
        public bool reportedFanOn;
        public string message;
    }
}
