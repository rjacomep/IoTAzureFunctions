using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class TranslatorFunction
{
    private readonly HttpClient _httpClient;
    private readonly string _translatorKey;
    private readonly string _translatorEndpoint;
    private readonly string _translatorRegion;

    public TranslatorFunction(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
        _translatorKey = Environment.GetEnvironmentVariable("TRANSLATOR_KEY")!;
        _translatorEndpoint = Environment.GetEnvironmentVariable("TRANSLATOR_ENDPOINT")!;
        _translatorRegion = Environment.GetEnvironmentVariable("TRANSLATOR_REGION")!;
    }

    [Function("TranslateText")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var json = JsonSerializer.Deserialize<Dictionary<string, string>>(requestBody);

        if (json == null || !json.TryGetValue("text", out var text))
        {
            var badRequest = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Falta el campo 'text'.");
            return badRequest;
        }

        var body = new[] { new { Text = text } };
        var requestContent = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var url = $"{_translatorEndpoint}/translate?api-version=3.0&to=en";
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Ocp-Apim-Subscription-Key", _translatorKey);
        request.Headers.Add("Ocp-Apim-Subscription-Region", _translatorRegion);
        request.Content = requestContent;

        var response = await _httpClient.SendAsync(request);
        var jsonResponse = await response.Content.ReadAsStringAsync();

        var output = req.CreateResponse(response.IsSuccessStatusCode ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.BadRequest);
        await output.WriteStringAsync(jsonResponse);
        return output;
    }
}
