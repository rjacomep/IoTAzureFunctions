using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

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
        public HttpResponseData Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteString("Welcome to Azure Functions!");
            return response;
        }
    }
}
