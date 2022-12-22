using Iot.Device.Max31856;
using System;
using System.Device.Gpio;
using System.Device.Spi;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

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

    public class FakeHeater : IHeater
    {
        public void Disable()
        {
        }

        public void Enable()
        {
        }
    }

    public class GpioHeater : IHeater
    {
        private const int HeatPin = 21;
        private GpioController gpio;

        public GpioHeater(GpioController gpio)
        {
            this.gpio = gpio;
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

    public partial class SpiThermocouple : ITemperatureSensor
    {
        private MAX31856 therm;

        public SpiThermocouple(GpioController gpio)
        {
            var settings = new SpiConnectionSettings(0, 1);
            settings.Mode = SpiMode.Mode1;
            settings.ClockFrequency = MAX31856.ClockFrequency;
            settings.DataFlow = DataFlow.MsbFirst;

            var spi = SpiDevice.Create(settings);
            this.therm = new MAX31856(gpio, spi, chipSelectPin: 0, MAX31856.ThermocoupleType.K, averageSamples: 4);
        }

        public float GetTemperature()
        {
            return (float)therm.GetTemperature().DegreesFahrenheit;
        }
    }
}
