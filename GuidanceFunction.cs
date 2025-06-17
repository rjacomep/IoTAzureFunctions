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
    private readonly HttpClient _httpClient;

    public NavigationGuideFunction(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<NavigationGuideFunction>();
        _blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
        _httpClient = new HttpClient();
    }

    [Function("NavigationGuideFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("📍 NavigationGuideFunction triggered");

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var data = JsonDocument.Parse(requestBody);

        if (!data.RootElement.TryGetProperty("latitude", out var latEl) ||
            !data.RootElement.TryGetProperty("longitude", out var lonEl))
        {
            _logger.LogWarning("❌ Coordenadas no proporcionadas correctamente.");
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Debe incluir 'latitude' y 'longitude' en el cuerpo.");
            return badResponse;
        }

        double userLat = latEl.GetDouble();
        double userLon = lonEl.GetDouble();
        _logger.LogInformation($"📡 Posición recibida: lat={userLat}, lon={userLon}");

        // Leer GeoJSON desde Blob Storage
        var containerClient = _blobServiceClient.GetBlobContainerClient("telemetry-data");
        var blobClient = containerClient.GetBlobClient("merged_all_20250409054216-ruta.geojson");

        if (!await blobClient.ExistsAsync())
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("El archivo GeoJSON no fue encontrado.");
            return notFound;
        }

        using var geoStream = await blobClient.OpenReadAsync();
        var geoDoc = JsonDocument.Parse(geoStream);

        // Buscar zona más cercana
        double minDistance = double.MaxValue;
        string closestZone = "";
        foreach (var feature in geoDoc.RootElement.GetProperty("features").EnumerateArray())
        {
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

        _logger.LogInformation($"🗣 Texto para sintetizar: {texto}");

        // Azure Speech API
        var speechKey = Environment.GetEnvironmentVariable("SPEECH_KEY");
        var region = "eastus";
        var endpoint = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1";

        var ssml = $@"<speak version='1.0' xml:lang='es-ES'>
                        <voice xml:lang='es-ES' xml:gender='Male' name='es-ES-AlvaroNeural'>
                            {System.Security.SecurityElement.Escape(texto)}
                        </voice>
                    </speak>";

        using var content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", speechKey);
        _httpClient.DefaultRequestHeaders.Add("X-Microsoft-OutputFormat", "audio-16khz-32kbitrate-mono-mp3");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "AzureFunction-Navigation");

        var ttsResponse = await _httpClient.PostAsync(endpoint, content);

        if (!ttsResponse.IsSuccessStatusCode)
        {
            string err = await ttsResponse.Content.ReadAsStringAsync();
            _logger.LogError("❌ Speech API error: {StatusCode} - {Message}", ttsResponse.StatusCode, err);
            var errorResponse = req.CreateResponse(ttsResponse.StatusCode);
            await errorResponse.WriteStringAsync("Error al generar audio: " + err);
            return errorResponse;
        }

        var audioBytes = await ttsResponse.Content.ReadAsByteArrayAsync();
        var audioResponse = req.CreateResponse(HttpStatusCode.OK);
        audioResponse.Headers.Add("Content-Type", "audio/mpeg");
        await audioResponse.Body.WriteAsync(audioBytes, 0, audioBytes.Length);
        _logger.LogInformation("✅ Audio generado correctamente.");

        return audioResponse;
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

        return R * c / 1000; // km
    }
}


