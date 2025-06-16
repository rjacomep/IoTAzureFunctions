// Azure Function en C# para Text-to-Speech con Azure Cognitive Services

// Azure Function en C# (Isolated Worker) para Text-to-Speech con Azure Cognitive Services

using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class TTSFunction
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    public TTSFunction(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<TTSFunction>();
        _httpClient = new HttpClient();
    }

    [Function("TTSFunction")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("TTSFunction (Isolated) received a request.");

        using var reader = new StreamReader(req.Body);
        var requestBody = await reader.ReadToEndAsync();
        var json = JsonDocument.Parse(requestBody);

        if (!json.RootElement.TryGetProperty("texto", out var textoElement) || string.IsNullOrWhiteSpace(textoElement.GetString()))
        {
            var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badResponse.WriteStringAsync("Por favor proporciona un texto válido en el cuerpo de la solicitud.");
            return badResponse;
        }

        var texto = textoElement.GetString();
        var region = "eastus";
        var subscriptionKey = Environment.GetEnvironmentVariable("SPEECH_KEY");
        var endpoint = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1";

        var ssml = $@"<speak version='1.0' xml:lang='es-ES'>
                        <voice xml:lang='es-ES' xml:gender='Male' name='es-ES-AlvaroNeural'>
                            {System.Security.SecurityElement.Escape(texto)}
                        </voice>
                    </speak>";

        using var content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
        _httpClient.DefaultRequestHeaders.Add("X-Microsoft-OutputFormat", "audio-16khz-32kbitrate-mono-mp3");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "AzureFunctionTTS");

        var responseFromAzure = await _httpClient.PostAsync(endpoint, content);

        if (!responseFromAzure.IsSuccessStatusCode)
        {
            var error = await responseFromAzure.Content.ReadAsStringAsync();
            var errorResponse = req.CreateResponse(responseFromAzure.StatusCode);
            await errorResponse.WriteStringAsync(error);
            return errorResponse;
        }

        var audioBytes = await responseFromAzure.Content.ReadAsByteArrayAsync();
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "audio/mpeg");
        await response.Body.WriteAsync(audioBytes, 0, audioBytes.Length);

        return response;
    }
}
