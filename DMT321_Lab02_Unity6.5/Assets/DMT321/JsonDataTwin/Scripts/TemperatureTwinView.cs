using TMPro;
using UnityEngine;

namespace DMT321.JsonDataTwin
{
    public class TemperatureTwinView : MonoBehaviour
    {
        [Header("Read Twin state from")]
        [SerializeField] private TelemetryReceiver receiver;

        [Header("Show state in UI")]
        [SerializeField] private TMP_Text deviceIdText;
        [SerializeField] private TMP_Text temperatureText;
        [SerializeField] private TMP_Text temperatureStatusText;
        [SerializeField] private TMP_Text dataStatusText;
        [SerializeField] private TMP_Text lastUpdatedText;
        [SerializeField] private TMP_Text humidityText;
        [SerializeField] private TMP_Text humidityStatusText;
        [SerializeField] private TMP_Text overallDataHealthText;

        [Header("Show state in 3D")]
        [SerializeField] private Renderer temperatureIndicatorRenderer;
        [SerializeField] private Renderer dataStatusBeaconRenderer;

        [Header("Temperature rules")]
        [SerializeField] private float humidityWarningAt = 70f;
        [SerializeField] private float coldBelowC = 20f;
        [SerializeField] private float warningAtC = 30f;

        [SerializeField]
        private Color coldColor =
            new Color(0.18f, 0.55f, 1f);

        [SerializeField]
        private Color normalColor =
            new Color(0.22f, 0.82f, 0.42f);

        [SerializeField]
        private Color warningColor =
            new Color(1f, 0.28f, 0.18f);

        [Header("Data-quality colors")]
        [SerializeField]
        private Color waitingColor =
            new Color(0.72f, 0.9f, 1f);

        [SerializeField]
        private Color liveColor =
            new Color(0.22f, 0.82f, 0.42f);

        [SerializeField]
        private Color staleColor =
            new Color(1f, 0.72f, 0.12f);

        [SerializeField]
        private Color offlineColor =
            new Color(0.48f, 0.46f, 0.55f);

        [SerializeField]
        private Color lastKnownObjectColor =
            new Color(0.76f, 0.7f, 0.86f);

        private MaterialPropertyBlock colorBlock;

        public TelemetryReceiver Receiver
        {
            get { return receiver; }
        }

        private void Update()
        {
            RefreshViewAt(Time.unscaledTime);
        }

        public void RefreshView()
        {
            RefreshViewAt(Time.unscaledTime);
        }

        public void RefreshViewAt(float nowSeconds)
        {
            if (receiver == null)
            {
                ShowMissingReceiver();
                return;
            }

            // ----------------------------------------
            // GET SEPARATE STATUS
            // ----------------------------------------

            TelemetryDataStatus temperatureStatus =
                receiver.GetTemperatureStatus(nowSeconds);

            TelemetryDataStatus humidityStatus =
                receiver.GetHumidityStatus(nowSeconds);

            bool temperatureLive =
                temperatureStatus == TelemetryDataStatus.Live;

            bool humidityLive =
                humidityStatus == TelemetryDataStatus.Live;

            // ----------------------------------------
            // OVERALL DATA HEALTH
            // ----------------------------------------

            UpdateOverallDataHealth(
                temperatureStatus,
                humidityStatus);

            // ----------------------------------------
            // DATA STATUS
            // ----------------------------------------

            if (temperatureLive && humidityLive)
            {
                UpdateDataStatus(
                    TelemetryDataStatus.Live,
                    liveColor);
            }
            else if (temperatureLive || humidityLive)
            {
                UpdateDataStatus(
                    TelemetryDataStatus.Stale,
                    staleColor);
            }
            else
            {
                UpdateDataStatus(
                    TelemetryDataStatus.Stale,
                    staleColor);
            }

            if (!receiver.HasReading)
            {
                ShowWaitingForFirstReading();
                return;
            }

            Color beaconColor;

            if (temperatureLive && humidityLive)
            {
                beaconColor = liveColor;
            }
            else if (temperatureLive || humidityLive)
            {
                beaconColor = staleColor;
            }
            else
            {
                beaconColor = warningColor;
            }

            SetRendererColor(
                dataStatusBeaconRenderer,
               temperatureLive || humidityLive
                    ? beaconColor
                    : lastKnownObjectColor);

            // ----------------------------------------
            // VALUES
            // ----------------------------------------

            float value =
                receiver.TemperatureC;

            float humidity =
                receiver.Humidity;

            // ----------------------------------------
            // TEMPERATURE
            // ----------------------------------------

            string condition =
                GetTemperatureCondition(value);

            Color conditionColor =
                GetTemperatureColor(value);

            // Beacon เปลี่ยนสีตามสถานะอุณหภูมิ
            SetRendererColor(
                dataStatusBeaconRenderer,
                temperatureLive
                    ? conditionColor
                    : lastKnownObjectColor);

            // ----------------------------------------
            // HUMIDITY
            // ----------------------------------------

            string humidityCondition =
                GetHumidityCondition(humidity);

            Color humidityColor =
                GetHumidityColor(humidity);

            // ----------------------------------------
            // DEVICE ID
            // ----------------------------------------

            if (deviceIdText != null)
            {
                deviceIdText.text =
                    "DEVICE  " +
                    receiver.CurrentDeviceId;
            }

            // ----------------------------------------
            // TEMPERATURE VALUE
            // ----------------------------------------

            if (temperatureText != null)
            {
                if (receiver.HasTemperatureReading)
                {
                    temperatureText.text =
                        value.ToString("0.0") +
                        " °C";
                }
                else
                {
                    temperatureText.text =
                        "--.- °C";
                }
            }

            // ----------------------------------------
            // TEMPERATURE STATUS
            // ----------------------------------------

            if (temperatureStatusText != null)
            {
                temperatureStatusText.text =
                    "TEMPERATURE  " +
                    condition +
                    (
                        temperatureLive
                            ? "  ·  LIVE"
                            : "  ·  LAST KNOWN"
                    );

                temperatureStatusText.color =
                    temperatureLive
                        ? conditionColor
                        : lastKnownObjectColor;
            }

            // ----------------------------------------
            // HUMIDITY VALUE
            // ----------------------------------------

            if (humidityText != null)
            {
                if (receiver.HasHumidityReading)
                {
                    humidityText.text =
                        humidity.ToString("0.0") +
                        " %RH";
                }
                else
                {
                    humidityText.text =
                        "--.- %RH";
                }
            }

            // ----------------------------------------
            // HUMIDITY STATUS
            // ----------------------------------------

            if (humidityStatusText != null)
            {
                humidityStatusText.text =
                    "HUMIDITY  " +
                    humidityCondition +
                    (
                        humidityLive
                            ? "  ·  LIVE"
                            : "  ·  LAST KNOWN"
                    );

                humidityStatusText.color =
                    humidityLive
                        ? humidityColor
                        : lastKnownObjectColor;
            }

            // ----------------------------------------
            // LAST UPDATED
            // ----------------------------------------

            if (lastUpdatedText != null)
            {
                float age =
                    receiver.GetDataAgeSeconds(
                        nowSeconds);

                lastUpdatedText.text =
                    "LAST UPDATED  " +
                    age.ToString("0.0") +
                    " s AGO";
            }

            // ----------------------------------------
            // 3D OBJECT
            // ----------------------------------------

            SetRendererColor(
                temperatureIndicatorRenderer,
                temperatureLive
                    ? conditionColor
                    : lastKnownObjectColor);
        }

        // ==================================================
        // DATA STATUS
        // ==================================================

        private void UpdateDataStatus(
            TelemetryDataStatus status,
            Color statusColor)
        {
            if (dataStatusText == null)
            {
                return;
            }

            dataStatusText.text =
                "DATA  " +
                status.ToString().ToUpperInvariant();

            dataStatusText.color =
                statusColor;
        }

        // ==================================================
        // WAITING
        // ==================================================

        private void ShowWaitingForFirstReading()
        {
            if (deviceIdText != null)
            {
                deviceIdText.text =
                    "DEVICE  —";
            }

            if (temperatureText != null)
            {
                temperatureText.text =
                    "--.- °C";
            }

            if (humidityText != null)
            {
                humidityText.text =
                    "--.- %RH";
            }

            if (temperatureStatusText != null)
            {
                temperatureStatusText.text =
                    "TEMPERATURE  NO VALID READING";

                temperatureStatusText.color =
                    waitingColor;
            }

            if (humidityStatusText != null)
            {
                humidityStatusText.text =
                    "HUMIDITY  NO VALID READING";

                humidityStatusText.color =
                    waitingColor;
            }

            if (lastUpdatedText != null)
            {
                lastUpdatedText.text =
                    "LAST UPDATED  —";
            }

            SetRendererColor(
                temperatureIndicatorRenderer,
                lastKnownObjectColor);
        }

        // ==================================================
        // MISSING RECEIVER
        // ==================================================

        private void ShowMissingReceiver()
        {
            if (dataStatusText != null)
            {
                dataStatusText.text =
                    "DATA  NOT WIRED";

                dataStatusText.color =
                    warningColor;
            }

            if (overallDataHealthText != null)
            {
                overallDataHealthText.text =
                    "● NO RELIABLE DATA";

                overallDataHealthText.color =
                    warningColor;
            }
        }

        // ==================================================
        // TEMPERATURE
        // ==================================================

        private string GetTemperatureCondition(
            float value)
        {
            if (value < coldBelowC)
            {
                return "COLD";
            }

            return value >= warningAtC
                ? "WARNING"
                : "NORMAL";
        }

        private Color GetTemperatureColor(
            float value)
        {
            if (value < coldBelowC)
            {
                return coldColor;
            }

            return value >= warningAtC
                ? warningColor
                : normalColor;
        }

        // ==================================================
        // HUMIDITY
        // ==================================================

        private string GetHumidityCondition(
            float value)
        {
            return value >= humidityWarningAt
                ? "WARNING"
                : "NORMAL";
        }

        private Color GetHumidityColor(
            float value)
        {
            return value >= humidityWarningAt
                ? warningColor
                : normalColor;
        }

        // ==================================================
        // OVERALL DATA HEALTH
        // ==================================================

        private OverallDataHealth GetOverallDataHealth(
            TelemetryDataStatus temperatureStatus,
            TelemetryDataStatus humidityStatus)
        {
            bool temperatureLive =
                temperatureStatus ==
                TelemetryDataStatus.Live;

            bool humidityLive =
                humidityStatus ==
                TelemetryDataStatus.Live;

            // ทั้งคู่ LIVE
            if (temperatureLive &&
                humidityLive)
            {
                return OverallDataHealth.DataOK;
            }

            // มีอย่างใดอย่างหนึ่ง LIVE
            if (temperatureLive ||
                humidityLive)
            {
                return OverallDataHealth.PartialData;
            }

            // ไม่มีตัวไหน LIVE
            return OverallDataHealth.NoReliableData;
        }

        private void UpdateOverallDataHealth(
            TelemetryDataStatus temperatureStatus,
            TelemetryDataStatus humidityStatus)
        {
            if (overallDataHealthText == null)
            {
                return;
            }

            OverallDataHealth health =
                GetOverallDataHealth(
                    temperatureStatus,
                    humidityStatus);

            switch (health)
            {
                case OverallDataHealth.DataOK:

                    overallDataHealthText.text =
                        "● DATA OK";

                    overallDataHealthText.color =
                        liveColor;

                    break;

                case OverallDataHealth.PartialData:

                    overallDataHealthText.text =
                        "● PARTIAL DATA";

                    overallDataHealthText.color =
                        staleColor;

                    break;

                case OverallDataHealth.NoReliableData:

                    overallDataHealthText.text =
                        "● NO RELIABLE DATA";

                    overallDataHealthText.color =
                        warningColor;

                    break;
            }
        }

        // ==================================================
        // RENDERER COLOR
        // ==================================================

        private void SetRendererColor(
     Renderer target,
     Color color)
        {
            if (target == null)
            {
                return;
            }

            if (colorBlock == null)
            {
                colorBlock = new MaterialPropertyBlock();
            }

            target.GetPropertyBlock(colorBlock);

            if (target.sharedMaterial != null)
            {
                if (target.sharedMaterial.HasProperty("_BaseColor"))
                {
                    colorBlock.SetColor("_BaseColor", color);
                }
                else if (target.sharedMaterial.HasProperty("_Color"))
                {
                    colorBlock.SetColor("_Color", color);
                }
            }

            target.SetPropertyBlock(colorBlock);
        }

        // ==================================================
        // ENUM
        // ==================================================

        public enum OverallDataHealth
        {
            DataOK,
            PartialData,
            NoReliableData
        }
    }
}