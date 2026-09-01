using System;
using UnityEngine;

namespace DMT321.JsonDataTwin
{
    public enum TelemetryDataStatus
    {
        Waiting,
        Live,
        Stale,
        Offline
    }

    /// <summary>
    /// Receives raw JSON from any source, validates it, and stores Twin state.
    /// A future Serial, HTTP, or MQTT adapter can reuse ReceiveJson unchanged.
    /// </summary>
    public class TelemetryReceiver : MonoBehaviour
    {
        private const float MinimumTemperatureC = 15f;
        private const float MaximumTemperatureC = 40f;

        [Header("Data quality rule")]
        [SerializeField, Min(0.1f)]
        private float staleAfterSeconds = 3f;

        [Header("Runtime state")]
        [SerializeField] private bool isConnected = true;
        [SerializeField] private bool hasReading;
        [SerializeField] private bool requiresFreshReading;
        [SerializeField] private string currentDeviceId = string.Empty;
        [SerializeField] private float temperatureC;
        [SerializeField] private float lastReceivedTime = -1f;
        [SerializeField, TextArea(2, 4)]
        private string lastValidJson = string.Empty;
        [SerializeField] private string lastError = string.Empty;

        public float StaleAfterSeconds
        {
            get { return staleAfterSeconds; }
        }

        public bool IsConnected
        {
            get { return isConnected; }
        }

        public bool HasReading
        {
            get { return hasReading; }
        }

        public bool RequiresFreshReading
        {
            get { return requiresFreshReading; }
        }

        public string CurrentDeviceId
        {
            get { return currentDeviceId; }
        }

        public float TemperatureC
        {
            get { return temperatureC; }
        }

        public float LastReceivedTime
        {
            get { return lastReceivedTime; }
        }

        public string LastValidJson
        {
            get { return lastValidJson; }
        }

        public string LastError
        {
            get { return lastError; }
        }

        public TelemetryDataStatus DataStatus
        {
            get { return GetDataStatus(Time.unscaledTime); }
        }

        /// <summary>
        /// This is the one entry point every transport adapter should call.
        /// Returns false when the connection, JSON, or value is invalid.
        /// Invalid input never overwrites the last valid reading.
        /// </summary>
        public bool ReceiveJson(string rawJson)
        {
            if (!isConnected)
            {
                return Reject("Source is offline. Reconnect before sending.");
            }

            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return Reject("JSON is empty.");
            }

            if (!ContainsRequiredKey(rawJson, "deviceId") ||
                !ContainsRequiredKey(rawJson, "temperatureC"))
            {
                return Reject(
                    "JSON must contain deviceId and temperatureC exactly.");
            }

            SensorPacket packet;

            try
            {
                packet = JsonUtility.FromJson<SensorPacket>(rawJson);
            }
            catch (Exception exception)
            {
                return Reject("JSON syntax error: " + exception.Message);
            }

            if (packet == null)
            {
                return Reject("JSON could not be converted to SensorPacket.");
            }

            if (string.IsNullOrWhiteSpace(packet.deviceId))
            {
                return Reject("deviceId must not be empty.");
            }

            if (float.IsNaN(packet.temperatureC) ||
                float.IsInfinity(packet.temperatureC) ||
                packet.temperatureC < MinimumTemperatureC ||
                packet.temperatureC > MaximumTemperatureC)
            {
                return Reject(
                    "temperatureC must be between 15 and 40 degrees C.");
            }

            currentDeviceId = packet.deviceId.Trim();
            temperatureC = packet.temperatureC;
            lastReceivedTime = Time.unscaledTime;
            lastValidJson = rawJson;
            lastError = string.Empty;
            hasReading = true;
            requiresFreshReading = false;

            Debug.Log(
                "Telemetry accepted: " + currentDeviceId + " / " +
                temperatureC.ToString("0.0") + " C",
                this);
            return true;
        }

        public void Disconnect()
        {
            isConnected = false;
        }

        public void Reconnect()
        {
            if (isConnected)
            {
                return;
            }

            isConnected = true;

            // The old value is retained, but reconnecting alone is not proof
            // that a new sensor sample has arrived.
            requiresFreshReading = hasReading;
        }

        public void ResetForNewSession()
        {
            isConnected = true;
            hasReading = false;
            requiresFreshReading = false;
            currentDeviceId = string.Empty;
            temperatureC = 0f;
            lastReceivedTime = -1f;
            lastValidJson = string.Empty;
            lastError = string.Empty;
        }

        public TelemetryDataStatus GetDataStatus(float nowSeconds)
        {
            if (!isConnected)
            {
                return TelemetryDataStatus.Offline;
            }

            if (!hasReading)
            {
                return TelemetryDataStatus.Waiting;
            }

            if (requiresFreshReading)
            {
                return TelemetryDataStatus.Stale;
            }

            return GetDataAgeSeconds(nowSeconds) < staleAfterSeconds
                ? TelemetryDataStatus.Live
                : TelemetryDataStatus.Stale;
        }

        public float GetDataAgeSeconds(float nowSeconds)
        {
            if (!hasReading)
            {
                return 0f;
            }

            return Mathf.Max(0f, nowSeconds - lastReceivedTime);
        }

        private static bool ContainsRequiredKey(string json, string key)
        {
            return json.IndexOf(
                "\"" + key + "\"",
                StringComparison.Ordinal) >= 0;
        }

        private bool Reject(string message)
        {
            lastError = message;
            Debug.LogWarning("Telemetry rejected: " + message, this);
            return false;
        }
    }
}
