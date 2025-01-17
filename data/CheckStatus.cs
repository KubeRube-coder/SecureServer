using Microsoft.AspNetCore.Mvc;

namespace SecureServer.data
{
    [ApiController]
    [Route("api/check")]
    public class CheckStatus : ControllerBase
    {
        [HttpGet("status")]
        public async Task<IActionResult> Status()
        {
            string ok = "Its Okay";
            return Ok(new { ok });
        }
    }
}
