using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace iothubandroid.Function;

public class HttpTriggerThird
{
    private readonly ILogger<HttpTriggerThird> _logger;

    public HttpTriggerThird(ILogger<HttpTriggerThird> logger)
    {
        _logger = logger;
    }

    [Function("HttpTriggerThird")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}