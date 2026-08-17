using UnityEngine;

namespace DMT321.JsonDataTwin
{
    /// <summary>
    /// Transport adapter. It turns a TextAsset into raw JSON and
    /// forwards that string to TelemetryReceiver.
    /// </summary>
    public class TextAssetTelemetrySource : MonoBehaviour
    {
        [Header("Send JSON to")]
        [SerializeField] private TelemetryReceiver receiver;

        [Header("JSON packets")]
        [SerializeField] private TextAsset coldJson;
        [SerializeField] private TextAsset normalJson;
        [SerializeField] private TextAsset hotJson;

        public TelemetryReceiver Receiver
        {
            get { return receiver; }
        }

        public TextAsset ColdJson
        {
            get { return coldJson; }
        }

        public TextAsset NormalJson
        {
            get { return normalJson; }
        }

        public TextAsset HotJson
        {
            get { return hotJson; }
        }

        public void LoadColdJson()
        {
            SendFile(coldJson);
        }

        public void LoadNormalJson()
        {
            SendFile(normalJson);
        }

        public void LoadHotJson()
        {
            SendFile(hotJson);
        }

        public void Disconnect()
        {
            if (receiver != null)
            {
                receiver.Disconnect();
            }
        }

        public void Reconnect()
        {
            if (receiver != null)
            {
                receiver.Reconnect();
            }
        }

        public bool SendFile(TextAsset jsonFile)
        {
            if (receiver == null)
            {
                Debug.LogError(
                    "TextAssetTelemetrySource needs a TelemetryReceiver.",
                    this);
                return false;
            }

            if (jsonFile == null)
            {
                Debug.LogError(
                    "A JSON TextAsset slot is empty in the Inspector.",
                    this);
                return false;
            }

            return receiver.ReceiveJson(jsonFile.text);
        }
    }
}
