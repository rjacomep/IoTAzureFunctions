using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

public class NavigationGuideFunction
{
    private readonly ILogger _logger;
    private readonly BlobServiceClient _blobServiceClient;

    public NavigationGuideFunction(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<NavigationGuideFunction>();
        _blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
    }

    [Function("NavigationGuideFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("📍 NavigationGuideFunction triggered");

        try
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation($"📨 Cuerpo recibido: {requestBody}");

            var data = JsonDocument.Parse(requestBody);

            if (!data.RootElement.TryGetProperty("latitude", out var latEl) ||
                !data.RootElement.TryGetProperty("longitude", out var lonEl) ||
                !data.RootElement.TryGetProperty("blobName", out var blobNameEl))
            {
                _logger.LogWarning("❌ Faltan parámetros: latitude, longitude o blobName.");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Debe incluir 'latitude', 'longitude' y 'blobName' en el cuerpo.");
                return badResponse;
            }

            double userLat = latEl.GetDouble();
            double userLon = lonEl.GetDouble();
            string blobName = blobNameEl.GetString() ?? "";

            _logger.LogInformation($"Posición recibida: lat={userLat}, lon={userLon}");
            _logger.LogInformation($"Nombre de blob recibido: {blobName}");

            // Obtener el cliente del contenedor y del blob
            string containerName = blobName.StartsWith("customroute_") ? "geojsonfiles" : "telemetry-data";
            _logger.LogInformation($"📦 Usando contenedor: {containerName}");
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

            var blobClient = containerClient.GetBlobClient(blobName);

            _logger.LogInformation($"🔍 Verificando existencia del blob: {blobName}");
            if (!await blobClient.ExistsAsync())
            {
                _logger.LogWarning($"Blob '{blobName}' no encontrado en el contenedor.");
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("El archivo GeoJSON no fue encontrado.");
                return notFound;
            }

            _logger.LogInformation($"✅ Blob '{blobName}' encontrado. Iniciando lectura...");

            using var geoStream = await blobClient.OpenReadAsync();
            var geoDoc = JsonDocument.Parse(geoStream);

            double minDistance = double.MaxValue;
            string closestZone = "";
            int totalFeatures = 0;

            foreach (var feature in geoDoc.RootElement.GetProperty("features").EnumerateArray())
            {
                totalFeatures++;
                var props = feature.GetProperty("properties");
                var geometry = feature.GetProperty("geometry");
                if (geometry.GetProperty("type").GetString() != "Point") continue;

                var coords = geometry.GetProperty("coordinates").EnumerateArray().ToArray();
                double pointLon = coords[0].GetDouble();
                double pointLat = coords[1].GetDouble();
                double distance = Haversine(userLat, userLon, pointLat, pointLon);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestZone = props.TryGetProperty("zoneType", out var z) ? z.GetString() ?? "" : "";
                }
            }

            _logger.LogInformation($"📊 Total de features analizadas: {totalFeatures}");
            _logger.LogInformation($"📏 Distancia mínima encontrada: {minDistance:F4} km");
            _logger.LogInformation($"📌 Zona más cercana: {closestZone}");

            string zoneDesc = closestZone switch
            {
                "hallway" => "un pasillo",
                "rest_area" => "una zona de descanso",
                "stairs" => "unas escaleras",
                _ => "una zona desconocida"
            };

            var texto = (minDistance > 0.05)
                ? "No se detecta una zona cercana en la ruta."
                : $"Estás cerca de {zoneDesc}. Sigue recto por 15 metros.";

            _logger.LogInformation($"🗣 Texto generado para respuesta: {texto}");

            var result = new { texto = texto };
            var jsonResponse = req.CreateResponse(HttpStatusCode.OK);
            jsonResponse.Headers.Add("Content-Type", "application/json");
            await jsonResponse.WriteStringAsync(JsonSerializer.Serialize(result));

            _logger.LogInformation("✅ Respuesta enviada correctamente.");
            return jsonResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error inesperado: {ex.Message}");
            _logger.LogError(ex.ToString());

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Ocurrió un error al procesar la solicitud.");
            return errorResponse;
        }
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        double R = 6371e3; // metros
        double φ1 = lat1 * Math.PI / 180;
        double φ2 = lat2 * Math.PI / 180;
        double Δφ = (lat2 - lat1) * Math.PI / 180;
        double Δλ = (lon2 - lon1) * Math.PI / 180;

        double a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                   Math.Cos(φ1) * Math.Cos(φ2) *
                   Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c / 1000; // kilómetros
    }
}
