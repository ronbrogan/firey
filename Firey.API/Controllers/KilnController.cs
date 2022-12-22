using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Firey.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class KilnController : ControllerBase
    {
        private readonly KilnControlService kiln;
        private readonly ILogger<KilnController> _logger;

        public KilnController(KilnControlService kiln, ILogger<KilnController> logger)
        {
            this.kiln = kiln;
            _logger = logger;
        }

        [HttpGet()]
        public KilnInfo Get()
        {
            return kiln.Info;
        }

        [HttpGet("schedule")]
        public KilnSchedule? GetRunningSchedule()
        {
            return this.kiln.Schedule;
        }

        [HttpPost("schedule/{scheduleId}")]
        public async Task<ActionResult> StartSchedule(int scheduleId)
        {
            var schedule = await GlazyApi.GetSchedule(scheduleId);

            this._logger.LogWarning("Starting schedule: " + JsonSerializer.Serialize(schedule));

            if (this.kiln.TryStartSchedule(schedule))
                return Ok();
            else
                return BadRequest();
        }

        [HttpPost("stop")]
        public void Stop()
        {
            this.kiln.StopSchedule();
        }
    }
}