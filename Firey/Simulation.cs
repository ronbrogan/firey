﻿using Firey.Data;

namespace Firey
{
    public class SimulationService : BackgroundService
    {
        private readonly SteppedTimeSource time;
        private readonly ModelKiln kiln;
        private readonly KilnControlService controller;

        public SimulationService(SteppedTimeSource time, ModelKiln kiln, KilnControlService controller)
        {
            this.time = time;
            this.kiln = kiln;
            this.controller = controller;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            long lastEmit = 0;
            DateTimeOffset lastTick = DateTimeOffset.MinValue;

            await Task.Yield();

            while (!stoppingToken.IsCancellationRequested)
            {
                for(var i = 0; i < 10; i++)
                {
                    kiln.StepSeconds(0.1f);
                    time.StepSeconds(0.1f);

                    if ((time.Now - lastTick).TotalSeconds > time.HeatPeriod)
                    {
                        lastTick = time.Now;
                        controller.Tick();
                    }
                }

                await Task.Delay(1);
            }
        }
    }

    public class SteppedTimeSource : ITimeSource
    {
        public float HeatPeriod => 0.2f;

        public float UpdatePeriod => 1f;

        private DateTimeOffset now;
        public DateTimeOffset Now => now;

        public void Step(TimeSpan delta)
        {
            now += delta;
        }

        public void StepSeconds(float seconds)
        {
            now += TimeSpan.FromSeconds(seconds);
        }
    }

    public class Simulation
    {
        private readonly KilnSchedule schedule;

        public Simulation(KilnSchedule schedule)
        {
            this.schedule = schedule;
        }

        public IList<SimulationTick> Run()
        {
            var time = new SteppedTimeSource();
            var kiln = new ModelKiln();
            
            var controller = new KilnControlService(kiln, kiln, time, new InMemoryKilnRunRepo());

            var ticks = new List<SimulationTick>(schedule.calculatedTimeMinutes * 60);

            if (!controller.TryStartSchedule(schedule)) throw new Exception("what");

            long lastEmit = 0;
            DateTimeOffset lastTick = DateTimeOffset.MinValue;

            for (var i = 0; i < schedule.calculatedTimeMinutes * 60 * 10; i++)
            {
                kiln.StepSeconds(0.1f);
                time.StepSeconds(0.1f);

                if ((time.Now - lastTick).TotalSeconds > time.HeatPeriod)
                {
                    lastTick = time.Now;
                    controller.Tick();
                }

                if (time.Now.Ticks - lastEmit > 10000000)
                {
                    ticks.Add(new SimulationTick()
                    {
                        Heating = kiln.IsHeating,
                        Temperature = controller.Info.MeasuredTemp,
                        Target = controller.Info.ScheduleTarget ?? 0f,
                        Time = controller.SecondsIntoSchedule
                    });

                    lastEmit = time.Now.Ticks;
                }
            }


            return ticks;
        }
    }

    public class ModelKiln : IHeater, ITemperatureSensor
    {
        private readonly float heatPerSecond;
        private readonly float heatLossConstant;

        private float elementEmission = 0;
        private const int coldElementEmissionDelay = 40; // seconds


        public bool IsHeating { get; private set; }
        public float Temperature { get; private set; }

        public ModelKiln(float heatPerSecond = 2.6f, float heatLossConstant = 800f)
        {
            this.heatPerSecond = heatPerSecond;
            this.heatLossConstant = heatLossConstant;
        }

        public void Enable()
        {
            IsHeating = true;
        }

        public void Disable()
        {
            IsHeating = false;
        }

        public float GetTemperature()
        {
            return Temperature;
        }

        private bool wasHeating;
        public void StepSeconds(float seconds)
        {
            const float elementCooldownFactor = 2;

            if(wasHeating)
            {
                elementEmission += seconds;
                elementEmission = Math.Min(coldElementEmissionDelay, elementEmission);

                
            }
            else
            {
                elementEmission -= seconds * elementCooldownFactor;
                elementEmission = Math.Max(0, elementEmission);
            }

            var heatGain = (elementEmission / coldElementEmissionDelay) * heatPerSecond * seconds;

            const float ambient = 72f;

            var deltaT = Temperature - ambient;

            var heatLoss = (deltaT / heatLossConstant) * seconds;

            Temperature += heatGain - heatLoss;
            wasHeating = IsHeating;
        }
    }

    public struct SimulationTick
    {
        public float Time;
        public float Temperature;
        public float Target;
        public bool Heating;
    }
}
