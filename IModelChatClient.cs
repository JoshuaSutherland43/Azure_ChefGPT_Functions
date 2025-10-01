using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using static Prog7314_Recipe_LLaMA.Models;

namespace Prog7314_Recipe_LLaMA
{
    public interface IModelChatClient
    {
        Task<ChatResponse> QueryModelAsync(ChatQueryRequest req);
    }

    public class ModelChatClient : IModelChatClient
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly JsonSerializerOptions _jsonOptions;

        public ModelChatClient(IHttpClientFactory httpFactory, IConfiguration config)
        {
            _httpFactory = httpFactory;
            _config = config;

            // JSON options to handle enums as strings
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            _jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        public async Task<ChatResponse> QueryModelAsync(ChatQueryRequest req)
        {
            var client = _httpFactory.CreateClient("HuggingFaceClient");

            // Use Hugging Face Inference API URL
            var baseUrl = _config["HUGGINGFACE_BASE_URL"]?.TrimEnd('/')
                          ?? throw new InvalidOperationException("Model base URL not configured");

            Console.WriteLine("Calling Hugging Face URL: " + baseUrl + "/api/predict/");


            // Prepare inputs for Hugging Face API
            var requestBody = new
            {
                inputs = req.Messages.Select(m => new {
                    role = m.Role.ToString().ToLower(), // "user", "assistant", "system"
                    content = m.Content
                }),
                parameters = new
                {
                    max_new_tokens = req.NumPredict,
                    temperature = req.Temperature,
                    top_p = 0.95 // optional
                }
            };

            var httpReq = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/api/predict/")
            {
                Content = JsonContent.Create(new
                {
                    data = req.Messages.Select(m => m.Content).ToArray()
                }, options: _jsonOptions)
            };

            var apiKey = _config["HUGGINGFACE_API_KEY"];
            if (!string.IsNullOrEmpty(apiKey))
                httpReq.Headers.Add("Authorization", $"Bearer {apiKey}");

            var resp = await client.SendAsync(httpReq);
            resp.EnsureSuccessStatusCode();

            var content = await resp.Content.ReadAsStringAsync();
            // Deserialize to ChatResponse
            var chatResp = JsonSerializer.Deserialize<ChatResponse>(content, _jsonOptions);
            return chatResp!;
        }
    }
}
