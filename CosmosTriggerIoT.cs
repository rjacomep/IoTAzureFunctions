using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace iothubandroid.Function;

public class CosmosTriggerIoT
{
    private readonly ILogger<CosmosTriggerIoT> _logger;

    public CosmosTriggerIoT(ILogger<CosmosTriggerIoT> logger)
    {
        _logger = logger;
    }

    [Function("CosmosTriggerIoT")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", "get")] HttpRequestData req)
    {
        // Si es GET, retorna un ejemplo quemado
        if (req.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            var example = new SensorPayload
            {
                DeviceId = "esp32-demo",
                Timestamp = DateTime.UtcNow.ToString("o"),
                AccelX = 0.12,
                AccelY = -0.34,
                AccelZ = 9.81,
                GyroX = 0.01,
                GyroY = 0.02,
                GyroZ = 0.03,
                GpsLat = 4.60971,
                GpsLon = -74.08175,
                GpsAlt = 2600.0
            };
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(JsonSerializer.Serialize(example));
            return response;
        }

        // POST normal
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<SensorPayload>(requestBody);

        if (data == null || string.IsNullOrEmpty(data.DeviceId))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Missing deviceId or payload.");
            return badResponse;
        }

        string timestamp = data.Timestamp ?? DateTime.UtcNow.ToString("o");

        var storageAccount = CloudStorageAccount.Parse(
            Environment.GetEnvironmentVariable("COSMOSDB_TABLE_CONN"));
        var tableClient = storageAccount.CreateCloudTableClient();
        var table = tableClient.GetTableReference("DevicesTable");
        await table.CreateIfNotExistsAsync();

        var entity = new DynamicTableEntity("iot-devices", data.DeviceId)
        {
            Properties = {
                { "timestamp", new EntityProperty(timestamp) },
                { "accel_x", new EntityProperty(data.AccelX) },
                { "accel_y", new EntityProperty(data.AccelY) },
                { "accel_z", new EntityProperty(data.AccelZ) },
                { "gyro_x", new EntityProperty(data.GyroX) },
                { "gyro_y", new EntityProperty(data.GyroY) },
                { "gyro_z", new EntityProperty(data.GyroZ) },
                { "gps_lat", new EntityProperty(data.GpsLat) },
                { "gps_lon", new EntityProperty(data.GpsLon) },
                { "gps_alt", new EntityProperty(data.GpsAlt) }
            }
        };

        var insertOperation = TableOperation.InsertOrMerge(entity);
        await table.ExecuteAsync(insertOperation);

        var okResponse = req.CreateResponse(HttpStatusCode.OK);
        await okResponse.WriteStringAsync($"Device {data.DeviceId} data stored.");
        return okResponse;
    }

    public class SensorPayload
    {
        public string? DeviceId { get; set; }
        public string? Timestamp { get; set; }
        public double AccelX { get; set; }
        public double AccelY { get; set; }
        public double AccelZ { get; set; }
        public double GyroX { get; set; }
        public double GyroY { get; set; }
        public double GyroZ { get; set; }
        public double GpsLat { get; set; }
        public double GpsLon { get; set; }
        public double GpsAlt { get; set; }
    }
}