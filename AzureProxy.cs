using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Azure.Storage.Blobs;
using Azure;
using System.Net;

namespace iothubandroid.Function;

public class ProxyGeoJsonFunction
{
    private readonly BlobServiceClient _blobServiceClient;

    public ProxyGeoJsonFunction(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    [Function("ProxyGeoJson")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "options")] HttpRequestData req)
    {
        if (req.Method == HttpMethod.Options.Method)
        {
            var optionsResponse = req.CreateResponse(HttpStatusCode.NoContent);
            AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        string? blobName = query["blobName"];

        var response = req.CreateResponse();

        AddCorsHeaders(response);

        if (string.IsNullOrEmpty(blobName))
        {
            response.StatusCode = HttpStatusCode.BadRequest;
            await response.WriteStringAsync("Debe proveer el parámetro 'blobName'");
            return response;
        }

        string containerName = "telemetry-data";

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var exists = await blobClient.ExistsAsync();
            if (!exists)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync("Archivo no encontrado");
                return response;
            }

            var download = await blobClient.DownloadStreamingAsync();
            using var reader = new StreamReader(download.Value.Content);
            string jsonContent = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                response.StatusCode = HttpStatusCode.NoContent;
                await response.WriteStringAsync("El blob está vacío.");
                return response;
            }

            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(jsonContent);

            return response;
        }
        catch (RequestFailedException ex)
        {
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync($"Error accediendo al blob: {ex.Message}");
            return response;
        }
    }

    private void AddCorsHeaders(HttpResponseData response)
    {
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
    }
}



