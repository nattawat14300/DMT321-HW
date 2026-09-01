using TMPro;
using UnityEngine;

namespace DMT321.CommandAckTwin
{
    /// <summary>
    /// Presents command state in UI and 3D. The fan rotor intentionally reads
    /// ReportedFanOn only; DesiredFanOn never drives the physical twin visual.
    /// </summary>
    public class FanCommandView : MonoBehaviour
    {
        [Header("Read command state from")]
        [SerializeField] private FanCommandController controller;
        [SerializeField] private MockFanDevice mockDevice;

        [Header("Show state in UI")]
        [SerializeField] private TMP_Text deviceConnectionText;
        [SerializeField] private TMP_Text desiredFanText;
        [SerializeField] private TMP_Text reportedFanText;
        [SerializeField] private TMP_Text commandStatusText;
        [SerializeField] private TMP_Text commandIdText;
        [SerializeField] private TMP_Text lastMessageText;
        [SerializeField] private TMP_Text commandJsonText;
        [SerializeField] private TMP_Text ackJsonText;

        [Header("Show Reported state in 3D")]
        [SerializeField] private Transform fanRotor;
        [SerializeField] private Renderer fanStatusRenderer;
        [SerializeField] private Vector3 localRotationAxis = Vector3.forward;
        [SerializeField, Min(0f)]
        private float rotationDegreesPerSecond = 360f;

        [Header("Command-status colors")]
        [SerializeField] private Color idleColor =
            new Color(0.55f, 0.65f, 0.7f);
        [SerializeField] private Color pendingColor =
            new Color(1f, 0.72f, 0.12f);
        [SerializeField] private Color acceptedColor =
            new Color(0.22f, 0.82f, 0.42f);
        [SerializeField] private Color rejectedColor =
            new Color(1f, 0.28f, 0.18f);
        [SerializeField] private Color timeoutColor =
            new Color(1f, 0.48f, 0.12f);
        [SerializeField] private Color offlineColor =
            new Color(0.48f, 0.46f, 0.55f);
        [SerializeField] private Color desiredColor =
            new Color(0.18f, 0.75f, 1f);

        private MaterialPropertyBlock colorBlock;
        private float lastRefreshTime = -1f;

        public FanCommandController Controller
        {
            get { return controller; }
            set { controller = value; }
        }

        public MockFanDevice MockDevice
        {
            get { return mockDevice; }
            set { mockDevice = value; }
        }

        public Transform FanRotor
        {
            get { return fanRotor; }
            set { fanRotor = value; }
        }

        public Renderer FanStatusRenderer
        {
            get { return fanStatusRenderer; }
            set { fanStatusRenderer = value; }
        }

        public float RotationDegreesPerSecond
        {
            get { return rotationDegreesPerSecond; }
            set { rotationDegreesPerSecond = Mathf.Max(0f, value); }
        }

        private void Update()
        {
            RefreshViewAt(Time.unscaledTime, Time.unscaledDeltaTime);
        }

        private void OnValidate()
        {
            rotationDegreesPerSecond = Mathf.Max(
                0f,
                rotationDegreesPerSecond);
        }

        private void OnDisable()
        {
            lastRefreshTime = -1f;
        }

        public void RefreshView()
        {
            RefreshViewAt(Time.unscaledTime, Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Deterministic refresh based on successive explicit timestamps.
        /// The first call uses zero elapsed time.
        /// </summary>
        public void RefreshViewAt(float nowSeconds)
        {
            float deltaSeconds = lastRefreshTime < 0f
                ? 0f
                : Mathf.Max(0f, nowSeconds - lastRefreshTime);

            RefreshViewAt(nowSeconds, deltaSeconds);
        }

        /// <summary>
        /// Deterministic refresh with an explicit animation delta.
        /// </summary>
        public void RefreshViewAt(float nowSeconds, float deltaSeconds)
        {
            lastRefreshTime = nowSeconds;

            if (controller == null)
            {
                ShowMissingController();
                return;
            }

            bool deviceOnline = mockDevice != null && mockDevice.IsOnline;
            Color statusColor = GetStatusColor(controller.Status);

            if (!deviceOnline)
            {
                statusColor = offlineColor;
            }

            if (deviceConnectionText != null)
            {
                deviceConnectionText.text = mockDevice == null
                    ? "DEVICE  NOT WIRED"
                    : "DEVICE  " + (deviceOnline ? "ONLINE" : "OFFLINE");
                deviceConnectionText.color = deviceOnline
                    ? acceptedColor
                    : offlineColor;
            }

            if (desiredFanText != null)
            {
                desiredFanText.text = "DESIRED  " +
                    (controller.DesiredFanOn ? "ON" : "OFF");
                desiredFanText.color = desiredColor;
            }

            if (reportedFanText != null)
            {
                reportedFanText.text = controller.HasReportedState
                    ? "REPORTED  " +
                        (controller.ReportedFanOn ? "ON" : "OFF")
                    : "REPORTED  —";
                reportedFanText.color = controller.ReportedFanOn
                    ? acceptedColor
                    : idleColor;
            }

            if (commandStatusText != null)
            {
                commandStatusText.text = "COMMAND  " +
                    controller.Status.ToString().ToUpperInvariant();
                commandStatusText.color = GetStatusColor(
                    controller.Status);
            }

            if (commandIdText != null)
            {
                commandIdText.text = "COMMAND ID  " +
                    (string.IsNullOrEmpty(controller.ActiveCommandId)
                        ? "—"
                        : controller.ActiveCommandId);
            }

            if (lastMessageText != null)
            {
                lastMessageText.text = "MESSAGE  " +
                    (string.IsNullOrEmpty(controller.LastMessage)
                        ? "—"
                        : controller.LastMessage);
            }

            if (commandJsonText != null)
            {
                commandJsonText.text = "LAST COMMAND JSON\n" +
                    (string.IsNullOrEmpty(controller.LastCommandJson)
                        ? "—"
                        : controller.LastCommandJson);
            }

            if (ackJsonText != null)
            {
                ackJsonText.text = "LAST ACK JSON\n" +
                    (string.IsNullOrEmpty(controller.LastAckJson)
                        ? "—"
                        : controller.LastAckJson);
            }

            SetRendererColor(fanStatusRenderer, statusColor);

            // The key Digital Twin rule for this lab: only Reported state can
            // animate the fan. Pending Desired state never moves the rotor.
            if (fanRotor != null && controller.HasReportedState &&
                controller.ReportedFanOn)
            {
                Vector3 axis = localRotationAxis.sqrMagnitude > 0f
                    ? localRotationAxis.normalized
                    : Vector3.forward;
                float rotationAmount = rotationDegreesPerSecond *
                    Mathf.Max(0f, deltaSeconds);
                fanRotor.Rotate(axis, rotationAmount, Space.Self);
            }
        }

        private void ShowMissingController()
        {
            if (deviceConnectionText != null)
            {
                deviceConnectionText.text = "DEVICE  NOT WIRED";
                deviceConnectionText.color = rejectedColor;
            }

            if (desiredFanText != null)
            {
                desiredFanText.text = "DESIRED  —";
                desiredFanText.color = idleColor;
            }

            if (reportedFanText != null)
            {
                reportedFanText.text = "REPORTED  —";
                reportedFanText.color = idleColor;
            }

            if (commandStatusText != null)
            {
                commandStatusText.text = "COMMAND  NOT WIRED";
                commandStatusText.color = rejectedColor;
            }

            if (commandIdText != null)
            {
                commandIdText.text = "COMMAND ID  —";
            }

            if (lastMessageText != null)
            {
                lastMessageText.text = "MESSAGE  —";
            }

            if (commandJsonText != null)
            {
                commandJsonText.text = "LAST COMMAND JSON\n—";
            }

            if (ackJsonText != null)
            {
                ackJsonText.text = "LAST ACK JSON\n—";
            }

            SetRendererColor(fanStatusRenderer, rejectedColor);
        }

        private Color GetStatusColor(FanCommandStatus commandStatus)
        {
            switch (commandStatus)
            {
                case FanCommandStatus.Pending:
                    return pendingColor;
                case FanCommandStatus.Accepted:
                    return acceptedColor;
                case FanCommandStatus.Rejected:
                    return rejectedColor;
                case FanCommandStatus.Timeout:
                    return timeoutColor;
                case FanCommandStatus.Offline:
                    return offlineColor;
                default:
                    return idleColor;
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
