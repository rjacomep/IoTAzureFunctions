using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace iothubandroid.Function;

public class HttpTriggerSecond
{
    private readonly ILogger<HttpTriggerSecond> _logger;

    public HttpTriggerSecond(ILogger<HttpTriggerSecond> logger)
    {
        _logger = logger;
    }

    [Function("HttpTriggerSecond")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}