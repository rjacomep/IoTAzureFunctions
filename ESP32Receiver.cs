using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace iothubandroid.Function;

public class ESP32Receiver
{
    private readonly ILogger<ESP32Receiver> _logger;

    public ESP32Receiver(ILogger<ESP32Receiver> logger)
    {
        _logger = logger;
    }

    [Function("ESP32Receiver")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}