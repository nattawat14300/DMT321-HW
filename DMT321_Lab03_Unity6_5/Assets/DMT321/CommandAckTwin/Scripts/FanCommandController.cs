using System;
using UnityEngine;

namespace DMT321.CommandAckTwin
{
    public enum FanCommandStatus
    {
        Idle,
        Pending,
        Accepted,
        Rejected,
        Timeout,
        Offline
    }

    /// <summary>
    /// Owns the Desired and Reported fan states.
    /// </summary>
    public class FanCommandController : MonoBehaviour
    {
        [Header("Command target")]
        [SerializeField] private string deviceId = "GH-01";
        [SerializeField] private MockFanDevice mockDevice;

        [Header("Command timing")]
        [SerializeField, Min(0.1f)]
        private float commandTimeoutSeconds = 3f;

        [Header("Runtime state")]
        [SerializeField] private bool desiredFanOn;
        [SerializeField] private bool reportedFanOn;
        [SerializeField] private bool hasReportedState = true;
        [SerializeField] private bool isPending;
        [SerializeField] private FanCommandStatus status =
            FanCommandStatus.Idle;
        [SerializeField] private int commandSequence;
        [SerializeField] private string activeCommandId = string.Empty;
        [SerializeField] private float lastCommandSentTime = -1f;
        [SerializeField, TextArea(2, 4)]
        private string lastCommandJson = string.Empty;
        [SerializeField, TextArea(2, 4)]
        private string lastAckJson = string.Empty;
        [SerializeField] private string lastMessage = "Ready";
        [SerializeField] private string lastError = string.Empty;

        public string DeviceId
        {
            get { return deviceId; }
            set { deviceId = value == null ? string.Empty : value.Trim(); }
        }

        public MockFanDevice MockDevice
        {
            get { return mockDevice; }
            set { mockDevice = value; }
        }

        public float CommandTimeoutSeconds
        {
            get { return commandTimeoutSeconds; }
            set { commandTimeoutSeconds = Mathf.Max(0.1f, value); }
        }

        public bool DesiredFanOn
        {
            get { return desiredFanOn; }
        }

        public bool ReportedFanOn
        {
            get { return reportedFanOn; }
        }

        public bool HasReportedState
        {
            get { return hasReportedState; }
        }

        public bool IsPending
        {
            get { return isPending; }
        }

        public FanCommandStatus Status
        {
            get { return status; }
        }

        public int CommandSequence
        {
            get { return commandSequence; }
        }

        public string ActiveCommandId
        {
            get { return activeCommandId; }
        }

        public float LastCommandSentTime
        {
            get { return lastCommandSentTime; }
        }

        public string LastCommandJson
        {
            get { return lastCommandJson; }
        }

        public string LastAckJson
        {
            get { return lastAckJson; }
        }

        public string LastMessage
        {
            get { return lastMessage; }
        }

        public string LastError
        {
            get { return lastError; }
        }

        private void Awake()
        {
            ResetRuntimeState(false);
        }

        private void Start()
        {
            // Every Awake has completed before Start. This lets the controller
            // copy the mock device's configured initial state safely.
            if (mockDevice != null && commandSequence == 0 &&
                status == FanCommandStatus.Idle)
            {
                desiredFanOn = mockDevice.ReportedFanOn;
                reportedFanOn = mockDevice.ReportedFanOn;
                hasReportedState = true;
            }
        }

        private void Update()
        {
            TickAt(Time.unscaledTime);
        }

        private void OnValidate()
        {
            commandTimeoutSeconds = Mathf.Max(
                0.1f,
                commandTimeoutSeconds);
        }

        /// <summary>
        /// Unity Button entry point.
        /// </summary>
        public void SendFanOn()
        {
            SendFanCommand(true);
        }

        /// <summary>
        /// Unity Button entry point.
        /// </summary>
        public void SendFanOff()
        {
            SendFanCommand(false);
        }

        public bool SendFanCommand(bool fanOn)
        {
            return SendFanCommandAt(fanOn, Time.unscaledTime);
        }

        /// <summary>
        /// Deterministic version used by tests and demonstrations.
        /// Desired changes immediately; Reported changes only after a valid,
        /// matching ACK arrives.
        /// </summary>
        public bool SendFanCommandAt(bool fanOn, float nowSeconds)
        {
            if (isPending)
            {
                lastError = "A command is already pending.";
                lastMessage = "Wait for the current ACK or timeout.";
                Debug.LogWarning(lastError, this);
                return false;
            }

            desiredFanOn = fanOn;

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                status = FanCommandStatus.Rejected;
                lastError = "Controller deviceId must not be empty.";
                lastMessage = "Command was not created.";
                Debug.LogWarning(lastError, this);
                return false;
            }

            commandSequence++;
            activeCommandId = "CMD-" + commandSequence.ToString("000");

            FanCommandPacket packet = new FanCommandPacket
            {
                deviceId = deviceId.Trim(),
                commandId = activeCommandId,
                fanOn = fanOn
            };

            lastCommandJson = JsonUtility.ToJson(packet);
            lastAckJson = string.Empty;
            lastCommandSentTime = nowSeconds;
            lastError = string.Empty;
            lastMessage = "Command sent. Waiting for ACK.";
            isPending = true;
            status = FanCommandStatus.Pending;

            bool delivered = mockDevice != null &&
                mockDevice.SendCommandJsonAt(lastCommandJson, nowSeconds);

            if (!delivered)
            {
                isPending = false;
                status = FanCommandStatus.Offline;
                lastError = mockDevice == null
                    ? "MockFanDevice is not assigned."
                    : "Device is offline or unavailable.";
                lastMessage = "Command was not delivered.";
                Debug.LogWarning(lastError, this);
                return false;
            }

            Debug.Log("Command sent: " + lastCommandJson, this);
            return true;
        }

        /// <summary>
        /// The one ACK entry point for a mock, Serial, HTTP, or MQTT adapter.
        /// Invalid, late, or mismatched ACKs never overwrite Reported state.
        /// </summary>
        public bool ReceiveAckJson(string rawAckJson)
        {
            lastAckJson = rawAckJson ?? string.Empty;

            if (string.IsNullOrWhiteSpace(rawAckJson))
            {
                return RejectAck("ACK JSON is empty.");
            }

            if (!ContainsRequiredKey(rawAckJson, "deviceId") ||
                !ContainsRequiredKey(rawAckJson, "commandId") ||
                !ContainsRequiredKey(rawAckJson, "accepted") ||
                !ContainsRequiredKey(rawAckJson, "reportedFanOn") ||
                !ContainsRequiredKey(rawAckJson, "message"))
            {
                return RejectAck(
                    "ACK must contain deviceId, commandId, accepted, " +
                    "reportedFanOn, and message exactly.");
            }

            FanAckPacket ack;

            try
            {
                ack = JsonUtility.FromJson<FanAckPacket>(rawAckJson);
            }
            catch (Exception exception)
            {
                return RejectAck(
                    "ACK JSON syntax error: " + exception.Message);
            }

            if (ack == null)
            {
                return RejectAck(
                    "ACK JSON could not be converted to FanAckPacket.");
            }

            if (string.IsNullOrWhiteSpace(ack.deviceId))
            {
                return RejectAck("ACK deviceId must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(ack.commandId))
            {
                return RejectAck("ACK commandId must not be empty.");
            }

            if (!string.Equals(
                    ack.deviceId.Trim(),
                    deviceId.Trim(),
                    StringComparison.Ordinal))
            {
                return RejectAck(
                    "ACK deviceId does not match this controller.");
            }

            if (!isPending)
            {
                return RejectAck(
                    "ACK is late or no command is currently pending.");
            }

            if (!string.Equals(
                    ack.commandId.Trim(),
                    activeCommandId,
                    StringComparison.Ordinal))
            {
                return RejectAck(
                    "ACK commandId does not match the pending command.");
            }

            reportedFanOn = ack.reportedFanOn;
            hasReportedState = true;
            isPending = false;
            lastMessage = string.IsNullOrWhiteSpace(ack.message)
                ? (ack.accepted
                    ? "Command accepted by device."
                    : "Command rejected by device.")
                : ack.message.Trim();

            if (ack.accepted)
            {
                status = FanCommandStatus.Accepted;
                lastError = string.Empty;
                Debug.Log("ACK accepted: " + rawAckJson, this);
                return true;
            }

            status = FanCommandStatus.Rejected;
            lastError = lastMessage;
            Debug.LogWarning("ACK rejected: " + rawAckJson, this);
            return true;
        }

        /// <summary>
        /// Advances the timeout state without relying on wall-clock waiting.
        /// Returns true only on the frame/call that enters Timeout.
        /// </summary>
        public bool TickAt(float nowSeconds)
        {
            if (!isPending)
            {
                return false;
            }

            if (GetPendingAgeSeconds(nowSeconds) < commandTimeoutSeconds)
            {
                return false;
            }

            isPending = false;
            status = FanCommandStatus.Timeout;
            lastError = "No matching ACK arrived before timeout.";
            lastMessage = "Command timed out; Reported state was retained.";
            Debug.LogWarning(lastError, this);
            return true;
        }

        public float GetPendingAgeSeconds(float nowSeconds)
        {
            if (lastCommandSentTime < 0f)
            {
                return 0f;
            }

            return Mathf.Max(0f, nowSeconds - lastCommandSentTime);
        }

        /// <summary>
        /// Unity Button entry point. Restores a deterministic lab baseline.
        /// </summary>
        public void ResetLab()
        {
            if (mockDevice != null)
            {
                mockDevice.ResetMockDevice();
            }

            ResetRuntimeState(true);
        }

        private void ResetRuntimeState(bool copyMockState)
        {
            bool hasInitialReported = copyMockState && mockDevice != null;
            bool initialReported = hasInitialReported
                ? mockDevice.ReportedFanOn
                : false;

            desiredFanOn = initialReported;
            reportedFanOn = initialReported;
            hasReportedState = hasInitialReported;
            isPending = false;
            status = FanCommandStatus.Idle;
            commandSequence = 0;
            activeCommandId = string.Empty;
            lastCommandSentTime = -1f;
            lastCommandJson = string.Empty;
            lastAckJson = string.Empty;
            lastMessage = "Ready";
            lastError = string.Empty;
        }

        private static bool ContainsRequiredKey(string json, string key)
        {
            return json.IndexOf(
                "\"" + key + "\"",
                StringComparison.Ordinal) >= 0;
        }

        private bool RejectAck(string message)
        {
            lastError = message;
            lastMessage = "ACK ignored: " + message;
            Debug.LogWarning(lastMessage, this);
            return false;
        }
    }
}
