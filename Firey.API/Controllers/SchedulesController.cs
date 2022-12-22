using Microsoft.AspNetCore.Mvc;

namespace Firey.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SchedulesController : ControllerBase
    {
        private readonly KilnControlService kiln;
        private readonly ILogger<SchedulesController> _logger;

        public SchedulesController(KilnControlService kiln, ILogger<SchedulesController> logger)
        {
            this.kiln = kiln;
            _logger = logger;
        }

        [HttpGet()]
        public async Task<ActionResult<KilnSchedule>> Get(int id)
        {
            var result = await GlazyApi.GetSchedule(id);
            
            if(result != null)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest();
            }
        }

        [HttpGet("foruser")]
        public async Task<ActionResult<KilnSchedule[]>> GetForUser(int id)
        {
            var result = await GlazyApi.GetSchedulesForUser(id);

            if (result != null)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest();
            }
        }
    }
}