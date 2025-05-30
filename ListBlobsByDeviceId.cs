using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace iothubandroid.Function;
public class ListBlobsByDeviceId
{
    private readonly ILogger _logger;

    public ListBlobsByDeviceId(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ListBlobsByDeviceId>();
    }

    [Function("ListBlobsByDeviceId")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "listblobs/{deviceId}")] HttpRequestData req,
        string deviceId)
    {
        string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        string containerName = "telemetry-data";

        var containerClient = new BlobContainerClient(connectionString, containerName);

        var blobsList = new List<string>();
        string prefix = $"batch_{deviceId}_";

        await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(prefix: prefix))
        {
            blobsList.Add(blobItem.Name);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(blobsList);

        return response;
    }
}