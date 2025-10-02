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
            
            if (string.IsNullOrEmpty(body))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                bad.Headers.Add("Content-Type", "application/json");
                await bad.WriteStringAsync("{\"error\":\"Request body is empty\"}");
                return bad;
            }

            var chatReq = JsonSerializer.Deserialize<ChatQueryRequest>(body, _jsonOptions);

            if (chatReq == null || chatReq.Messages.Count == 0)
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                bad.Headers.Add("Content-Type", "application/json");
                await bad.WriteStringAsync("{\"error\":\"Invalid request body or empty messages\"}");
                return bad;
            }

            // Query the model through ModelChatClient
            var result = await _modelClient.QueryModelAsync(new ChatQueryRequest
            {
                Model = chatReq.Model,
                Messages = chatReq.Messages,
                Temperature = chatReq.Temperature,
                NumPredict = chatReq.NumPredict
            });

            // Return the API response
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(result, _jsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            var errorResp = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResp.Headers.Add("Content-Type", "application/json");
            await errorResp.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }, _jsonOptions));
            return errorResp;
        }
    }
}