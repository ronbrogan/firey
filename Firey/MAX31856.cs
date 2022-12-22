using System.Device.Gpio;
using System.Device.Spi;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;
using UnitsNet;

namespace Firey
{
    public class MAX31856
    {
        public enum ThermocoupleType : byte
        {
            B = 0,
            E = 1,
            J = 2,
            K = 3,
            N = 4,
            R = 5,
            S = 6,
            T = 7
        }

        [Flags]
        public enum Config0 : byte
        {
            Default = 0,
            Filter50Hz = 1 << 0,
            ClearFaults = 1 << 1,
            ManualFaultClear = 1 << 2,
            DisableColdJunction = 1 << 3,
            OpenCircuit0 = 1 << 4,
            OpenCircuit1 = 1 << 5,
            OneShotConvert = 1 << 6,
            ContinuousConversion = 1 << 7
        }

        [Flags]
        public enum Config1 : byte
        {
            Default = 0,
            TC0 = 1 << 0,
            TC1 = 1 << 1,
            TC2 = 1 << 2,
            TC3 = 1 << 3,
            AVG0 = 1 << 4,
            AVG1 = 1 << 5,
            AVG2 = 1 << 6,
            RESERVED = 1 << 7,

            TypeB = ThermocoupleType.B,
            TypeE = ThermocoupleType.E,
            TypeJ = ThermocoupleType.J,
            TypeK = ThermocoupleType.K,
            TypeN = ThermocoupleType.N,
            TypeR = ThermocoupleType.R,
            TypeS = ThermocoupleType.S,
            TypeT = ThermocoupleType.T,

            AverageShift = 4,
            Average2 = 1 << AverageShift,
            Average4 = 2 << AverageShift,
            Average8 = 3 << AverageShift,
            Average16 = 4 << AverageShift
        }

        /// <summary>
        /// The Fault Mask Register allows the user to mask faults from causing the FAULT output from asserting. Masked faults
        /// will still result in fault bits being set in the Fault Status register(0Fh). Note that the FAULT output is never asserted by
        /// thermocouple and cold-junction out-of-range status.
        /// </summary>
        [Flags]
        public enum FaultMask : byte
        {
            OpenCircuit = 1 << 0,
            OverUnderVoltage = 1 << 1,
            ThermocoupleLow = 1 << 2,
            ThermocoupleHigh = 1 << 3,
            ColdJunctionLow = 1 << 4,
            ColdJunctionHigh = 1 << 5,
            RESERVED6 = 1 << 6,
            RESERVED7 = 1 << 7
        }

        // Read addresses
        private enum Register : byte
        {
            Config0 = 0x00, // CR0 configuration
            Config1 = 0x01, // CR1 configuration
            FaultMask = 0x02, // Fault Mask Register
            ColdJunctionHighThreshold = 0x03, // Cold Junction High Fault Threshold
            ColdJunctionLowThreshold = 0x04, // Cold Junction Low Fault Threshold
            TempHighFaultThresholdMSB = 0x05, // Linearized Temperature High Fault Threshold MSB
            TempHighFaultThresholdLSB = 0x06, // Linearized Temperature High Fault Threshold LSB
            TempLowFaultThresholdMSB = 0x07, // Linearized Temperature Low Fault Threshold MSB
            TempLowFaultThresholdLSB = 0x08, // Linearized Temperature Low Fault Threshold LSB
            ColdJunctionTempOffset = 0x09, // Cold-Junction Temperature Offset Register
            ColdJunctionTempMSB = 0x0A, // Cold-Junction Temperature Register, MSB
            ColdJunctionTempLSB = 0x0B, // Cold-Junction Temperature Register, LSB
            TempByte2 = 0x0C, // Linearized TC Temperature, Byte 2
            TempByte1 = 0x0D, // Linearized TC Temperature, Byte 1
            TempByte0 = 0x0E, // Linearized TC Temperature, Byte 0
            Faults = 0x0F, // Fault status register
        }

        public enum Faults : byte
        {
            OpenCircuit = 1 << 0,
            OverUnderVoltage = 1 << 1,
            ThermocoupleLow = 1 << 2,
            ThermocoupleHigh = 1 << 3,
            ColdJunctionLow = 1 << 4,
            ColdJunctionHigh = 1 << 5,
            ThermocoupleOutOfRange = 1 << 6,
            ColdJunctionOutOfRange = 1 << 7
        }

        private const double MAX31856_CONST_THERM_LSB = 0.0078125;
        private const int MAX31856_CONST_THERM_BITS = 19;
        private const double MAX31856_CONST_CJ_LSB = 0.015625;
        private const int MAX31856_CONST_CJ_BITS = 14;

        public const int ClockFrequency = 5000000;

        private readonly GpioController gpio;
        private readonly SpiDevice spiDevice;
        private readonly int chipSelectPin;

        public MAX31856(GpioController gpio, SpiDevice spiDevice, int chipSelectPin, ThermocoupleType thermocoupleType = ThermocoupleType.K, int averageSamples = 0)
        {
            this.gpio = gpio;
            this.spiDevice = spiDevice;
            this.chipSelectPin = chipSelectPin;
#if DEBUG
            Console.WriteLine($"Opening GPIO pin {chipSelectPin} for chip select usage");
#endif
            gpio.OpenPin(chipSelectPin, PinMode.Output);


            WriteRegister(Register.ColdJunctionTempOffset, 0);

            WriteRegister(Register.Config0, (byte)Config0.ContinuousConversion);
            WriteRegister(Register.Config1, (byte)((Config1)thermocoupleType | (Config1)(averageSamples << (byte)Config1.AverageShift)));

            WriteRegister(Register.FaultMask, 0x3F);

            WriteRegister(Register.ColdJunctionHighThreshold, 0x7F);
            WriteRegister(Register.ColdJunctionLowThreshold, 0xC0);
            WriteRegister(Register.TempHighFaultThresholdMSB, 0x7F);
            WriteRegister(Register.TempHighFaultThresholdLSB, 0xFF);
            WriteRegister(Register.TempLowFaultThresholdMSB, 0x80);
            WriteRegister(Register.TempLowFaultThresholdLSB, 0x00);

            var config0 = ReadRegister<Config0>(Register.Config0);
            var config1 = ReadRegister<Config1>(Register.Config1);
            var faultMask = ReadRegister<FaultMask>(Register.FaultMask);
            var faults = ReadRegister<Faults>(Register.Faults);

#if DEBUG
            Console.WriteLine("Got Config0: " + config0.ToString());
            Console.WriteLine("Got Config1: " + config1.ToString());
            Console.WriteLine("Got FaultMask: " + faultMask.ToString());
            Console.WriteLine("Got faults: " + faults.ToString());
#endif
        }

        /// <summary>
        /// Reads the temperature from the MAX31856
        /// </summary>
        /// <returns>Temperature (in celsius)</returns>
        public Temperature GetTemperature()
        {
            //The temperature is read from three different registers, and the fourth is the fault register
            var bytes = ReadRegister(Register.TempByte2, 4);

            var val_bytes = ((bytes[0] & 0x7F) << 16) + (bytes[1] << 8) + bytes[2];
            // ends with 5 empty bits at the end
            val_bytes = val_bytes >> 5;

            double temperature = val_bytes;

            //Check if positive or negative
            if ((bytes[0] & 0x80) == 1)
            {
                temperature -= Math.Pow(2, (MAX31856_CONST_THERM_BITS - 1));
            }

            temperature *= MAX31856_CONST_THERM_LSB;

            try
            {
                AssertFaults((Faults)bytes[3]);
            }
            catch (Exception ex)
            {
                throw new DeviceFaultException(ex.Message + ", " + temperature.ToString() + " 0x:" + string.Join(" ", bytes.ToArray().Select(b => b.ToString("x"))));
            }

            return Temperature.FromDegreesCelsius(temperature);
        }

        /// <summary>
        /// Gets the cold junction temperature
        /// </summary>
        /// <returns>Cold Junction Temperature (in celsius)</returns>
        public Temperature GetColdJunctionTemperature()
        {
            // The internal temperature is read from two different registers (MAX31856_REG_READ_CJTH and MAX31856_REG_READ_CJTL)
            var bytes = ReadRegister(Register.ColdJunctionTempMSB, 2);

            // Convert the bytes into double
            // MSB (bytes[0]) contains the sign of the temperature (so we want to remove that portion, hence the & 0x7F)
            var val_bytes = ((bytes[0] & 0x7F) << 8) + bytes[1];
            // LSB (bytes[1]) contains 2 dead bits at the end
            val_bytes = val_bytes >> 2;

            double temperature = val_bytes;

            // Check if positive or negative
            if ((bytes[0] & 0x80) == 1)
            {
                temperature -= Math.Pow(2, (MAX31856_CONST_CJ_BITS - 1));
            }
            temperature *= MAX31856_CONST_CJ_LSB;

            try
            {
                AssertFaults();
            }
            catch (Exception ex)
            {
                throw new DeviceFaultException(ex.Message + ", " + temperature.ToString());
            }

            return Temperature.FromDegreesCelsius(temperature);
        }

        /// <summary>
        /// Determines the fault given the fault byte (if any exists)
        /// </summary>
        /// <param name="fault">The fault byte to be interpreted</param>
        public void AssertFaults(Faults? fault = null)
        {
            // If no byte was specified, then get the fault byte
            if (fault == null)
            {
                fault = ReadRegister<Faults>(Register.Faults);
            }

            var faults = new StringBuilder();

            if (fault.Value.HasFlag(Faults.ColdJunctionOutOfRange))
            {
                faults.AppendLine("Cold Junction Out-of-Range");
            }
            if (fault.Value.HasFlag(Faults.ThermocoupleOutOfRange))
            {
                faults.AppendLine("Thermocouple Out-of-Range");
            }
            if (fault.Value.HasFlag(Faults.ColdJunctionHigh))
            {
                faults.AppendLine("Cold Junction High Fault");
            }
            if (fault.Value.HasFlag(Faults.ColdJunctionLow))
            {
                faults.AppendLine("Cold Junction Low Fault");
            }
            if (fault.Value.HasFlag(Faults.ThermocoupleHigh))
            {
                faults.AppendLine("Thermocouple Temperature High Fault");
            }
            if (fault.Value.HasFlag(Faults.ThermocoupleLow))
            {
                faults.AppendLine("Thermocouple Temperature Low Fault");
            }
            if (fault.Value.HasFlag(Faults.OverUnderVoltage))
            {
                faults.AppendLine("Overvoltage or Undervoltage Input Fault");
            }
            if (fault.Value.HasFlag(Faults.OpenCircuit))
            {
                faults.AppendLine("Thermocouple Open-Circuit Fault");
            }

            if (faults.Length > 0)
            {
                throw new DeviceFaultException(faults.ToString());
            }
        }

        private const int MaxReadWriteBytes = 32;
        private byte[] input = new byte[MaxReadWriteBytes];
        private byte[] output = new byte[MaxReadWriteBytes];

        private ref struct InputOutput
        {
            public Span<byte> Input;
            public Span<byte> Output;

            public InputOutput(Span<byte> input, Span<byte> output)
            {
                Input = input;
                Output = output;
            }
        }

        private InputOutput GetRegisterBufs(int count)
        {
            Debug.Assert(count <= MaxReadWriteBytes, "Too many bytes to read per transaction");

            Array.Clear(this.input);
            Array.Clear(this.output);
            var input = this.input.AsSpan().Slice(0, count + 1);
            var output = this.output.AsSpan().Slice(0, count + 1);
            return new InputOutput(input, output);
        }

        private byte ReadRegister(Register address)
        {
            return ReadRegister<byte>(address);
        }

        private T ReadRegister<T>(Register address)
        {
            return (T)(object)ReadRegister(address, 1)[0];
        }

        private ReadOnlySpan<byte> ReadRegister(Register address, int registersToRead)
        {
            if (registersToRead < 1)
            {
                throw new Exception("At least one byte must be specified to read in");
            }

            var bufs = this.GetRegisterBufs(registersToRead);

            bufs.Input[0] = (byte)address;

            gpio.Write(this.chipSelectPin, PinValue.Low);
            spiDevice.TransferFullDuplex(bufs.Input, bufs.Output);
            gpio.Write(this.chipSelectPin, PinValue.High);

            return bufs.Output.Slice(1, registersToRead);
        }

        private void WriteRegister(Register address, byte value)
        {
            gpio.Write(this.chipSelectPin, PinValue.Low);
            spiDevice.Write(stackalloc byte[] { (byte)(0x80 | (byte)address), value });
            gpio.Write(this.chipSelectPin, PinValue.High);
        }

        public class DeviceFaultException : Exception
        {
            public DeviceFaultException()
            {
            }

            public DeviceFaultException(string? message) : base(message)
            {
            }

            public DeviceFaultException(string? message, Exception? innerException) : base(message, innerException)
            {
            }

            protected DeviceFaultException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }
    }
}
