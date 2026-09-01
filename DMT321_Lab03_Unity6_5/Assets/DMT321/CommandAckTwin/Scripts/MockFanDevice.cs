using System;
using UnityEngine;

namespace DMT321.CommandAckTwin
{
    /// <summary>
    /// A deterministic stand-in for a fan, transport, and firmware response.
    /// It can accept, reject, drop the next ACK, or behave as an offline device.
    /// </summary>
    public class MockFanDevice : MonoBehaviour
    {
        [Header("Mock device")]
        [SerializeField] private string deviceId = "GH-01";
        [SerializeField] private FanCommandController ackReceiver;
        [SerializeField, Min(0f)] private float ackDelaySeconds = 1f;
        [SerializeField] private bool startOnline = true;
        [SerializeField] private bool initialFanOn;

        [Header("Runtime state")]
        [SerializeField] private bool isOnline;
        [SerializeField] private bool reportedFanOn;
        [SerializeField] private bool rejectNextCommand;
        [SerializeField] private bool dropNextAck;
        [SerializeField] private bool hasPendingResponse;
        [SerializeField] private float pendingAckTime = -1f;
        [SerializeField, TextArea(2, 4)]
        private string pendingCommandJson = string.Empty;
        [SerializeField] private bool pendingForceReject;
        [SerializeField] private bool pendingDropAck;
        [SerializeField] private bool lastAckWasDropped;
        [SerializeField, TextArea(2, 4)]
        private string lastReceivedCommandJson = string.Empty;
        [SerializeField, TextArea(2, 4)]
        private string lastAckJson = string.Empty;
        [SerializeField] private string lastMessage = "Ready";

        public string DeviceId
        {
            get { return deviceId; }
            set { deviceId = value == null ? string.Empty : value.Trim(); }
        }

        public FanCommandController AckReceiver
        {
            get { return ackReceiver; }
            set { ackReceiver = value; }
        }

        public float AckDelaySeconds
        {
            get { return ackDelaySeconds; }
            set { ackDelaySeconds = Mathf.Max(0f, value); }
        }

        public bool StartOnline
        {
            get { return startOnline; }
            set { startOnline = value; }
        }

        public bool InitialFanOn
        {
            get { return initialFanOn; }
            set { initialFanOn = value; }
        }

        public bool IsOnline
        {
            get { return isOnline; }
        }

        public bool ReportedFanOn
        {
            get { return reportedFanOn; }
        }

        public bool RejectNextArmed
        {
            get { return rejectNextCommand; }
        }

        public bool DropNextAckArmed
        {
            get { return dropNextAck; }
        }

        public bool HasPendingResponse
        {
            get { return hasPendingResponse; }
        }

        public float PendingAckTime
        {
            get { return pendingAckTime; }
        }

        public bool PendingWillDropAck
        {
            get { return hasPendingResponse && pendingDropAck; }
        }

        public bool LastAckWasDropped
        {
            get { return lastAckWasDropped; }
        }

        public string LastReceivedCommandJson
        {
            get { return lastReceivedCommandJson; }
        }

        /// <summary>
        /// The last ACK JSON actually delivered to AckReceiver. It stays empty
        /// when an ACK is dropped or no receiver is assigned.
        /// </summary>
        public string LastAckJson
        {
            get { return lastAckJson; }
        }

        public string LastMessage
        {
            get { return lastMessage; }
        }

        private void Awake()
        {
            ResetMockDevice();
        }

        private void Update()
        {
            ProcessPendingAt(Time.unscaledTime);
        }

        private void OnValidate()
        {
            ackDelaySeconds = Mathf.Max(0f, ackDelaySeconds);
        }

        public bool SendCommandJson(string rawCommandJson)
        {
            return SendCommandJsonAt(
                rawCommandJson,
                Time.unscaledTime);
        }

        /// <summary>
        /// Queues one response using an explicit time for deterministic tests.
        /// Returning true means the online transport delivered the command;
        /// device acceptance is communicated later by ACK.accepted when the
        /// return path is available.
        /// </summary>
        public bool SendCommandJsonAt(
            string rawCommandJson,
            float nowSeconds)
        {
            if (!isOnline)
            {
                lastMessage = "Command not received: device is offline.";
                Debug.LogWarning(lastMessage, this);
                return false;
            }

            if (hasPendingResponse)
            {
                lastMessage = "Mock device is already processing a command.";
                Debug.LogWarning(lastMessage, this);
                return false;
            }

            lastReceivedCommandJson = rawCommandJson ?? string.Empty;
            lastAckJson = string.Empty;
            lastAckWasDropped = false;

            bool shouldDropAck = dropNextAck;
            bool shouldReject = rejectNextCommand;
            dropNextAck = false;
            rejectNextCommand = false;

            pendingCommandJson = lastReceivedCommandJson;
            pendingForceReject = shouldReject;
            pendingDropAck = shouldDropAck;
            pendingAckTime = nowSeconds + ackDelaySeconds;
            hasPendingResponse = true;
            lastMessage = shouldDropAck
                ? "Command received; processing with ACK drop scheduled."
                : shouldReject
                    ? "Command received; rejection ACK scheduled."
                    : "Command received; acceptance ACK scheduled.";

            if (ackDelaySeconds <= 0f)
            {
                ProcessPendingAt(nowSeconds);
            }

            return true;
        }

        /// <summary>
        /// Processes a due command without requiring a Coroutine or real-time
        /// wait. It sends an ACK unless the Drop ACK mode is active. Returns
        /// true only when pending device work is processed.
        /// </summary>
        public bool ProcessPendingAt(float nowSeconds)
        {
            if (!isOnline || !hasPendingResponse ||
                nowSeconds < pendingAckTime)
            {
                return false;
            }

            string commandJson = pendingCommandJson;
            bool forceReject = pendingForceReject;
            bool shouldDropAck = pendingDropAck;
            ClearPendingResponse();

            FanAckPacket ack = CreateAck(commandJson, forceReject);
            string ackJson = JsonUtility.ToJson(ack);

            if (shouldDropAck)
            {
                // The device still validates and applies an accepted command.
                // Only the return path is lost, so the controller will time out
                // while the physical/mock Reported state may already differ.
                lastAckWasDropped = true;
                lastAckJson = string.Empty;
                lastMessage = ack.accepted
                    ? "Fan state applied; ACK was dropped."
                    : "Command rejected; ACK was dropped: " + ack.message;
                Debug.LogWarning(lastMessage, this);
                return true;
            }

            if (ackReceiver == null)
            {
                lastMessage = "ACK created, but ACK Receiver is not assigned.";
                Debug.LogError(lastMessage, this);
                return true;
            }

            // LastAckJson means the last ACK actually delivered to a receiver.
            lastAckJson = ackJson;
            ackReceiver.ReceiveAckJson(lastAckJson);
            Debug.Log("Mock ACK sent: " + lastAckJson, this);
            return true;
        }

        /// <summary>
        /// Unity Button entry point.
        /// </summary>
        public void GoOnline()
        {
            isOnline = true;
            lastMessage = "Device is online. No command was retried.";
        }

        /// <summary>
        /// Unity Button entry point. A response waiting inside the offline
        /// device is lost, so its controller will eventually time out.
        /// </summary>
        public void GoOffline()
        {
            isOnline = false;
            rejectNextCommand = false;
            dropNextAck = false;
            ClearPendingResponse();
            lastMessage = "Device is offline.";
        }

        /// <summary>
        /// Unity Button entry point. Clears one-shot failure modes so the next
        /// delivered command follows the normal accepted path.
        /// </summary>
        public void AcceptNextCommand()
        {
            rejectNextCommand = false;
            dropNextAck = false;
            lastMessage = "Accept Next is armed.";
        }

        /// <summary>
        /// Unity Button entry point. The next delivered command gets a valid
        /// ACK with accepted=false and the unchanged Reported state.
        /// </summary>
        public void RejectNextCommand()
        {
            rejectNextCommand = true;
            dropNextAck = false;
            lastMessage = "Reject Next is armed.";
        }

        /// <summary>
        /// Unity Button entry point. The next delivered command is processed
        /// normally, but its ACK is lost. The mock device may change state
        /// while the controller eventually enters Timeout.
        /// </summary>
        public void DropNextAck()
        {
            dropNextAck = true;
            rejectNextCommand = false;
            lastMessage = "Drop Next ACK is armed.";
        }

        /// <summary>
        /// Restores the configured initial mock state and cancels pending work.
        /// </summary>
        public void ResetMockDevice()
        {
            isOnline = startOnline;
            reportedFanOn = initialFanOn;
            rejectNextCommand = false;
            dropNextAck = false;
            lastReceivedCommandJson = string.Empty;
            lastAckJson = string.Empty;
            lastAckWasDropped = false;
            lastMessage = "Ready";
            ClearPendingResponse();
        }

        private FanAckPacket CreateAck(
            string rawCommandJson,
            bool forceReject)
        {
            FanCommandPacket command = null;
            string validationError = ValidateCommand(
                rawCommandJson,
                out command);

            bool accepted = string.IsNullOrEmpty(validationError) &&
                !forceReject;

            if (accepted)
            {
                reportedFanOn = command.fanOn;
            }

            string message;

            if (!string.IsNullOrEmpty(validationError))
            {
                message = validationError;
            }
            else if (forceReject)
            {
                message = "Command rejected by mock device.";
            }
            else
            {
                message = "Fan state applied.";
            }

            lastMessage = message;

            return new FanAckPacket
            {
                deviceId = deviceId == null
                    ? string.Empty
                    : deviceId.Trim(),
                commandId = command == null || command.commandId == null
                    ? string.Empty
                    : command.commandId.Trim(),
                accepted = accepted,
                reportedFanOn = reportedFanOn,
                message = message
            };
        }

        private string ValidateCommand(
            string rawCommandJson,
            out FanCommandPacket command)
        {
            command = null;

            if (string.IsNullOrWhiteSpace(rawCommandJson))
            {
                return "Command JSON is empty.";
            }

            if (!ContainsRequiredKey(rawCommandJson, "deviceId") ||
                !ContainsRequiredKey(rawCommandJson, "commandId") ||
                !ContainsRequiredKey(rawCommandJson, "fanOn"))
            {
                return "Command must contain deviceId, commandId, " +
                    "and fanOn exactly.";
            }

            try
            {
                command = JsonUtility.FromJson<FanCommandPacket>(
                    rawCommandJson);
            }
            catch (Exception exception)
            {
                return "Command JSON syntax error: " + exception.Message;
            }

            if (command == null)
            {
                return "Command JSON could not be converted to " +
                    "FanCommandPacket.";
            }

            if (string.IsNullOrWhiteSpace(command.deviceId))
            {
                return "Command deviceId must not be empty.";
            }

            if (string.IsNullOrWhiteSpace(command.commandId))
            {
                return "Command commandId must not be empty.";
            }

            if (!string.Equals(
                    command.deviceId.Trim(),
                    deviceId == null ? string.Empty : deviceId.Trim(),
                    StringComparison.Ordinal))
            {
                return "Command targets a different device.";
            }

            return string.Empty;
        }

        private void ClearPendingResponse()
        {
            hasPendingResponse = false;
            pendingAckTime = -1f;
            pendingCommandJson = string.Empty;
            pendingForceReject = false;
            pendingDropAck = false;
        }

        private static bool ContainsRequiredKey(string json, string key)
        {
            return json.IndexOf(
                "\"" + key + "\"",
                StringComparison.Ordinal) >= 0;
        }
    }
}
