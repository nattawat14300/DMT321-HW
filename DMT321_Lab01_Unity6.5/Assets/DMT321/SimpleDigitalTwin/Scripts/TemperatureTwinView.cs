using TMPro;
using UnityEngine;

namespace DMT321.SimpleDigitalTwin
{
    /// <summary>
    /// Reads one sensor value and shows it in UI and on a 3D object.
    /// </summary>
    public class TemperatureTwinView : MonoBehaviour
    {
        [Header("Read data from")]
        [SerializeField] private MockTemperatureSensor sensor;

        [Header("Show data in UI")]
        [SerializeField] private TMP_Text temperatureText;
        [SerializeField] private TMP_Text statusText;

        [Header("Show data in 3D")]
        [SerializeField] private Renderer deviceRenderer;

        [Header("Warning rule")]
        [SerializeField] private float warningAtC = 30f;
        [SerializeField] private Color normalColor =
            new Color(0.2f, 0.8f, 0.35f);
        [SerializeField] private Color warningColor =
            new Color(1f, 0.3f, 0.2f);

            
        [Header("Temperature Indicator")]
        [Tooltip("ใส่ Tranform ของวัตถุที่มี Pivot อยู่ที่ฐานด้านล่าง")]
        [SerializeField] private Transform IndicatorPivotTransForm;
        [SerializeField] private float minHeight = 0.20f;
        [SerializeField] private float maxHeight = 1.50f;
        private const float displayMinC = 15f;
        private const float displayMaxC = 40f;
            

        public MockTemperatureSensor Sensor
        {
            get { return sensor; }
        }

        private void Update()
        {
            RefreshView();
        }

        /// <summary>
        /// Update calls this every frame.
        /// </summary>
        public void RefreshView()
        {
            if (sensor == null)
            {
                return;
            }

            float value = sensor.TemperatureC;

            UpdateText(value);
            UpdateTwinObject(value);
            UpdateIndicatorScale(value);
        }

        private void UpdateText(float value)
        {
            if (temperatureText != null)
            {
                temperatureText.text = value.ToString("0.0") + " °C";
            }

            if (statusText != null)
            {
                bool isWarning = value >= warningAtC;
                statusText.text = isWarning ? "WARNING" : "NORMAL";
                statusText.color = isWarning
                    ? warningColor
                    : normalColor;
            }
        }

        private void UpdateTwinObject(float value)
        {
            if (deviceRenderer != null)
            {
                deviceRenderer.material.color = value >= warningAtC
                    ? warningColor
                    : normalColor;
            }
        }

        private void UpdateIndicatorScale(float value)
        {
            if (IndicatorPivotTransForm == null)
            {
                return;
            }

            float normalizedValue = Mathf.InverseLerp(displayMinC, displayMaxC, value);
            float height = Mathf.Lerp(minHeight, maxHeight, normalizedValue);

            Vector3 currentScale = IndicatorPivotTransForm.localScale;
            IndicatorPivotTransForm.localScale = new Vector3(currentScale.x, height, currentScale.z);
        } 
    }
}
