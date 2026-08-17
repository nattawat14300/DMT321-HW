using UnityEngine;

namespace DMT321.SimpleDigitalTwin
{
    /// <summary>
    /// Stores one temperature value. 
    /// Arduino data can call SetTemperature later.
    /// </summary>
    public class MockTemperatureSensor : MonoBehaviour
    {
        [SerializeField, Range(15f, 40f)]
        private float temperatureC = 15f;

        public float TemperatureC
        {
            get { return temperatureC; }
        }

        /// <summary>
        /// Connect this public method to Slider > On Value Changed.
        /// </summary>
        public void SetTemperature(float newTemperature)
        {
            temperatureC = Mathf.Clamp(newTemperature, 15f, 40f);
        }
    }
}
