using Microsoft.AspNetCore.Mvc;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("OrderService")]
    public class WeatherForecastController : ControllerBase
    {
        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "GetOrder")]
        public IEnumerable<WeatherForecast> Get()
        {
            _logger.LogInformation("API is calling");
            _logger.LogInformation("This is an Information log");
            _logger.LogWarning("This is a Warning log");
            _logger.LogError("This is an Error log");

            try
            {
                throw new Exception("Test exception");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Order creation failed for OrderId");
                return null;
            }

        }
    }
}
