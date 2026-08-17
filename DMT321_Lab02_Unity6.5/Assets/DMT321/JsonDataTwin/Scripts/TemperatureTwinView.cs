using TMPro;
using UnityEngine;

namespace DMT321.JsonDataTwin
{
    /// <summary>
    /// Reads validated Twin state and presents it in UI and 3D.
    /// It does not know whether the JSON came from a file, Serial, or Cloud.
    /// </summary>
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

        [Header("Show state in 3D")]
        [SerializeField] private Renderer temperatureIndicatorRenderer;
        [SerializeField] private Renderer dataStatusBeaconRenderer;

        [Header("Temperature rules")]
        [SerializeField] private float coldBelowC = 20f;
        [SerializeField] private float warningAtC = 30f;
        [SerializeField] private Color coldColor =
            new Color(0.18f, 0.55f, 1f);
        [SerializeField] private Color normalColor =
            new Color(0.22f, 0.82f, 0.42f);
        [SerializeField] private Color warningColor =
            new Color(1f, 0.28f, 0.18f);

        [Header("Data-quality colors")]
        [SerializeField] private Color waitingColor =
            new Color(0.72f, 0.9f, 1f);
        [SerializeField] private Color liveColor =
            new Color(0.22f, 0.82f, 0.42f);
        [SerializeField] private Color staleColor =
            new Color(1f, 0.72f, 0.12f);
        [SerializeField] private Color offlineColor =
            new Color(0.48f, 0.46f, 0.55f);
        [SerializeField] private Color lastKnownObjectColor =
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

            TelemetryDataStatus dataStatus =
                receiver.GetDataStatus(nowSeconds);
            Color dataColor = GetDataStatusColor(dataStatus);

            UpdateDataStatus(dataStatus, dataColor);
            SetRendererColor(dataStatusBeaconRenderer, dataColor);

            if (!receiver.HasReading)
            {
                ShowWaitingForFirstReading();
                return;
            }

            float value = receiver.TemperatureC;
            bool isLive = dataStatus == TelemetryDataStatus.Live;
            string condition = GetTemperatureCondition(value);
            Color conditionColor = GetTemperatureColor(value);

            if (deviceIdText != null)
            {
                deviceIdText.text = "DEVICE  " + receiver.CurrentDeviceId;
            }

            if (temperatureText != null)
            {
                temperatureText.text = value.ToString("0.0") + " °C";
            }

            if (temperatureStatusText != null)
            {
                temperatureStatusText.text = "TEMPERATURE  " + condition +
                    (isLive ? string.Empty : "  ·  LAST KNOWN");
                temperatureStatusText.color = conditionColor;
            }

            if (lastUpdatedText != null)
            {
                float age = receiver.GetDataAgeSeconds(nowSeconds);
                lastUpdatedText.text =
                    "LAST UPDATED  " + age.ToString("0.0") + " s AGO" +
                    (isLive ? string.Empty : "  ·  LAST KNOWN VALUE");
            }

            SetRendererColor(
                temperatureIndicatorRenderer,
                isLive ? conditionColor : lastKnownObjectColor);
        }

        private void UpdateDataStatus(
            TelemetryDataStatus status,
            Color statusColor)
        {
            if (dataStatusText == null)
            {
                return;
            }

            dataStatusText.text = "DATA  " + status.ToString().ToUpperInvariant();
            dataStatusText.color = statusColor;
        }

        private void ShowWaitingForFirstReading()
        {
            if (deviceIdText != null)
            {
                deviceIdText.text = "DEVICE  —";
            }

            if (temperatureText != null)
            {
                temperatureText.text = "--.- °C";
            }

            if (temperatureStatusText != null)
            {
                temperatureStatusText.text = "TEMPERATURE  NO VALID READING";
                temperatureStatusText.color = waitingColor;
            }

            if (lastUpdatedText != null)
            {
                lastUpdatedText.text = "LAST UPDATED  —";
            }

            SetRendererColor(
                temperatureIndicatorRenderer,
                lastKnownObjectColor);
        }

        private void ShowMissingReceiver()
        {
            if (dataStatusText != null)
            {
                dataStatusText.text = "DATA  NOT WIRED";
                dataStatusText.color = warningColor;
            }
        }

        private string GetTemperatureCondition(float value)
        {
            if (value < coldBelowC)
            {
                return "COLD";
            }

            return value >= warningAtC ? "WARNING" : "NORMAL";
        }

        private Color GetTemperatureColor(float value)
        {
            if (value < coldBelowC)
            {
                return coldColor;
            }

            return value >= warningAtC ? warningColor : normalColor;
        }

        private Color GetDataStatusColor(TelemetryDataStatus status)
        {
            switch (status)
            {
                case TelemetryDataStatus.Live:
                    return liveColor;
                case TelemetryDataStatus.Stale:
                    return staleColor;
                case TelemetryDataStatus.Offline:
                    return offlineColor;
                default:
                    return waitingColor;
            }
        }

        private void SetRendererColor(Renderer target, Color color)
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
            colorBlock.SetColor("_Color", color);
            target.SetPropertyBlock(colorBlock);
        }
    }
}
