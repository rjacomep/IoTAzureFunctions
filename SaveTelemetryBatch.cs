using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace iothubandroid.Function;
// Función de Azure estática para procesar datos de telemetría
public static class TelemetryFunction
{
    // Nombre del contenedor de Azure Blob Storage
    private static string containerName = "telemetry-data";

    // Función que se ejecuta al recibir una solicitud HTTP POST
    [Function("SaveTelemetryBatch")]
    public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        FunctionContext context)
    {
        var log = context.GetLogger("SaveTelemetryBatch");
        log.LogInformation("Solicitud recibida para batch de telemetría.");

        // Lee el contenido del cuerpo de la solicitud
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

        // Verifica si el cuerpo está vacío y devuelve error
        if (string.IsNullOrEmpty(requestBody))
        {
            log.LogError("Error: El cuerpo de la solicitud está vacío.");
            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await errorResponse.WriteStringAsync("El cuerpo de la solicitud no puede estar vacío.");
            return errorResponse;
        }

        try
        {
            // Deserializa el cuerpo como una lista de objetos TelemetryData
            List<TelemetryData>? batch = JsonSerializer.Deserialize<List<TelemetryData>>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (batch == null || batch.Count == 0)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("El lote está vacío o malformado.");
                return errorResponse;
            }

            // Verifica si el lote está vacío o malformado
            if (batch == null || batch.Count == 0)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("El lote está vacío o malformado.");
                return errorResponse;
            }

            var features = new List<object>();

            // Recorre cada entrada del lote
            foreach (var telemetry in batch)
            {
                // Si no hay ubicación, se omite la entrada
                if (telemetry.Location == null)
                {
                    log.LogWarning("Se omitió una entrada por no tener datos de ubicación.");
                    continue;
                }

                // Agrega la entrada como una "feature" en formato GeoJSON
                features.Add(new
                {
                    type = "Feature",
                    geometry = new
                    {
                        type = "Point",
                        coordinates = new[]
                        {
                            telemetry.Location.Longitude,
                            telemetry.Location.Latitude,
                            telemetry.Location.Altitude
                        }
                    },
                    properties = new
                    {
                        timestamp = telemetry.Timestamp.ToString("o"),
                        accelerometer = new
                        {
                            telemetry.Accelerometer.X,
                            telemetry.Accelerometer.Y,
                            telemetry.Accelerometer.Z
                        },
                        gyroscope = new
                        {
                            telemetry.Gyroscope.X,
                            telemetry.Gyroscope.Y,
                            telemetry.Gyroscope.Z
                        },
                        deviceId = telemetry.DeviceId  // Se agrega el deviceId a las propiedades
                    }
                });
            }

            // Crea la estructura completa del archivo GeoJSON
            var geoJson = new
            {
                type = "FeatureCollection",
                features = features
            };

            // Serializa el objeto GeoJSON a string con formato bonito
            string geoJsonString = JsonSerializer.Serialize(geoJson, new JsonSerializerOptions { WriteIndented = true });

            // Genera un nombre de archivo basado en fecha y hora, e incluye el deviceId
            string fileName = $"batch_{batch[0].DeviceId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.geojson";

            // Guarda el archivo en Azure Blob Storage
            await SaveToBlobStorage(geoJsonString, fileName, context);

            // Devuelve respuesta exitosa con el nombre del archivo
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync($"GeoJSON guardado como: {fileName}");
            return response;
        }
        catch (JsonException ex)
        {
            // Captura errores de formato JSON y devuelve error
            log.LogError($"Error al deserializar el JSON: {ex.Message}");
            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await errorResponse.WriteStringAsync("Formato JSON inválido.");
            return errorResponse;
        }
    }

    // Método auxiliar para guardar un string como archivo en Blob Storage
    private static async Task SaveToBlobStorage(string data, string blobName, FunctionContext context)
    {
        // Obtiene la configuración (cadena de conexión de Azure)
        var configuration = context.InstanceServices.GetService(typeof(IConfiguration)) as IConfiguration;
        string? connectionString = configuration?["AzureWebJobsStorage"];

        if (string.IsNullOrEmpty(connectionString))
        {
            var log = context.GetLogger("SaveToBlobStorage");
            log.LogError("La cadena de conexión no se encontró o está vacía.");
            throw new ArgumentNullException(nameof(connectionString));
        }

        // Crea el cliente de Blob Service y obtiene el contenedor
        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        // Crea el contenedor si no existe
        await containerClient.CreateIfNotExistsAsync();

        // Sube el archivo al blob
        var blobClient = containerClient.GetBlobClient(blobName);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
        await blobClient.UploadAsync(stream, overwrite: true);
    }

    // Modelo para representar una entrada de telemetría
    public class TelemetryData
    {
        public required SensorData Accelerometer { get; set; }
        public required SensorData Gyroscope { get; set; }
        public LocationData? Location { get; set; }
        public DateTime Timestamp { get; set; }
        public required string DeviceId { get; set; }  // Nuevo campo para el DeviceId

    }

    // Clase para representar datos de acelerómetro o giroscopio
    public class SensorData
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    // Clase para representar la ubicación geográfica
    public class LocationData
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
    }

}