using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace iothubandroid.Function
{
    public class HttpTriggerIoT
    {
        private readonly ILogger<HttpTriggerIoT> _logger;

        public HttpTriggerIoT(ILogger<HttpTriggerIoT> logger)
        {
            _logger = logger;
        }

        [Function("HttpTriggerIoT")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            return new OkObjectResult("Welcome to Azure Functions!");
        }
    }
}
