using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using static Prog7314_Recipe_LLaMA.Models;

namespace Prog7314_Recipe_LLaMA
{
    public class ModelChatClient
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly JsonSerializerOptions _jsonOptions;

        public ModelChatClient(IHttpClientFactory httpFactory, IConfiguration config)
        {
            _httpFactory = httpFactory;
            _config = config;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            _jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        public async Task<ChatResponse> QueryModelAsync(ChatQueryRequest req)
        {
            var client = _httpFactory.CreateClient("HuggingFaceClient");
            
            var spaceUrl = _config["HUGGINGFACE_SPACE_URL"]?.TrimEnd('/')
                        ?? "https://samkelo28-chefgpt3.hf.space";

            var lastUserMessage = req.Messages.LastOrDefault(m => m.Role == ChatRole.User)?.Content 
                                ?? "Hello";
            var model = req.Model ?? "Qwen/Qwen2.5-72B-Instruct";
            var maxTokens = req.NumPredict > 0 ? req.NumPredict : 1500;
            var temperature = req.Temperature > 0 ? req.Temperature : 0.7f;
            var language = req.Language ?? "English"; // ✅ Use language from request

            var joinUrl = $"{spaceUrl}/gradio_api/queue/join";
            Console.WriteLine($"Calling ChefGPT API: {joinUrl}");

            var sessionHash = Guid.NewGuid().ToString("N").Substring(0, 11);
            
            // ✅ FIXED: Include all 5 parameters [message, language, model, maxTokens, temperature]
            var joinRequestBody = new
            {
                fn_index = 2,  
                session_hash = sessionHash,
                data = new object[]
                {
                    lastUserMessage,    // Parameter 1: User message
                    language,           // Parameter 2: Language (ADDED!)
                    model,              // Parameter 3: Model name
                    (double)maxTokens,  // Parameter 4: Max tokens
                    (double)temperature // Parameter 5: Temperature
                }
            };

            var joinJsonBody = JsonSerializer.Serialize(joinRequestBody, _jsonOptions);
            Console.WriteLine($"Join request: {joinJsonBody}");

            var joinReq = new HttpRequestMessage(HttpMethod.Post, joinUrl)
            {
                Content = new StringContent(joinJsonBody, Encoding.UTF8, "application/json")
            };

            HttpResponseMessage joinResp;
            string eventId;

            try
            {
                joinResp = await client.SendAsync(joinReq);
                var joinContent = await joinResp.Content.ReadAsStringAsync();
                
                if (!joinResp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Join error ({joinResp.StatusCode}): {joinContent}");
                    return GetFallbackResponse(lastUserMessage, model);
                }

                Console.WriteLine($"Join response: {joinContent}");

                using var joinDoc = JsonDocument.Parse(joinContent);
                if (!joinDoc.RootElement.TryGetProperty("event_id", out var eventIdElement))
                {
                    Console.WriteLine("No event_id in join response");
                    return GetFallbackResponse(lastUserMessage, model);
                }

                eventId = eventIdElement.GetString() ?? string.Empty;
                Console.WriteLine($"Got event_id: {eventId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Join request failed: {ex.Message}");
                return GetFallbackResponse(lastUserMessage, model);
            }

            // Poll for results using SSE
            var dataUrl = $"{spaceUrl}/gradio_api/queue/data?session_hash={sessionHash}";
            var maxAttempts = 30;
            var delayMs = 1000;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Console.WriteLine($"Polling attempt {attempt + 1}/{maxAttempts}...");
                await Task.Delay(delayMs);

                try
                {
                    var dataReq = new HttpRequestMessage(HttpMethod.Get, dataUrl);
                    dataReq.Headers.Add("Accept", "text/event-stream");

                    var dataResp = await client.SendAsync(dataReq, HttpCompletionOption.ResponseHeadersRead);
                    
                    if (!dataResp.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Data stream failed ({dataResp.StatusCode})");
                        continue;
                    }

                    // Read the SSE stream
                    using var stream = await dataResp.Content.ReadAsStreamAsync();
                    using var reader = new System.IO.StreamReader(stream);
                    
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        Console.WriteLine($"SSE Line: {line}");
                        
                        if (line.StartsWith("data: "))
                        {
                            var jsonData = line.Substring(6);
                            
                            if (string.IsNullOrWhiteSpace(jsonData)) continue;

                            try
                            {
                                using var dataDoc = JsonDocument.Parse(jsonData);
                                var root = dataDoc.RootElement;

                                // Check message type
                                if (root.TryGetProperty("msg", out var msgElement))
                                {
                                    var msg = msgElement.GetString();
                                    Console.WriteLine($"Message type: {msg}");

                                    if (msg == "process_completed")
                                    {
                                        Console.WriteLine("Processing complete!");
                                        
                                        // Extract output data
                                        if (root.TryGetProperty("output", out var outputElement))
                                        {
                                            string resultText = ExtractResultText(outputElement);
                                            
                                            if (!string.IsNullOrWhiteSpace(resultText))
                                            {
                                                Console.WriteLine($"Got recipe: {resultText.Substring(0, Math.Min(100, resultText.Length))}...");
                                                return new ChatResponse
                                                {
                                                    Model = model,
                                                    CreatedAt = DateTime.UtcNow,
                                                    Recipe = resultText,
                                                    Done = true
                                                };
                                            }
                                        }
                                        
                                        // If we couldn't extract the result, use fallback
                                        return GetFallbackResponse(lastUserMessage, model);
                                    }
                                    else if (msg == "process_starts")
                                    {
                                        Console.WriteLine("Processing started...");
                                    }
                                    else if (msg == "estimation")
                                    {
                                        if (root.TryGetProperty("rank", out var rankElement))
                                        {
                                            Console.WriteLine($"Waiting in queue (position: {rankElement.GetInt32()})...");
                                        }
                                        else
                                        {
                                            Console.WriteLine("Waiting in queue...");
                                        }
                                    }
                                }
                            }
                            catch (JsonException jex)
                            {
                                Console.WriteLine($"JSON parse error: {jex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Polling attempt {attempt + 1} failed: {ex.Message}");
                }
                
                // Increase delay progressively
                delayMs = Math.Min(delayMs + 500, 3000);
            }

            Console.WriteLine("Polling timed out - using fallback");
            return GetFallbackResponse(lastUserMessage, model);
        }

        private string ExtractResultText(JsonElement outputElement)
        {
            // Try different response formats
            if (outputElement.ValueKind == JsonValueKind.Object)
            {
                // Format 1: {"data": ["result text"]}
                if (outputElement.TryGetProperty("data", out var dataElement) &&
                    dataElement.ValueKind == JsonValueKind.Array &&
                    dataElement.GetArrayLength() > 0)
                {
                    return dataElement[0].GetString() ?? string.Empty;
                }
                
                // Format 2: Direct string in output
                if (outputElement.ValueKind == JsonValueKind.String)
                {
                    return outputElement.GetString() ?? string.Empty;
                }
            }
            else if (outputElement.ValueKind == JsonValueKind.Array && outputElement.GetArrayLength() > 0)
            {
                // Format 3: Direct array ["result text"]
                return outputElement[0].GetString() ?? string.Empty;
            }
            else if (outputElement.ValueKind == JsonValueKind.String)
            {
                // Format 4: Direct string
                return outputElement.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        private ChatResponse GetFallbackResponse(string message, string model)
        {
            var msg = message.ToLower();
            string fallbackText;

            if (msg.Contains("burger") || msg.Contains("cheese"))
            {
                fallbackText = @"Perfect Cheeseburger Recipe:

INGREDIENTS:
• 1 lb ground beef (80/20 blend)
• 4 slices cheddar or American cheese
• 4 burger buns
• 1 tbsp vegetable oil
• Salt and black pepper to taste
• Optional toppings: lettuce, tomato, onion, pickles, ketchup, mustard

INSTRUCTIONS:
Step 1: Form the patties - Divide beef into 4 equal portions and gently form into ¾-inch thick patties. Make a slight indentation in the center of each patty to prevent bulging during cooking.

Step 2: Season - Generously season both sides of each patty with salt and pepper.

Step 3: Heat the pan - Heat oil in a large skillet or griddle over medium-high heat until shimmering.

Step 4: Cook the burgers - Place patties in the hot pan and cook for 3-4 minutes without moving until well-browned on the bottom.

Step 5: Flip and add cheese - Flip burgers and cook for another 3-4 minutes for medium. During the last minute of cooking, place a slice of cheese on each patty and cover the pan to melt the cheese.

Step 6: Toast the buns - While cheese is melting, toast the burger buns cut-side down in the pan for 1-2 minutes until golden.

Step 7: Assemble - Place cheeseburgers on bottom buns, add desired toppings, and cover with top buns.

Step 8: Serve immediately with your favorite sides!

PRO TIPS:
• Don't overwork the meat when forming patties
• Make the patties slightly larger than your buns as they shrink during cooking
• Let the burgers rest for 1-2 minutes before serving
• For medium-rare, cook 3 minutes per side; for well-done, cook 5 minutes per side";
            }
            else if (msg.Contains("pasta"))
            {
                fallbackText = "Quick Pasta Recipe:\n\nStep 1: Boil salted water (10 mins)\nStep 2: Cook pasta according to package (8-10 mins)\nStep 3: Meanwhile, sauté garlic in olive oil (2 mins)\nStep 4: Toss pasta with garlic oil, add parmesan\n\nTip: Save pasta water to adjust sauce consistency!";
            }
            else if (msg.Contains("chicken"))
            {
                fallbackText = "Simple Chicken Recipe:\n\nStep 1: Season chicken with salt and pepper\nStep 2: Heat oil in pan over medium-high (2 mins)\nStep 3: Cook chicken 6-7 mins per side\nStep 4: Rest for 5 mins before serving\n\nInternal temp should reach 165°F!";
            }
            else if (msg.Contains("egg"))
            {
                fallbackText = "Perfect Scrambled Eggs:\n\nStep 1: Beat eggs with a splash of milk\nStep 2: Heat butter in pan on low heat\nStep 3: Pour eggs, stir gently with spatula\nStep 4: Remove from heat while slightly wet\n\nSecret: Low and slow is key!";
            }
            else
            {
                fallbackText = "Quick & Easy Recipe:\n\nIngredients:\n- Your choice of protein\n- Fresh vegetables\n- Olive oil\n- Salt & pepper\n\nSteps:\n1. Season and prep ingredients\n2. Heat oil in pan\n3. Cook protein until done\n4. Add vegetables, cook until tender\n5. Season to taste and serve!";
            }

            return new ChatResponse
            {
                Model = model,
                CreatedAt = DateTime.UtcNow,
                Recipe = fallbackText,
                Done = true
            };
        }
    }
}