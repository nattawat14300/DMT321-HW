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
        private const float MinimumHumidity = 0f;
        private const float MaximumHumidity = 100f;
        private bool temperatureEnabled = true;
        private bool humidityEnabled = true;

        [Header("Data quality rule")]
        [SerializeField, Min(0.1f)]
        private float staleAfterSeconds = 3f;

        [Header("Runtime state")]
        [SerializeField] private bool isConnected = true;
        [SerializeField] private bool hasReading;
        [SerializeField] private bool requiresFreshReading;
        [SerializeField] private string currentDeviceId = string.Empty;
        [SerializeField] private float temperatureC;
        [SerializeField] private float humidity;
        [SerializeField] private float lastReceivedTime = -1f;
        [SerializeField, TextArea(2, 4)]
        private string lastValidJson = string.Empty;
        [SerializeField] private string lastError = string.Empty;

        [SerializeField] private bool hasTemperatureReading;
        [SerializeField] private bool hasHumidityReading;

        [SerializeField] private float lastTemperatureReceivedTime = -1f;
        [SerializeField] private float lastHumidityReceivedTime = -1f;

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

        public float Humidity
        {
            get { return humidity; }
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
            hasTemperatureReading = false;
            hasHumidityReading = false;

            requiresFreshReading = false;

            currentDeviceId =
                string.Empty;

            temperatureC = 0f;
            humidity = 0f;

            lastReceivedTime = -1f;

            lastTemperatureReceivedTime = -1f;
            lastHumidityReceivedTime = -1f;

            lastValidJson =
                string.Empty;

            lastError =
                string.Empty;
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

        public bool HasTemperatureReading
        {
            get { return hasTemperatureReading; }
        }

        public bool HasHumidityReading
        {
            get { return hasHumidityReading; }
        }

        public TelemetryDataStatus GetTemperatureStatus(float nowSeconds)
        {
            if (!isConnected)
            {
                return TelemetryDataStatus.Offline;
            }

            if (!hasTemperatureReading)
            {
                return TelemetryDataStatus.Waiting;
            }

            return GetTemperatureAgeSeconds(nowSeconds) < staleAfterSeconds
                ? TelemetryDataStatus.Live
                : TelemetryDataStatus.Stale;
        }

        public TelemetryDataStatus GetHumidityStatus(float nowSeconds)
        {
            if (!isConnected)
            {
                return TelemetryDataStatus.Offline;
            }

            if (!hasHumidityReading)
            {
                return TelemetryDataStatus.Waiting;
            }

            return GetHumidityAgeSeconds(nowSeconds) < staleAfterSeconds
                ? TelemetryDataStatus.Live
                : TelemetryDataStatus.Stale;
        }

        public float GetTemperatureAgeSeconds(float nowSeconds)
        {
            if (!hasTemperatureReading)
            {
                return 0f;
            }

            return Mathf.Max(
                0f,
                nowSeconds - lastTemperatureReceivedTime);
        }

        public float GetHumidityAgeSeconds(float nowSeconds)
        {
            if (!hasHumidityReading)
            {
                return 0f;
            }

            return Mathf.Max(
                0f,
                nowSeconds - lastHumidityReceivedTime);
        }

        public bool ReceiveJson(string rawJson)
        {
            if (!isConnected)
            {
                return Reject(
                    "Source is offline. Reconnect before sending.");
            }

            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return Reject("JSON is empty.");
            }

            if (!ContainsRequiredKey(rawJson, "deviceId"))
            {
                return Reject(
                    "JSON must contain deviceId.");
            }

            SensorPacket packet;

            try
            {
                packet =
                    JsonUtility.FromJson<SensorPacket>(rawJson);
            }
            catch (Exception exception)
            {
                return Reject(
                    "JSON syntax error: " +
                    exception.Message);
            }

            if (packet == null)
            {
                return Reject(
                    "JSON could not be converted to SensorPacket.");
            }

            if (string.IsNullOrWhiteSpace(packet.deviceId))
            {
                return Reject(
                    "deviceId must not be empty.");
            }

            bool hasTemperature =
                ContainsRequiredKey(
                    rawJson,
                    "temperatureC");

            bool hasHumidity =
                ContainsRequiredKey(
                    rawJson,
                    "humidity");

            // ต้องมีอย่างน้อย 1 ค่า
            if (!hasTemperature && !hasHumidity)
            {
                return Reject(
                    "JSON must contain temperatureC or humidity.");
            }

            // ตรวจ Temperature เฉพาะเมื่อมี Temperature
            if (hasTemperature)
            {
                if (float.IsNaN(packet.temperatureC) ||
                    float.IsInfinity(packet.temperatureC) ||
                    packet.temperatureC < MinimumTemperatureC ||
                    packet.temperatureC > MaximumTemperatureC)
                {
                    return Reject(
                        "temperatureC must be between 15 and 40 °C.");
                }
            }

            // ตรวจ Humidity เฉพาะเมื่อมี Humidity
            if (hasHumidity)
            {
                if (float.IsNaN(packet.humidity) ||
                    float.IsInfinity(packet.humidity) ||
                    packet.humidity < MinimumHumidity ||
                    packet.humidity > MaximumHumidity)
                {
                    return Reject(
                        "humidity must be between 0 and 100 %RH.");
                }
            }

            // ----------------------------------------
            // SAVE DEVICE ID
            // ----------------------------------------

            currentDeviceId =
                packet.deviceId.Trim();

            // ----------------------------------------
            // SAVE TEMPERATURE
            // ----------------------------------------

            if (hasTemperature)
            {
                temperatureC =
                    packet.temperatureC;

                lastTemperatureReceivedTime =
                    Time.unscaledTime;

                hasTemperatureReading =
                    true;
            }

            // ----------------------------------------
            // SAVE HUMIDITY
            // ----------------------------------------

            if (hasHumidity)
            {
                humidity =
                    packet.humidity;

                lastHumidityReceivedTime =
                    Time.unscaledTime;

                hasHumidityReading =
                    true;
            }

            // ----------------------------------------
            // GENERAL STATE
            // ----------------------------------------

            lastReceivedTime =
                Time.unscaledTime;

            lastValidJson =
                rawJson;

            lastError =
                string.Empty;

            hasReading =
                hasTemperatureReading ||
                hasHumidityReading;

            requiresFreshReading =
                false;

            Debug.Log(
                "Telemetry accepted: " +
                currentDeviceId +
                " / TEMP: " +
                (
                    hasTemperature
                        ? temperatureC.ToString("0.0") + " C"
                        : "NO UPDATE"
                ) +
                " / HUMIDITY: " +
                (
                    hasHumidity
                        ? humidity.ToString("0.0") + " %RH"
                        : "NO UPDATE"
                ),
                this);

            return true;
        }

        public void TestTemperatureOnly()
        {
            ReceiveJson(
                "{\"deviceId\":\"GH-01\",\"temperatureC\":25.0}"
            );
        }

        public void TestHumidityOnly()
        {
            ReceiveJson(
                "{\"deviceId\":\"GH-01\",\"humidity\":65.0}"
            );
        }

        public void TestBothValid()
        {
            ReceiveJson(
                "{\"deviceId\":\"GH-01\",\"temperatureC\":25.0,\"humidity\":65.0}"
            );
        }

        public void ToggleTemperature()
        {
            temperatureEnabled = !temperatureEnabled;
        }

        public void ToggleHumidity()
        {
            humidityEnabled = !humidityEnabled;
        }

        public void TemperatureOffline()
        {
            lastTemperatureReceivedTime =
                Time.unscaledTime - staleAfterSeconds - 1f;
        }

        public void HumidityOffline()
        {
            lastHumidityReceivedTime =
                Time.unscaledTime - staleAfterSeconds - 1f;
        }

    }
}
