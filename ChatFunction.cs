using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Prog7314_Recipe_LLaMA.Models;

namespace Prog7314_Recipe_LLaMA;

public class ChatFunction
{
    private readonly ModelChatClient _modelClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public ChatFunction(ModelChatClient modelClient)
    {
        _modelClient = modelClient;

        // JSON options to handle enums as strings and case-insensitive
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    [Function("ChatQuery")]
    public async Task<HttpResponseData> Chat(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "chat/query")] HttpRequestData req)
    {
        try
        {
            // Read request body
            var body = await req.ReadAsStringAsync();
            
            Console.WriteLine($"Received request body: {body}");
            
            if (string.IsNullOrEmpty(body))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                bad.Headers.Add("Content-Type", "application/json");
                await bad.WriteStringAsync("{\"error\":\"Request body is empty\"}");
                return bad;
            }

            ChatQueryRequest? chatReq;

            try
            {
                chatReq = JsonSerializer.Deserialize<ChatQueryRequest>(body, _jsonOptions);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON Deserialization Error: {ex.Message}");
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                bad.Headers.Add("Content-Type", "application/json");
                await bad.WriteStringAsync($"{{\"error\":\"Invalid JSON format: {ex.Message}\"}}");
                return bad;
            }

            if (chatReq == null)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                bad.Headers.Add("Content-Type", "application/json");
                await bad.WriteStringAsync("{\"error\":\"Failed to parse request\"}");
                return bad;
            }

            // Handle missing or empty messages
            if (chatReq.Messages == null || chatReq.Messages.Count == 0)
            {
                Console.WriteLine("No messages in request - rejecting");
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                bad.Headers.Add("Content-Type", "application/json");
                await bad.WriteStringAsync("{\"error\":\"Invalid request body or empty messages\"}");
                return bad;
            }

            Console.WriteLine($"Processing chat request with {chatReq.Messages.Count} messages");
            Console.WriteLine($"Model: {chatReq.Model}, Temperature: {chatReq.Temperature}, NumPredict: {chatReq.NumPredict}");

            // Query the model through ModelChatClient
            var result = await _modelClient.QueryModelAsync(new ChatQueryRequest
            {
                Model = chatReq.Model ?? "Qwen/Qwen2.5-72B-Instruct",
                Messages = chatReq.Messages,
                Temperature = chatReq.Temperature > 0 ? chatReq.Temperature : 0.7f,
                NumPredict = chatReq.NumPredict > 0 ? chatReq.NumPredict : 1500
            });

            Console.WriteLine($"Got result, Recipe length: {result.Recipe?.ToString()?.Length ?? 0}");

            // Return the API response
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(result, _jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in Chat function: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            var errorResp = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResp.Headers.Add("Content-Type", "application/json");
            await errorResp.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions));
            return errorResp;
        }
    }
}