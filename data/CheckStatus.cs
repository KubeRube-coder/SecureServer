using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SecureServer.Data
{
    [ApiController]
    [Route("api/check")]
    public class CheckStatus : ControllerBase
    {
        private readonly HealthCheckService _healthCheckService;

        public CheckStatus(HealthCheckService healthCheckService)
        {
            _healthCheckService = healthCheckService;
        }

        [HttpGet("status")]
        public async Task<IActionResult> Status()
        {
            var report = await _healthCheckService.CheckHealthAsync();
            var status = report.Status.ToString();
            var details = report.Entries.Select(e => new
            {
                Component = e.Key,
                Status = e.Value.Status.ToString(),
                Description = e.Value.Description
            });

            return Ok(new
            {
                OverallStatus = status,
                Components = details
            });
        }
    }
}
