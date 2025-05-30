using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace iothubandroid.Function;

public class RouteFunction
{
    private readonly ILogger<RouteFunction> _logger;
    private readonly HttpClient _httpClient;

    public RouteFunction(ILogger<RouteFunction> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
    }

    [Function("RouteFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", "get")] HttpRequestData req)
    {
        if (req.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            var example = new RouteRequest
            {
                Start = new[] { -76.5367933, 3.3745495 },
                End = new[] { -76.5355621, 3.3730322 },
                Mid = new[] { -76.532817, 3.375936 },
                FootpathWaypoints = new List<double[]> { new[] { -76.534, 3.375 }, new[] { -76.533, 3.376 } },
                Mode = "pedestrian"
            };
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(JsonSerializer.Serialize(example));
            return response;
        }

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<RouteRequest>(requestBody);

        if (data == null || data.Start == null || data.End == null)
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Missing start or end coordinates.");
            return badResponse;
        }

        // Construye el array de coordenadas para la ruta
        var coordinates = new List<double[]>();
        coordinates.Add(data.Start);
        if (data.Mid != null && data.Mid.Length == 2)
            coordinates.Add(data.Mid);

        if (data.Mode == "pedestrian" && data.FootpathWaypoints != null && data.FootpathWaypoints.Count > 0)
        {
            coordinates = new List<double[]> { data.Start };
            coordinates.AddRange(data.FootpathWaypoints);
            coordinates.Add(data.End);
        }
        else
        {
            coordinates.Add(data.End);
        }

        // Llama a Azure Maps Route Directions API
        var subscriptionKey = Environment.GetEnvironmentVariable("AZURE_MAPS_KEY");
        var url = $"https://atlas.microsoft.com/route/directions/json?api-version=1.0&subscription-key={subscriptionKey}";

        var body = new
        {
            coordinates = coordinates,
            travelMode = data.Mode ?? "pedestrian"
        };

        var httpContent = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

        try
        {
            var apiResponse = await _httpClient.PostAsync(url, httpContent);
            var apiContent = await apiResponse.Content.ReadAsStringAsync();

            var response = req.CreateResponse(apiResponse.IsSuccessStatusCode ? HttpStatusCode.OK : apiResponse.StatusCode);
            await response.WriteStringAsync(apiContent);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Azure Maps API");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    public class RouteRequest
    {
        [JsonPropertyName("start")]
        public double[]? Start { get; set; }
        [JsonPropertyName("end")]
        public double[]? End { get; set; }
        [JsonPropertyName("mid")]
        public double[]? Mid { get; set; }
        [JsonPropertyName("footpathWaypoints")]
        public List<double[]>? FootpathWaypoints { get; set; }
        [JsonPropertyName("mode")]
        public string? Mode { get; set; }
    }
}