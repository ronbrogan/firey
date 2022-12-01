using System.Text.Json;

namespace Firey
{
    public class GlazyApi
    {
        private static HttpClient client = new();

        private const string UserSchedulesFormat = "https://api.glazy.org/api/kilnschedules?&u={0}";
        private const string ScheduleInfoFormat = "https://api.glazy.org/api/kilnschedules/{0}";

        public static async Task<KilnSchedule[]> GetSchedulesForUser(int userId)
        {
            var url = string.Format(UserSchedulesFormat, userId);
            var result = await client.GetAsync(url);
            if(result.IsSuccessStatusCode)
            {
                var resp = await JsonSerializer.DeserializeAsync<KilnSchedulesResponse>(result.Content.ReadAsStream());
             
                if(resp != null)
                {
                    return resp.data;
                }
            }

            throw new Exception("Unable to retrieve schedules for user");
        }

        public static async Task<KilnSchedule> GetSchedule(int scheduleId)
        {
            var url = string.Format(ScheduleInfoFormat, scheduleId);
            var result = await client.GetAsync(url);
            if (result.IsSuccessStatusCode)
            {
                var resp = await JsonSerializer.DeserializeAsync<KilnScheduleResponse>(result.Content.ReadAsStream());

                if (resp != null)
                {
                    return resp.data;
                }
            }

            throw new Exception("Unable to retrieve schedule");
        }
    }

    public class KilnScheduleResponse
    {
        public KilnSchedule data { get; set; }
    }

    public class KilnSchedulesResponse
    {
        public KilnSchedule[] data { get; set; }
        public Links links { get; set; }
        public Meta meta { get; set; }
    }

    public class Links
    {
        public string first { get; set; }
        public string last { get; set; }
        public object prev { get; set; }
        public object next { get; set; }
    }

    public class Meta
    {
        public Pagination pagination { get; set; }
        public User user { get; set; }
    }

    public class Pagination
    {
        public int current_page { get; set; }
        public string first_page_url { get; set; }
        public int from { get; set; }
        public int last_page { get; set; }
        public Link[] links { get; set; }
        public string path { get; set; }
        public int per_page { get; set; }
        public int to { get; set; }
        public int total { get; set; }
        public int total_pages { get; set; }
    }

    public class Link
    {
        public string url { get; set; }
        public string label { get; set; }
        public bool active { get; set; }
    }

    public class User
    {
        public Data data { get; set; }
    }

    public class Data
    {
        public int id { get; set; }
        public string name { get; set; }
        public string locale { get; set; }
        public int is_patron { get; set; }
        public int material_image_count { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
    }


    public class KilnSchedule
    {
        public int id { get; set; }
        public string name { get; set; }
        public object description { get; set; }
        public int ortonConeId { get; set; }
        public int atmosphereId { get; set; }
        public int defaultStartTemp { get; set; }
        public int isCelsius { get; set; }
        public int isPrivate { get; set; }
        public int calculatedTimeMinutes { get; set; }
        public object calculatedOrtonConeId { get; set; }
        public ScheduleRamp[] ramps { get; set; }
        public object[] materials { get; set; }
        public int createdByUserId { get; set; }
        public UserInfo createdByUser { get; set; }
        public DateTime createdAt { get; set; }
        public string createdAtHuman { get; set; }
        public string createdAtDiff { get; set; }
        public DateTime updatedAt { get; set; }
        public string updatedAtHuman { get; set; }
        public string updatedAtDiff { get; set; }
    }

    public class UserInfo
    {
        public int id { get; set; }
        public string name { get; set; }
        public string locale { get; set; }
        public object profile { get; set; }
        public int is_patron { get; set; }
        public int material_image_count { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
    }

    public class ScheduleRamp
    {
        public int id { get; set; }
        public int order { get; set; }
        public int tempRatePerHr { get; set; }
        public int targetTemp { get; set; }
        public int holdMinutes { get; set; }
        public int calculatedTimeMinutes { get; set; }
        public int calculatedTimeWithRampMinutes { get; set; }
    }

}
