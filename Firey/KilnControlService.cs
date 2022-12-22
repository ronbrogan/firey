using Firey.Data;
using Iot.Device.Max31856;
using System.Device.Gpio;
using System.Device.Spi;
using System.Runtime.CompilerServices;

namespace Firey
{
    public interface ITimeSource
    {
        float HeatPeriod { get; }
        float UpdatePeriod { get; }

        DateTimeOffset Now { get; }
    }

    public class DefaultTimeSource : ITimeSource
    {
        public const float heatPeriodSeconds = 0.2f;
        public const float updatePeriodSeconds = 1f;

        public float HeatPeriod => heatPeriodSeconds;

        public float UpdatePeriod => updatePeriodSeconds;

        public DateTimeOffset Now => DateTimeOffset.UtcNow;
    }

    public struct KilnInfo
    {
        public float MeasuredTemp { get; internal set; }
        public float? SecondsElapsed { get; internal set; }
        public float? ScheduleTarget { get; internal set; }
        public bool Heating { get; internal set; }
        public KilnStatus Status { get; internal set; }
    }

    public enum KilnStatus
    {
        Ready,
        Starting,
        Running,
        Cooldown
    }

    public struct KilnMeasurement
    {
        public float Temperature;
        public float Seconds;
    }

    public class KilnState
    {
        public KilnStatus Status { get; set; }
        public KilnSchedule? Schedule { get; set; }
        public KilnMeasurement[] Measurements { get; set; }
    }

    public class KilnControlService : BackgroundService
    {
        private float cooldownEnd = 100f;

        private float kp = 25f;
        private float ki = 800f;
        private float kd = 20;

        private float output;

        // actual sum of the heat in the current period
        private float periodAmount;


        private PidController pid;

        private KilnInfo info;
        public KilnInfo Info => info;

        public event Action<KilnInfo> OnUpdate;
        public event Action<KilnSchedule?> OnScheduleChange;

        public KilnStatus Status { get; private set; }
        public KilnSchedule? Schedule { get; private set; }
        public KilnRun? StoredRun { get; private set; }

        private DateTimeOffset scheduleStartedAt;
        private readonly IHeater heater;
        private readonly ITemperatureSensor therm;
        private readonly ITimeSource time;
        private readonly IKilnRunRepostory runRepo;

        public float SecondsIntoSchedule
        {
            get
            {
                if (this.Schedule == null)
                    return 0;

                var now = time.Now - this.scheduleStartedAt;
                return (float)now.TotalSeconds;
            }
        }


        public KilnControlService(IHeater heater, ITemperatureSensor therm, ITimeSource time, IKilnRunRepostory runRepo)
        {
            this.pid = new PidController(kp, ki, kd);
            this.Status = KilnStatus.Ready;

            this.heater = heater;
            this.therm = therm;
            this.time = time;
            this.runRepo = runRepo;
        }

        /// <summary>
        /// Sets the controller to follow the given schedule and starts running it
        /// </summary>
        /// <param name="schedule"></param>
        /// <returns></returns>
        public bool TryStartSchedule(KilnSchedule schedule)
        {
            // TODO, more schedule validation?
            if (schedule == null || schedule.ramps.Length == 0)
                return false;

            if (this.Status != KilnStatus.Ready)
            {
                return false;
            }

            schedule.ramps = schedule.ramps.OrderBy(r => r.order).ToArray();

            this.Schedule = schedule;
            this.scheduleStartedAt = time.Now;
            this.StoredRun = KilnRun.ForCommencingSchedule(schedule);
            this.runRepo.SaveRun(this.StoredRun);

            this.Status = KilnStatus.Starting;
            this.lastUpdate = time.Now;
            this.lastSave = time.Now;

            this.OnScheduleChange?.Invoke(this.Schedule);

            this.Tick();

            return true;
        }

        public void StopSchedule()
        {
            if (this.Status == KilnStatus.Ready)
                return;

            this.info.Heating = false;
            this.heater.Disable();
            this.Schedule = null;
            this.StoredRun = null;
            this.Status = KilnStatus.Ready;
            this.OnScheduleChange?.Invoke(this.Schedule);
        }

        public KilnInfo[] GetRunTimeseries()
        {
            if (this.StoredRun == null)
                return Array.Empty<KilnInfo>();

            var lastSec = 0;
            return this.StoredRun.Timeseries.Where(e => 
            {
                if(e.TimeIntoSchedule > lastSec)
                {
                    lastSec++;
                    return true;
                }

                return false;
            }).Select(e => new KilnInfo 
            { 
                Heating = e.HeatEnabled,
                MeasuredTemp = e.MeasuredTemperature,
                ScheduleTarget = e.SetpointTemperature,
                SecondsElapsed = e.TimeIntoSchedule,
                Status = KilnStatus.Running
            }).ToArray();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Tick();

                await Task.Delay((int)(time.HeatPeriod * 1000f));
            }
        }

        private DateTimeOffset lastSave;
        private DateTimeOffset lastUpdate;
        public void Tick()
        {
            this.info.Status = this.Status;
            this.info.MeasuredTemp = this.therm.GetTemperature();
            this.info.SecondsElapsed = SecondsIntoSchedule;
            this.info.ScheduleTarget = this.GetScheduleTarget((this.info.SecondsElapsed / 60f) ?? -1);
            var currentTime = time.Now;

            if (IsRunnable())
            {
                var deltaT = (currentTime - lastUpdate).TotalMilliseconds / 1000f;

                if (deltaT > time.UpdatePeriod)
                {
                    lastUpdate = currentTime;
                    this.Update((float)deltaT);
                }

                // toggle heating depending on current output level
                this.EvaluateOutput();
            }

            if (this.StoredRun != null && this.Schedule != null)
            {
                this.StoredRun.AddSample(this.info);
                // Save run every minute
                if ((currentTime - lastSave).TotalMinutes > 1)
                {
                    this.runRepo.SaveRun(this.StoredRun);
                    this.lastSave = time.Now;
                }
            }

            this.OnUpdate?.Invoke(this.info);
        }

        // Returns the desired temp for the schedule, or null if we're before/after
        private float? GetScheduleTarget(float minutesIntoSchedule)
        {
            if (this.Schedule == null)
                return null;

            var minutesElapsed = minutesIntoSchedule;

            var rampStart = this.Schedule.defaultStartTemp;

            foreach(var ramp in this.Schedule.ramps)
            {
                if(minutesElapsed < ramp.calculatedTimeMinutes)
                {
                    // this is the linear ramp
                    var amount = minutesElapsed / (float)ramp.calculatedTimeMinutes;
                    return Lerp(rampStart, ramp.targetTemp, amount);
                }
                else if(minutesElapsed < ramp.calculatedTimeWithRampMinutes)
                {
                    // this is the hold
                    return ramp.targetTemp;
                }
                else
                {
                    // this ramp has already passed
                    // consume the minutesElapsed, set the next ramp's start
                    minutesElapsed -= ramp.calculatedTimeWithRampMinutes;
                    rampStart = ramp.targetTemp;
                }
            }

            return null;
        }

        private static float Lerp(float start, float end, float amount)
        {
            return start + amount * (end - start);
        }

        // Manage state transitions
        private bool IsRunnable()
        {
            switch (this.Status)
            {
                case KilnStatus.Ready:
                    return false;
                case KilnStatus.Starting:
                    this.Status = KilnStatus.Running;
                    return true;
                case KilnStatus.Running:
                    if (this.info.ScheduleTarget == null)
                    {
                        this.Status = KilnStatus.Cooldown;
                        this.info.Heating = false;
                        this.heater.Disable();
                        return false;
                    }
                    return true;
                case KilnStatus.Cooldown:
                    if (this.Info.MeasuredTemp < cooldownEnd)
                    {
                        this.Schedule = null;
                        this.OnScheduleChange?.Invoke(this.Schedule);
                        this.Status = KilnStatus.Ready;
                    }
                    return false;
                default:
                    return false;
            }
        }

        private void Update(float deltaT)
        {
            var target = this.info.ScheduleTarget;
            var current = this.info.MeasuredTemp;

            if (target == null)
                return;

            this.output = pid.Update(target.Value, current, deltaT);

            // reset period amount to ensure evaluation will turn on appropriately
            this.periodAmount = 0;
        }

        private void EvaluateOutput()
        {
            var desiredPeriodOutput = (this.output / 100f) * time.UpdatePeriod;

            if (periodAmount < desiredPeriodOutput)
            {
                this.info.Heating = true;
                this.heater.Enable();
                this.periodAmount += time.HeatPeriod;
            }
            else
            {
                this.info.Heating = false;
                this.heater.Disable();
            }
        }
    }
}
