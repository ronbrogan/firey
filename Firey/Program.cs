using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Firey
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var scheduleResponse = """{"data":[{"id":1271,"name":"Fast Cone 6","description":null,"ortonConeId":30,"atmosphereId":1,"defaultStartTemp":70,"isCelsius":0,"isPrivate":0,"calculatedTimeMinutes":367,"calculatedOrtonConeId":null,"ramps":[{"id":15428,"order":0,"tempRatePerHr":300,"targetTemp":300,"holdMinutes":20,"calculatedTimeMinutes":46,"calculatedTimeWithRampMinutes":66},{"id":15429,"order":1,"tempRatePerHr":500,"targetTemp":1975,"holdMinutes":0,"calculatedTimeMinutes":201,"calculatedTimeWithRampMinutes":201},{"id":15430,"order":2,"tempRatePerHr":150,"targetTemp":2225,"holdMinutes":0,"calculatedTimeMinutes":100,"calculatedTimeWithRampMinutes":100}],"materials":[],"createdByUserId":40133,"createdByUser":{"id":40133,"name":"Ron Brogan","locale":"en","profile":null,"is_patron":1,"material_image_count":0,"createdAt":"2022-09-19T18:40:02","updatedAt":"2022-11-01T21:30:40"},"createdAt":"2022-11-22T22:09:46","createdAtHuman":"22 Nov 2022","createdAtDiff":"2 days ago","updatedAt":"2022-11-22T22:09:46","updatedAtHuman":"22 Nov 2022","updatedAtDiff":"2 days ago"}],"links":{"first":"https:\/\/api.glazy.org\/api\/kilnschedules?page=1","last":"https:\/\/api.glazy.org\/api\/kilnschedules?page=1","prev":null,"next":null},"meta":{"pagination":{"current_page":1,"first_page_url":"https:\/\/api.glazy.org\/api\/kilnschedules?page=1","from":1,"last_page":1,"links":[{"url":null,"label":"pagination.previous","active":false},{"url":"https:\/\/api.glazy.org\/api\/kilnschedules?page=1","label":"1","active":true},{"url":null,"label":"pagination.next","active":false}],"path":"https:\/\/api.glazy.org\/api\/kilnschedules","per_page":40,"to":1,"total":1,"total_pages":1},"user":{"data":{"id":40133,"name":"Ron Brogan","locale":"en","profile":null,"collections":[{"id":124655,"name":"Bookmarks","description":"Your personal bookmarks","materialCount":1,"isPinned":0,"isPrivate":0,"createdByUserId":40133,"createdAt":"2022-09-19T18:40:02","createdAtHuman":"19 Sep 2022","createdAtDiff":"2 months ago","updatedAt":"2022-10-08T05:18:28","updatedAtHuman":"08 Oct 2022","updatedAtDiff":"1 month ago"}],"is_patron":1,"material_image_count":0,"createdAt":"2022-09-19T18:40:02","updatedAt":"2022-11-01T21:30:40"}}}}""";
            var schedule = JsonSerializer.Deserialize<KilnSchedulesResponse>(scheduleResponse).data[0];

            var sim = new Simulation(schedule);
            var data = sim.Run();

            Console.WriteLine(data);

            var last = 0f;

            var log = new StringBuilder();

            foreach(var tick in data)
            {
                if (tick.Time > last + 1)
                {
                    last = tick.Time;
                    log.AppendLine($"{tick.Time:0.000},{tick.Target:0.0},{tick.Temperature:0.0},{tick.Heating}");
                }

                if (tick.Time > last + 60)
                {
                    last = tick.Time;
                    Console.WriteLine($"{tick.Time:0.000}\t{tick.Target:0.0}\t{tick.Temperature:0.0}\t{tick.Heating}");
                }
            }

            File.WriteAllText("""D:\kilnlog.csv""", log.ToString());
        }
    }
}