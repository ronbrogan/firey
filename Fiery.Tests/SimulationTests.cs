using Firey;
using Moq;
using System.Text.Json;

namespace Fiery.Tests
{
    [TestClass]
    public class KilnControllerTests
    {
        private KilnSchedule schedule;

        public KilnControllerTests()
        {
            var scheduleResponse = """{"data":[{"id":1271,"name":"Fast Cone 6","description":null,"ortonConeId":30,"atmosphereId":1,"defaultStartTemp":70,"isCelsius":0,"isPrivate":0,"calculatedTimeMinutes":367,"calculatedOrtonConeId":null,"ramps":[{"id":15428,"order":0,"tempRatePerHr":300,"targetTemp":300,"holdMinutes":20,"calculatedTimeMinutes":46,"calculatedTimeWithRampMinutes":66},{"id":15429,"order":1,"tempRatePerHr":500,"targetTemp":1975,"holdMinutes":0,"calculatedTimeMinutes":201,"calculatedTimeWithRampMinutes":201},{"id":15430,"order":2,"tempRatePerHr":150,"targetTemp":2225,"holdMinutes":0,"calculatedTimeMinutes":100,"calculatedTimeWithRampMinutes":100}],"materials":[],"createdByUserId":40133,"createdByUser":{"id":40133,"name":"Ron Brogan","locale":"en","profile":null,"is_patron":1,"material_image_count":0,"createdAt":"2022-09-19T18:40:02","updatedAt":"2022-11-01T21:30:40"},"createdAt":"2022-11-22T22:09:46","createdAtHuman":"22 Nov 2022","createdAtDiff":"2 days ago","updatedAt":"2022-11-22T22:09:46","updatedAtHuman":"22 Nov 2022","updatedAtDiff":"2 days ago"}],"links":{"first":"https:\/\/api.glazy.org\/api\/kilnschedules?page=1","last":"https:\/\/api.glazy.org\/api\/kilnschedules?page=1","prev":null,"next":null},"meta":{"pagination":{"current_page":1,"first_page_url":"https:\/\/api.glazy.org\/api\/kilnschedules?page=1","from":1,"last_page":1,"links":[{"url":null,"label":"pagination.previous","active":false},{"url":"https:\/\/api.glazy.org\/api\/kilnschedules?page=1","label":"1","active":true},{"url":null,"label":"pagination.next","active":false}],"path":"https:\/\/api.glazy.org\/api\/kilnschedules","per_page":40,"to":1,"total":1,"total_pages":1},"user":{"data":{"id":40133,"name":"Ron Brogan","locale":"en","profile":null,"collections":[{"id":124655,"name":"Bookmarks","description":"Your personal bookmarks","materialCount":1,"isPinned":0,"isPrivate":0,"createdByUserId":40133,"createdAt":"2022-09-19T18:40:02","createdAtHuman":"19 Sep 2022","createdAtDiff":"2 months ago","updatedAt":"2022-10-08T05:18:28","updatedAtHuman":"08 Oct 2022","updatedAtDiff":"1 month ago"}],"is_patron":1,"material_image_count":0,"createdAt":"2022-09-19T18:40:02","updatedAt":"2022-11-01T21:30:40"}}}}""";
            this.schedule = JsonSerializer.Deserialize<KilnSchedulesResponse>(scheduleResponse).data[0];
        }

        [TestMethod]
        public void TemperatureStandardDeviation_IsWithinReason()
        {
            var simulator = new Simulation(schedule);
            var ticks = simulator.Run();


            // Assert standard deviation of temp vs target
            var stddev = MathNet.Numerics.Statistics.Statistics.PopulationStandardDeviation(ticks.Select(t => t.Target - t.Temperature));
            Assert.IsTrue(stddev < 3);
        }

        [TestMethod]
        public void Schedule_RunsToTermination()
        {
            var kiln = new ModelKiln();
            var time = new SteppedTimeSource();
            var controller = new KilnControlService(kiln, kiln, time);

            Assert.IsTrue(controller.TryStartSchedule(schedule));

            var states = new Queue<KilnStatus>(new[]
            { 
                KilnStatus.Running, 
                KilnStatus.Cooldown, 
                KilnStatus.Ready
            });

            var lastTick = time.Now;
            const int maxTicks = 500_000;
            var i = maxTicks;
            while (states.Any() && --i > 0)
            {
                kiln.StepSeconds(0.1f);
                time.StepSeconds(0.1f);

                if ((time.Now - lastTick).TotalSeconds > time.HeatPeriod)
                {
                    lastTick = time.Now;
                    controller.Tick();

                    if(states.TryPeek(out var status) && status == controller.Status)
                    {
                        if(!states.TryDequeue(out var deq) || deq != status)
                        {
                            Assert.Fail("Dequeued unexpected value");
                        }
                    }
                }
            }

            
            Assert.IsFalse(states.Any());

            var ticks = maxTicks - i;
            Console.WriteLine($"Complete in {ticks} ticks, or {TimeSpan.FromSeconds(ticks / 10):g}");
        }
    }
}