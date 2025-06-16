using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Azure.Storage.Blobs;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Registro del BlobServiceClient
builder.Services.AddSingleton(x =>
{
    string? connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("La cadena de conexión 'AzureWebJobsStorage' no está configurada.");

    return new BlobServiceClient(connectionString);
});

// ✅ Registro del HttpClient para consumir servicios externos
builder.Services.AddHttpClient();

builder.Build().Run();


