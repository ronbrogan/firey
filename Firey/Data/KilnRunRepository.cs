using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Firey.Data
{
    public interface IKilnRunRepostory
    {
        List<(string, DateTimeOffset)> GetAllRunInfo();
        KilnRun? GetRun(string schedule, DateTimeOffset startedAt);
        void SaveRun(KilnRun run);
    }

    public class InMemoryKilnRunRepo : IKilnRunRepostory
    {
        private Dictionary<(string, DateTimeOffset), KilnRun> runs = new();

        public List<(string, DateTimeOffset)> GetAllRunInfo()
        {
            return runs.Keys.ToList();
        }

        public KilnRun? GetRun(string schedule, DateTimeOffset startedAt)
        {
            runs.TryGetValue((schedule, startedAt), out var run);
            return run;
        }

        public void SaveRun(KilnRun run)
        {
            runs[(run.ScheduleId, run.RunStartedAt)] = run;
        }
    }

    public class KilnRun
    {
        public Guid Id { get; } = Guid.NewGuid();

        public required string ScheduleName { get; set; } 

        public required string ScheduleId { get; set; }

        // TODO: embed schedule data here?

        public required DateTimeOffset RunStartedAt { get; set; }

        public int TimeseriesVersion => 1;

        [JsonIgnore]
        public List<KilnStateEntryV1> Timeseries { get; set; }


        public static KilnRun ForCommencingSchedule(KilnSchedule schedule)
        {
            return new KilnRun
            {
                ScheduleId = schedule.id.ToString(),
                ScheduleName = schedule.name,
                RunStartedAt = DateTimeOffset.UtcNow,
                Timeseries = new()
            };
        }

        internal void AddSample(KilnInfo info)
        {
            this.Timeseries.Add(new KilnStateEntryV1()
            {
                MeasuredTemperature = info.MeasuredTemp,
                SetpointTemperature = info.ScheduleTarget ?? -1,
                TimeIntoSchedule = info.SecondsElapsed ?? -1,
                HeatEnabled = info.Heating,
                AuxEnabled = false,
                VentEnabled = false
            });
        }
    }

    public struct KilnStateEntryV1
    {
        public float TimeIntoSchedule;
        public float MeasuredTemperature;
        public float SetpointTemperature;
        public bool HeatEnabled;
        public bool VentEnabled;
        public bool AuxEnabled;
    }

    public class KilnRunRepository : IKilnRunRepostory
    {
        private string directory;

        private string nameFormat = "{0}-{1}.run";

        public KilnRunRepository(IConfiguration config)
        {
            var dataRoot = config.GetValue<string>("DataLocation");
            this.directory = Path.Combine(dataRoot, "runs");

            Directory.CreateDirectory(this.directory);
        }

        public List<(string, DateTimeOffset)> GetAllRunInfo() 
        {
            var results = new List<(string, DateTimeOffset)> ();

            foreach(var run in Directory.EnumerateFiles(this.directory))
            {
                try
                {
                    results.Add(ParseRunInfo(run));
                }
                catch
                {
                }
            }

            return results;
        }

        public void SaveRun(KilnRun run)
        {
            var info = JsonSerializer.Serialize(run);
            var timeseries = MemoryMarshal.AsBytes<KilnStateEntryV1>(run.Timeseries.ToArray());

            var infoPath = Path.Combine(this.directory, this.GetRunFileName(run));
            var timeseriesPath = Path.Combine(this.directory, run.Id + ".ts");

            using var infoStream = new FileStream(infoPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            infoStream.Write(Encoding.UTF8.GetBytes(info));
            infoStream.Flush();
            infoStream.Close();

            // TODO: append new entries only?
            using var tsStream = new FileStream(timeseriesPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            tsStream.Write(timeseries);
            tsStream.Flush();
            tsStream.Close();
        }

        public KilnRun? GetRun(string schedule, DateTimeOffset startedAt)
        {
            var path = Path.Combine(this.directory, GetRunFileName(schedule, startedAt));

            if (!File.Exists(path))
                return null;

            var info = JsonSerializer.Deserialize<KilnRun>(File.ReadAllText(path));

            if (info == null)
                return null;

            var timeseriesPath = Path.Combine(this.directory, info.Id + ".ts");

            Debug.Assert(info.TimeseriesVersion == 1);

            // TODO: Allocate less pls
            info.Timeseries = MemoryMarshal.Cast<byte, KilnStateEntryV1>(File.ReadAllBytes(timeseriesPath)).ToArray().ToList();

            return info;
        }

        private const string dateTimeFormat = "yyyy-MM-ddTHH-mmZ";

        private string GetRunFileName(KilnRun run) => GetRunFileName(run.ScheduleName, run.RunStartedAt);

        private string GetRunFileName(string scheduleName, DateTimeOffset startedAt)
        {
            var encodedName = Convert.ToBase64String(Encoding.UTF8.GetBytes(scheduleName));
            var startedAtString = startedAt.ToString(dateTimeFormat);

            return string.Format(nameFormat, encodedName, startedAtString);
        }

        private (string, DateTimeOffset) ParseRunInfo(string filename)
        {
            var data = Path.GetFileNameWithoutExtension(filename);

            var sepIndex = data.IndexOf('-');

            if (sepIndex == -1)
                throw new FormatException("unknown run name format");

            var nameSpan = data.AsSpan();

            var scheduleName = nameSpan.Slice(0, sepIndex);

            var nameBytes = new byte[scheduleName.Length].AsSpan();
            if (!Convert.TryFromBase64Chars(scheduleName, nameBytes, out var nameByteCount))
                throw new FormatException("Couldn't decode name");

            var nameValue = Encoding.UTF8.GetString(nameBytes.Slice(0, nameByteCount));

            var startedAt = nameSpan.Slice(sepIndex + 1);
            if (!DateTimeOffset.TryParseExact(startedAt, dateTimeFormat, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AssumeUniversal, out var startAtValue))
                throw new FormatException("Date couldn't be parsed");

            return (nameValue, startAtValue);
        }
    }
}
