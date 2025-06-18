using System.Net;
using System.Text;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class ProxyGeoJsonUpload
{
    private readonly ILogger _logger;

    public ProxyGeoJsonUpload(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ProxyGeoJsonUpload>();
    }

    [Function("proxygeojsonupload")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("📥 Recibiendo GeoJSON para subir...");

        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var json = JsonDocument.Parse(requestBody);

        if (!json.RootElement.TryGetProperty("fileName", out var fileNameProp) ||
            !json.RootElement.TryGetProperty("geojson", out var geojsonProp))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Faltan fileName o geojson en el cuerpo.");
            return badResponse;
        }

        var fileName = fileNameProp.GetString();
        var geojsonString = geojsonProp.ToString();

        var containerName = "geojsonfiles"; 
        var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

        var blobClient = new BlobContainerClient(connectionString, containerName);
        await blobClient.CreateIfNotExistsAsync();

        var blob = blobClient.GetBlobClient(fileName);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(geojsonString));
        await blob.UploadAsync(stream, overwrite: true);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("GeoJSON subido correctamente.");
        return response;
    }
}
