using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection; // <-- ESTA ES LA LÍNEA CLAVE
using Azure.Storage.Blobs;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// REGISTRO DEL BlobServiceClient
builder.Services.AddSingleton(x =>
{
  string? connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("La cadena de conexión 'AzureWebJobsStorage' no está configurada.");

var blobServiceClient = new BlobServiceClient(connectionString);

    return new BlobServiceClient(connectionString);
});

builder.Build().Run();

