using Microsoft.AspNetCore.Mvc;

namespace OrderService.Controllers
{
    [ApiController]
    [Route("OrderService")]
    public class OrderController : ControllerBase
    {
        private readonly ILogger<OrderController> _logger;
        private readonly Dictionary<int, OrderRequest> _orders = new()
{
    {
        1001,
        new OrderRequest
        {
            Id = 1001,
            CustomerName = "John Smith",
            Amount = 250.75m,
            Status = "New",
            CreatedOn = DateTime.UtcNow.AddMinutes(-10)
        }
    },
    {
        1002,
        new OrderRequest
        {
            Id = 1002,
            CustomerName = "Emma Johnson",
            Amount = 1250.00m,
            Status = "Processed",
            CreatedOn = DateTime.UtcNow.AddHours(-2)
        }
    },
    {
        1003,
        new OrderRequest
        {
            Id = 1003,
            CustomerName = "Michael Brown",
            Amount = 499.99m,
            Status = "Shipped",
            CreatedOn = DateTime.UtcNow.AddDays(-1)
        }
    },
    {
        1004,
        new OrderRequest
        {
            Id = 1004,
            CustomerName = "Sophia Davis",
            Amount = 75.25m,
            Status = "Cancelled",
            CreatedOn = DateTime.UtcNow.AddDays(-3)
        }
    },
    {
        1005,
        new OrderRequest
        {
            Id = 1005,
            CustomerName = "Liam Wilson",
            Amount = 9999.99m,
            Status = "New",
            CreatedOn = DateTime.UtcNow.AddMinutes(-30)
        }
    },
    {
        1006,
        new OrderRequest
        {
            Id = 1006,
            CustomerName = "Olivia Taylor",
            Amount = 15000.00m,
            Status = "PendingApproval",
            CreatedOn = DateTime.UtcNow.AddMinutes(-45)
        }
    },
    {
        1007,
        new OrderRequest
        {
            Id = 1007,
            CustomerName = "Noah Anderson",
            Amount = 325.40m,
            Status = "Delivered",
            CreatedOn = DateTime.UtcNow.AddDays(-5)
        }
    },
    {
        1008,
        new OrderRequest
        {
            Id = 1008,
            CustomerName = "Ava Martinez",
            Amount = 875.60m,
            Status = "Returned",
            CreatedOn = DateTime.UtcNow.AddDays(-7)
        }
    },
    {
        1009,
        new OrderRequest
        {
            Id = 1009,
            CustomerName = "James Thomas",
            Amount = 50.00m,
            Status = "New",
            CreatedOn = DateTime.UtcNow.AddMinutes(-5)
        }
    },
    {
        1010,
        new OrderRequest
        {
            Id = 1010,
            CustomerName = "Isabella Garcia",
            Amount = 2200.00m,
            Status = "Processed",
            CreatedOn = DateTime.UtcNow.AddHours(-6)
        }
    }
};


        public OrderController(ILogger<OrderController> logger)
        {
            _logger = logger;
        }

        [HttpPost("CreateOrder")]
        public async Task<IActionResult> CreateOrder(OrderRequest order)
        {
            try
            {
                // Real-world duplicate key issue
                if (_orders.ContainsKey(order.Id))
                    throw new Exception("Violation of PRIMARY KEY constraint.");

                if (order.Amount > 10000 && order.Amount < 50000)
                    throw new Exception("Order exceeds manager approval threshold.");

                // Real-world DB timeout
                if (order.Amount > 50000)
                    throw new TimeoutException("SQL timeout while inserting order.");

                if (order.CustomerName == "BlockedCustomer")
                    throw new Exception("Customer account is inactive.");

                _orders.Add(order.Id, order);

                _logger.LogInformation(
                    "CREATE_ORDER Success | OrderId:{OrderId} Customer:{Customer} Amount:{Amount}",
                    order.Id, order.CustomerName, order.Amount);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "500 INTERNAL SERVER ERROR | CREATE_ORDER Failed | OrderId:{OrderId}", order.Id);
                return StatusCode(500, new
                {
                    errorCode = "GEN_5000",
                    message = "Internal server error.",
                    traceId = HttpContext.TraceIdentifier
                });
            }
        }

        [HttpGet("GetOrder")]
        public async Task<IActionResult> GetOrder(int id)
        {
            try
            {
                if (id == 999)
                    throw new Exception("Cannot open database. Connection pool exhausted.");

                if (!_orders.ContainsKey(id))
                    throw new Exception("SQL query timeout.");

                _logger.LogInformation("GET_ORDER Success | OrderId:{OrderId}", id);

                return Ok(_orders[id]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "500 INTERNAL SERVER ERROR | GET_ORDER Failed | OrderId:{OrderId}", id);
                return StatusCode(500, new
                {
                    errorCode = "GEN_5000",
                    message = "Internal server error.",
                    traceId = HttpContext.TraceIdentifier
                });
            }
        }

        [HttpGet("UpdateOrder")]
        public async Task<IActionResult> UpdateOrder(int id, decimal newAmount)
        {
            try
            {
                // Real-world deadlock issue
                if (newAmount > 25000)
                    throw new Exception("Transaction deadlock victim.");

                // Real-world optimistic concurrency issue
                if (_orders[id].Status == "Shipped")
                    throw new Exception("Foreign key conflict.");

                _orders[id].Amount = newAmount;
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "500 INTERNAL SERVER ERROR | UPDATE_ORDER Failed | OrderId:{OrderId}", id);
                return StatusCode(500, new
                {
                    errorCode = "GEN_5000",
                    message = "Internal server error.",
                    traceId = HttpContext.TraceIdentifier
                });
            }
        }

        [HttpGet("DeleteOrder")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            try
            {
                if (!_orders.ContainsKey(id))
                    throw new Exception("Order not found.");

                // Real-world FK constraint
                if (_orders[id].Status == "Processed")
                    throw new Exception("DELETE conflicted with REFERENCE constraint.");

                // Real-world archived records protection
                if (_orders[id].CreatedOn < DateTime.UtcNow.AddDays(-30))
                    throw new Exception("Unknown production error.");

                _orders.Remove(id);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "500 INTERNAL SERVER ERROR | DELETE_ORDER Failed | OrderId:{OrderId}", id);
                return StatusCode(500, new
                {
                    errorCode = "GEN_5000",
                    message = "Internal server error.",
                    traceId = HttpContext.TraceIdentifier
                });
            }
        }
    }

    public class OrderRequest
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = "";
        public decimal Amount { get; set; }
        public string Status { get; set; } = "New";
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    }
}
