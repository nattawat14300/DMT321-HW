using System;

namespace DMT321.JsonDataTwin
{
    /// <summary>
    /// The data contract shared by every telemetry source.
    /// JSON keys must match these field names exactly.
    /// </summary>
    [Serializable]
    public class SensorPacket
    {
        public string deviceId;
        public float temperatureC;
    }
}
