using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using Azure.Storage.Blobs;
using System;

namespace iothubandroid.Function
{
    public class ESP32DataReceiver
    {
        private readonly ILogger _logger;

        public ESP32DataReceiver(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ESP32DataReceiver>();
        }

        [Function("ESP32Receiver")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("HTTP trigger function received a request from ESP32.");

            try
            {
                // Read the JSON from the request body
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                _logger.LogInformation($"Datos recibidos: {requestBody}");

                // Get the Blob Storage connection string from environment variables
                string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                string containerName = "sensordata"; // you can change this as needed

                // Create the Blob container client
                BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync();

                // Create a unique blob name
                string blobName = $"data_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}.json";

                // Convert the JSON string into a stream
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(requestBody)))
                {
                    BlobClient blobClient = containerClient.GetBlobClient(blobName);
                    await blobClient.UploadAsync(stream, overwrite: true);
                    _logger.LogInformation($"Data saved to Blob Storage as: {blobName}");
                }

                // Send success response
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync("JSON recibido y guardado en Blob Storage");

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error procesando el request: {ex.Message}");

                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Error procesando los datos");
                return errorResponse;
            }
        }
    }
}