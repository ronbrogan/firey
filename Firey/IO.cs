using Iot.Device.Max31856;
using System.Device.Gpio;
using System.Device.Spi;

namespace Firey
{
    public interface IHeater
    {
        void Enable();
        void Disable();
    }

    public interface ITemperatureSensor
    {
        float GetTemperature();
    }

    public class GpioHeater : IHeater
    {

        private const int HeatPin = 23;
        private GpioController gpio;

        public GpioHeater()
        {
            this.gpio = new GpioController();
            this.gpio.OpenPin(HeatPin, PinMode.Output, PinValue.Low);
        }

        public void Disable()
        {
            this.gpio.Write(HeatPin, PinValue.Low);
        }

        public void Enable()
        {
            this.gpio.Write(HeatPin, PinValue.High);
        }
    }

    public class SpiThermocouple : ITemperatureSensor
    {
        private Max31856 therm;

        public SpiThermocouple()
        {
            var settings = new SpiConnectionSettings(0, 0);
            var spi = SpiDevice.Create(settings);
            this.therm = new Max31856(spi, ThermocoupleType.K);
        }

        public float GetTemperature()
        {
            return (float)this.therm.GetTemperature().As(UnitsNet.Units.TemperatureUnit.DegreeFahrenheit);
        }
    }
}
