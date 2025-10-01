using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using static Prog7314_Recipe_LLaMA.ModelChatClient;
using static Prog7314_Recipe_LLaMA.Models;

namespace Prog7314_Recipe_LLaMA
{
    public interface IRapidRecipeClient
    {
        Task<List<RecipeSummary>> ComplexSearchAsync(RecipeSearchRequest req);
        Task<List<RecipeSummary>> GetRandomAsync(string tags = "", int number = 1);
        Task<RecipeDetails> GetRecipeAsync(int recipeId);
        Task<List<RecipeSummary>> GetSimilarAsync(int recipeId, int number = 5);

        // Dashboard-friendly shortcuts
        Task<List<RecipeSummary>> GetVeganRecipesAsync(int number = 10);
        Task<List<RecipeSummary>> GetVegetarianRecipesAsync(int number = 10);
        Task<List<RecipeSummary>> GetBeefRecipesAsync(int number = 10);
        Task<List<RecipeSummary>> GetChickenRecipesAsync(int number = 10);
        Task<List<RecipeSummary>> GetFishRecipesAsync(int number = 10);
        Task<List<RecipeSummary>> GetDessertRecipesAsync(int number = 10);
        Task<List<RecipeSummary>> GetQuickRecipesAsync(int maxMinutes = 30, int number = 10);
        Task<List<RecipeSummary>> GetHalaalRecipesAsync(int number = 10);
        Task<List<RecipeSummary>> GetCuisineRecipesAsync(string cuisine, int number = 10);
    }


    public class RapidRecipeClient : IRapidRecipeClient
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;
        private readonly IDistributedCache _cache;
        private readonly string _rapidHost;
        private readonly string _rapidKey;
        private readonly string _baseUrl;

        public RapidRecipeClient(IHttpClientFactory httpFactory, IConfiguration config, IDistributedCache cache)
        {
            _httpFactory = httpFactory;
            _config = config;
            _cache = cache;
            _rapidHost = _config["RAPIDAPI_HOST"] ?? "spoonacular-recipe-food-nutrition-v1.p.rapidapi.com";
            _rapidKey = _config["RAPIDAPI_KEY"] ?? "";
            _baseUrl = _config["RECIPE_BASE_URL"]?.TrimEnd('/')
                       ?? "https://spoonacular-recipe-food-nutrition-v1.p.rapidapi.com";
        }

        private HttpRequestMessage BuildRequest(string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + path);
            if (!string.IsNullOrEmpty(_rapidKey)) request.Headers.Add("x-rapidapi-key", _rapidKey);
            if (!string.IsNullOrEmpty(_rapidHost)) request.Headers.Add("x-rapidapi-host", _rapidHost);
            return request;
        }

        public async Task<List<RecipeSummary>> ComplexSearchAsync(RecipeSearchRequest req)
        {
            var cacheKey = $"recipes:complex:{req.SearchQuery}:{req.Ingredients}:{req.Diet}:{req.Cuisine}:{req.ResultsAmount}".ToLowerInvariant();
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached)) return JsonSerializer.Deserialize<List<RecipeSummary>>(cached);

            var query = $"/recipes/complexSearch?query={Uri.EscapeDataString(req.SearchQuery ?? "")}" +
                        (string.IsNullOrWhiteSpace(req.Ingredients) ? "" : $"&includeIngredients={Uri.EscapeDataString(req.Ingredients)}") +
                        (string.IsNullOrWhiteSpace(req.Diet) ? "" : $"&diet={Uri.EscapeDataString(req.Diet)}") +
                        (string.IsNullOrWhiteSpace(req.Cuisine) ? "" : $"&cuisine={Uri.EscapeDataString(req.Cuisine)}") +
                        $"&number={req.ResultsAmount}&instructionsRequired=true";

            var client = _httpFactory.CreateClient("RapidApiClient");
            var resp = await client.SendAsync(BuildRequest(query));
            resp.EnsureSuccessStatusCode();

            var content = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var list = new List<RecipeSummary>();

            if (doc.RootElement.TryGetProperty("results", out var results))
            {
                foreach (var item in results.EnumerateArray())
                {
                    list.Add(new RecipeSummary
                    {
                        RecipeId = item.GetProperty("id").GetInt32(),
                        Title = item.GetProperty("title").GetString(),
                        Image = item.GetProperty("image").GetString(),
                        ReadyInMinutes = item.TryGetProperty("readyInMinutes", out var r) ? r.GetInt32() : 0,
                        Servings = item.TryGetProperty("servings", out var s) ? s.GetInt32() : 0
                    });
                }
            }

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(list),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) });

            return list;
        }



        public async Task<List<RecipeSummary>> GetRandomAsync(string tags = "", int number = 1)
        {
            var cacheKey = $"recipes:random:{tags}:{number}";
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached)) return JsonSerializer.Deserialize<List<RecipeSummary>>(cached);

            var query = $"/recipes/random?number={number}" +
                        (string.IsNullOrWhiteSpace(tags) ? "" : $"&tags={Uri.EscapeDataString(tags)}");

            var client = _httpFactory.CreateClient("RapidApiClient");
            var resp = await client.SendAsync(BuildRequest(query));
            resp.EnsureSuccessStatusCode();

            var content = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var list = new List<RecipeSummary>();

            if (doc.RootElement.TryGetProperty("recipes", out var recipes))
            {
                foreach (var item in recipes.EnumerateArray())
                {
                    list.Add(new RecipeSummary
                    {
                        RecipeId = item.GetProperty("id").GetInt32(),
                        Title = item.GetProperty("title").GetString(),
                        Image = item.GetProperty("image").GetString(),
                        ReadyInMinutes = item.TryGetProperty("readyInMinutes", out var r) ? r.GetInt32() : 0,
                        Servings = item.TryGetProperty("servings", out var s) ? s.GetInt32() : 0
                    });
                }
            }

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(list),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });

            return list;
        }

        public async Task<RecipeDetails> GetRecipeAsync(int recipeId)
        {
            var cacheKey = $"recipes:details:{recipeId}";
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached)) return JsonSerializer.Deserialize<RecipeDetails>(cached);

            var client = _httpFactory.CreateClient("RapidApiClient");
            var resp = await client.SendAsync(BuildRequest($"/recipes/{recipeId}/information"));
            resp.EnsureSuccessStatusCode();

            var content = await resp.Content.ReadAsStringAsync();
            var details = JsonSerializer.Deserialize<RecipeDetails>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(details),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6) });

            return details;
        }

        public async Task<List<RecipeSummary>> GetSimilarAsync(int recipeId, int number = 5)
        {
            var cacheKey = $"recipes:similar:{recipeId}:{number}";
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached)) return JsonSerializer.Deserialize<List<RecipeSummary>>(cached);

            var client = _httpFactory.CreateClient("RapidApiClient");
            var resp = await client.SendAsync(BuildRequest($"/recipes/{recipeId}/similar?number={number}"));
            resp.EnsureSuccessStatusCode();

            var content = await resp.Content.ReadAsStringAsync();
            var list = JsonSerializer.Deserialize<List<RecipeSummary>>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(list),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2) });

            return list;
        }

        public Task<List<RecipeSummary>> GetVeganRecipesAsync(int number = 10) =>
       ComplexSearchAsync(new RecipeSearchRequest("", "", "vegan", "", number));

        public Task<List<RecipeSummary>> GetVegetarianRecipesAsync(int number = 10) =>
            ComplexSearchAsync(new RecipeSearchRequest("", "", "vegetarian", "", number));

        public Task<List<RecipeSummary>> GetBeefRecipesAsync(int number = 10) =>
            ComplexSearchAsync(new RecipeSearchRequest("beef", "", "", "", number));

        public Task<List<RecipeSummary>> GetChickenRecipesAsync(int number = 10) =>
            ComplexSearchAsync(new RecipeSearchRequest("chicken", "", "", "", number));

        public Task<List<RecipeSummary>> GetFishRecipesAsync(int number = 10) =>
            ComplexSearchAsync(new RecipeSearchRequest("fish", "", "", "", number));

        public Task<List<RecipeSummary>> GetDessertRecipesAsync(int number = 10) =>
            GetRandomAsync("dessert", number);

        public Task<List<RecipeSummary>> GetQuickRecipesAsync(int maxMinutes = 30, int number = 10) =>
            ComplexSearchAsync(new RecipeSearchRequest("", "", "", "", number)
            {
                // You might want to extend RecipeSearchRequest to accept MaxReadyTime
                // If not, add it to the query manually here
            });

        public Task<List<RecipeSummary>> GetHalaalRecipesAsync(int number = 10) =>
            ComplexSearchAsync(new RecipeSearchRequest("halaal", "", "", "", number));

        public Task<List<RecipeSummary>> GetCuisineRecipesAsync(string cuisine, int number = 10) =>
            ComplexSearchAsync(new RecipeSearchRequest("", "", "", cuisine, number));

    }


}
