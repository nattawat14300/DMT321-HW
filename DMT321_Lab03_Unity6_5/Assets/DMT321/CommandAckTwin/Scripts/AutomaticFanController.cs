using UnityEngine;
using DMT321.JsonDataTwin;

namespace DMT321.CommandAckTwin
{
    public enum AutomaticFanZone
    {
        Waiting,
        Cold,
        Hold,
        Hot
    }

    /// <summary>
    /// Automatically controls the fan using the latest LIVE
    /// temperature received by TelemetryReceiver.
    ///
    /// Rule:
    /// >= 30 C              -> FAN ON
    /// > 26 C and < 30 C    -> HOLD
    /// <= 26 C              -> FAN OFF
    /// </summary>
    public class AutomaticFanController : MonoBehaviour
    {
        [Header("Read sensor data from")]
        [SerializeField] private TelemetryReceiver telemetryReceiver;

        [Header("Send actuator command through")]
        [SerializeField] private FanCommandController fanController;

        [Header("Automatic rule")]
        [SerializeField] private bool automaticModeEnabled = true;

        [SerializeField] private float turnFanOnAtC = 30f;
        [SerializeField] private float turnFanOffAtC = 26f;

        [Header("Runtime state")]
        [SerializeField] private AutomaticFanZone currentZone =
            AutomaticFanZone.Waiting;

        [SerializeField] private bool hasEvaluatedReading;

        [SerializeField] private float lastEvaluatedTemperature;

        [SerializeField] private float lastProcessedPacketTime = -1f;

        [SerializeField] private bool hasQueuedTarget;

        [SerializeField] private bool queuedFanOn;

        [SerializeField] private int automaticCommandCount;

        [SerializeField, TextArea(2, 4)]
        private string lastDecision =
            "Waiting for a new LIVE temperature packet.";

        // --------------------------------------------------
        // Public Properties
        // --------------------------------------------------

        public TelemetryReceiver TelemetryReceiver
        {
            get { return telemetryReceiver; }
            set { telemetryReceiver = value; }
        }

        public FanCommandController FanController
        {
            get { return fanController; }
            set { fanController = value; }
        }

        public bool AutomaticModeEnabled
        {
            get { return automaticModeEnabled; }
            set { automaticModeEnabled = value; }
        }

        public float TurnFanOnAtC
        {
            get { return turnFanOnAtC; }
            set { turnFanOnAtC = value; }
        }

        public float TurnFanOffAtC
        {
            get { return turnFanOffAtC; }
            set { turnFanOffAtC = value; }
        }

        public AutomaticFanZone CurrentZone
        {
            get { return currentZone; }
        }

        public bool HasEvaluatedReading
        {
            get { return hasEvaluatedReading; }
        }

        public float LastEvaluatedTemperature
        {
            get { return lastEvaluatedTemperature; }
        }

        public float LastProcessedPacketTime
        {
            get { return lastProcessedPacketTime; }
        }

        public bool HasQueuedTarget
        {
            get { return hasQueuedTarget; }
        }

        public bool QueuedFanOn
        {
            get { return queuedFanOn; }
        }

        public int AutomaticCommandCount
        {
            get { return automaticCommandCount; }
        }

        public string LastDecision
        {
            get { return lastDecision; }
        }

        // --------------------------------------------------
        // Unity
        // --------------------------------------------------

        private void Awake()
        {
            ResetRuntimeState();
        }

        private void Update()
        {
            if (!automaticModeEnabled)
            {
                return;
            }

            if (telemetryReceiver == null)
            {
                return;
            }

            // ยังไม่มีข้อมูล
            if (!telemetryReceiver.HasReading)
            {
                return;
            }

            // ต้องเป็นข้อมูล LIVE เท่านั้น
            if (telemetryReceiver.DataStatus != TelemetryDataStatus.Live)
            {
                return;
            }

            // เวลาที่ TelemetryReceiver ได้รับ packet ล่าสุด
            float packetTime =
                telemetryReceiver.LastReceivedTime;

            // ป้องกันการประมวลผล packet เดิมซ้ำทุก frame
            if (Mathf.Approximately(
                    packetTime,
                    lastProcessedPacketTime))
            {
                return;
            }

            float temperature =
                telemetryReceiver.TemperatureC;

            EvaluateTemperature(
                temperature,
                packetTime);
        }

        private void OnValidate()
        {
            if (turnFanOffAtC >= turnFanOnAtC)
            {
                turnFanOffAtC = turnFanOnAtC - 1f;
            }
        }

        // --------------------------------------------------
        // Automatic Rule
        // --------------------------------------------------

        public void EvaluateTemperature(
            float temperatureC,
            float packetTime)
        {
            hasEvaluatedReading = true;

            lastEvaluatedTemperature = temperatureC;

            lastProcessedPacketTime = packetTime;

            hasQueuedTarget = false;

            // ==================================================
            // HOT
            // Temperature >= 30
            // ==================================================

            if (temperatureC >= turnFanOnAtC)
            {
                currentZone = AutomaticFanZone.Hot;

                lastDecision =
                    "Temperature " +
                    temperatureC.ToString("0.0") +
                    "°C >= " +
                    turnFanOnAtC.ToString("0.0") +
                    "°C.\n" +
                    "Automatic target: FAN ON.";

                SendAutomaticCommand(true);

                return;
            }

            // ==================================================
            // HOLD
            // 26 < Temperature < 30
            // ==================================================

            if (temperatureC > turnFanOffAtC &&
                temperatureC < turnFanOnAtC)
            {
                currentZone = AutomaticFanZone.Hold;

                lastDecision =
                    "Temperature " +
                    temperatureC.ToString("0.0") +
                    "°C is between " +
                    turnFanOffAtC.ToString("0.0") +
                    "°C and " +
                    turnFanOnAtC.ToString("0.0") +
                    "°C.\n" +
                    "HOLD: no new command.";

                Debug.Log(lastDecision, this);

                return;
            }

            // ==================================================
            // COLD
            // Temperature <= 26
            // ==================================================

            currentZone = AutomaticFanZone.Cold;

            lastDecision =
                "Temperature " +
                temperatureC.ToString("0.0") +
                "°C <= " +
                turnFanOffAtC.ToString("0.0") +
                "°C.\n" +
                "Automatic target: FAN OFF.";

            SendAutomaticCommand(false);
        }

        // --------------------------------------------------
        // Send Command
        // --------------------------------------------------

        private void SendAutomaticCommand(bool fanOn)
        {
            if (fanController == null)
            {
                hasQueuedTarget = true;
                queuedFanOn = fanOn;

                lastDecision +=
                    "\nFan Controller is not assigned.";

                Debug.LogWarning(
                    lastDecision,
                    this);

                return;
            }

            // ถ้า Desired state เป็นสถานะเดียวกับที่ต้องการ
            // ไม่ต้องส่ง command ซ้ำ
            if (fanController.DesiredFanOn == fanOn)
            {
                lastDecision +=
                    "\nNo new command needed.";

                Debug.Log(
                    lastDecision,
                    this);

                return;
            }

            // ถ้ามี command กำลังรอ ACK
            if (fanController.IsPending)
            {
                hasQueuedTarget = true;
                queuedFanOn = fanOn;

                lastDecision +=
                    "\nCommand is pending. Target queued.";

                Debug.Log(
                    lastDecision,
                    this);

                return;
            }

            bool sent =
                fanController.SendFanCommand(fanOn);

            if (sent)
            {
                automaticCommandCount++;

                lastDecision +=
                    "\nAutomatic command sent.";

                Debug.Log(
                    lastDecision,
                    this);
            }
            else
            {
                hasQueuedTarget = true;
                queuedFanOn = fanOn;

                lastDecision +=
                    "\nCommand could not be sent. Target queued.";

                Debug.LogWarning(
                    lastDecision,
                    this);
            }
        }

        // --------------------------------------------------
        // Manual Test
        // --------------------------------------------------

        public void TestHot()
        {
            EvaluateTemperature(
                35f,
                Time.unscaledTime);
        }

        public void TestHold()
        {
            EvaluateTemperature(
                28f,
                Time.unscaledTime);
        }

        public void TestCold()
        {
            EvaluateTemperature(
                25f,
                Time.unscaledTime);
        }

        // --------------------------------------------------
        // Reset
        // --------------------------------------------------

        public void ResetAutomaticController()
        {
            ResetRuntimeState();
        }

        private void ResetRuntimeState()
        {
            currentZone =
                AutomaticFanZone.Waiting;

            hasEvaluatedReading = false;

            lastEvaluatedTemperature = 0f;

            lastProcessedPacketTime = -1f;

            hasQueuedTarget = false;

            queuedFanOn = false;

            automaticCommandCount = 0;

            lastDecision =
                "Waiting for a new LIVE temperature packet.";
        }
    }
}