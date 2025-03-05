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
                avgRespone = report.TotalDuration.TotalMilliseconds.ToString() + " ms",
                Description = e.Value.Description
            });

            return Ok(new
            {
                OverallStatus = status,
                Components = details
            });
        }

        [HttpGet("help")]
        public async Task<IActionResult> help()
        {
            // Test request
            return Ok(new
            {
                help = "Данный запрос существует для ознакомления с ошибками",
                statusCode400 = "Сервер не может или не будет обрабатывать запрос из-за чего-то, что воспринимается как ошибка клиента (например, неправильный синтаксис, формат или маршрутизация запроса).",
                statusCode401 = "Сервер получил неправильные данные для авторизации.",
                statusCode403 = "Клиент не имеет права доступа к запрашиваемому контенту.",
                statusCode404 = "Сервер не может найти запрашиваемый ресурс",
                statusCode418 = "Шутка. Сервер отклоняет попытку заварить кофе в чайнике :))",
                statusCode429 = "Клиент отправил слишком много запросов в определённый промежуток времени. (Запросы будут заблокированы на некоторое время)",
                statusCode500 = "Внутренняя ошибка сервера. В основном проблема с базой данных"
            });
        }
    }
}
