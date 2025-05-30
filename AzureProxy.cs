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
        // Responder a la petición OPTIONS (preflight CORS)
        if (req.Method == HttpMethod.Options.Method)
        {
            var optionsResponse = req.CreateResponse(HttpStatusCode.NoContent);
            AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        string blobName = query["blobName"];

        var response = req.CreateResponse();

        AddCorsHeaders(response);  // Agregar headers CORS en todas las respuestas

        if (string.IsNullOrEmpty(blobName))
        {
            response.StatusCode = HttpStatusCode.BadRequest;
            await response.WriteStringAsync("Debe proveer el parámetro 'blobName'");
            return response;
        }

        string containerName = "telemetry-data"; // Cambia según tu contenedor

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

            response.Headers.Add("Content-Type", "application/json");
            await download.Value.Content.CopyToAsync(response.Body);

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

