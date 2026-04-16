using Microsoft.AspNetCore.Mvc;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("OrderService")]
    public class OrderController : ControllerBase
    {
        private readonly ILogger<OrderController> _logger;

        public OrderController(ILogger<OrderController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder(OrderRequest request)
        {
            var correlationId = Guid.NewGuid().ToString();
            _logger.LogInformation("Order Process Started. ID: {CorrelationId}, Service: OrderService", correlationId);

            try
            {
                // 1. Simulate a dependency check
                _logger.LogInformation("Checking InventoryService in EastUS...");

                // 2. Simulate a REAL architectural failure (e.g., Database Connection via Redis)
                _logger.LogWarning("Redis Cache 'SouthIndia-Primary' is unresponsive. Attempting failover...");

                throw new TimeoutException("ERR_002: Redis authentication failure during write-through.");
            }
            catch (Exception ex)
            {
                // Log with structured data so KQL can pick up the Service Name and Error Code
                _logger.LogError(ex, "CRITICAL_FAILURE | Service: OrderService | Target: Redis_Cache | Code: ERR_002 | ID: {CorrelationId}", correlationId);
                return StatusCode(500, "Internal Server Error - SRE Team Notified.");
            }
        }
    }

    public class OrderRequest
    {
        public string OrderId { get; set; }
    }
}
