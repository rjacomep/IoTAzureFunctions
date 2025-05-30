using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace iothubandroid.Function
{
    public class GenerarRutaDesdeGeoJson
    {
        private readonly ILogger _logger;

        public GenerarRutaDesdeGeoJson(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<GenerarRutaDesdeGeoJson>();
        }

        [Function("GenerarRutaDesdeGeoJson")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string? nombreArchivo = query["archivo"];

            var response = req.CreateResponse();

            if (string.IsNullOrEmpty(nombreArchivo))
            {
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Falta el parámetro 'archivo' en la URL.");
                return response;
            }

            string? connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            if (string.IsNullOrEmpty(connectionString))
            {
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                await response.WriteStringAsync("No se encontró la cadena de conexión en las variables de entorno.");
                return response;
            }

            string contenedor = "telemetry-data";

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(contenedor);
            var blobClient = containerClient.GetBlobClient(nombreArchivo);

            if (!await blobClient.ExistsAsync())
            {
                response.StatusCode = System.Net.HttpStatusCode.NotFound;
                await response.WriteStringAsync($"El archivo '{nombreArchivo}' no existe.");
                return response;
            }

            JsonDocument json;
            try
            {
                using var content = await blobClient.OpenReadAsync();
                json = await JsonDocument.ParseAsync(content);
            }
            catch (JsonException ex)
            {
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                await response.WriteStringAsync($"Error al procesar el archivo JSON: {ex.Message}");
                return response;
            }

            var features = json.RootElement.GetProperty("features")
                .EnumerateArray()
                .Where(f => f.GetProperty("geometry").GetProperty("type").GetString() == "Point")
                .Select(f => new
                {
                    Timestamp = DateTime.TryParse(
                        f.GetProperty("properties").GetProperty("timestamp").GetString(),
                        out var ts) ? ts : default,
                    Coordinates = f.GetProperty("geometry").GetProperty("coordinates")
                })
                .OrderBy(f => f.Timestamp)
                .ToList();

            if (features.Count < 2)
            {
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Se necesitan al menos 2 puntos para generar una ruta.");
                return response;
            }

            var coordenadas = new JsonArray();
            foreach (var f in features)
                coordenadas.Add(f.Coordinates);

            var startPoint = new JsonObject
            {
                ["type"] = "Feature",
                ["geometry"] = new JsonObject
                {
                    ["type"] = "Point",
                    ["coordinates"] = JsonNode.Parse(features.First().Coordinates.ToString())
                },
                ["properties"] = new JsonObject
                {
                    ["descripcion"] = "Inicio de la ruta"
                }
            };

            var endPoint = new JsonObject
            {
                ["type"] = "Feature",
                ["geometry"] = new JsonObject
                {
                    ["type"] = "Point",
                    ["coordinates"] = JsonNode.Parse(features.Last().Coordinates.ToString())
                },
                ["properties"] = new JsonObject
                {
                    ["descripcion"] = "Fin de la ruta"
                }
            };

            var rutaFeature = new JsonObject
            {
                ["type"] = "Feature",
                ["geometry"] = new JsonObject
                {
                    ["type"] = "LineString",
                    ["coordinates"] = coordenadas
                },
                ["properties"] = new JsonObject
                {
                    ["descripcion"] = "Ruta generada desde puntos ordenados por timestamp"
                }
            };

            var geojsonRuta = new JsonObject
            {
                ["type"] = "FeatureCollection",
                ["features"] = new JsonArray { startPoint, rutaFeature, endPoint }
            };

            var nuevoNombre = Path.GetFileNameWithoutExtension(nombreArchivo) + "-ruta.geojson";
            var nuevoBlob = containerClient.GetBlobClient(nuevoNombre);

            using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, geojsonRuta, new JsonSerializerOptions { WriteIndented = true });
            stream.Position = 0;
            await nuevoBlob.UploadAsync(stream, overwrite: true);

            await response.WriteStringAsync($"Ruta generada y guardada como '{nuevoNombre}'.");
            response.StatusCode = System.Net.HttpStatusCode.OK;
            return response;
        }
    }
}