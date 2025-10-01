using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;
using static Prog7314_Recipe_LLaMA.Models;

namespace Prog7314_Recipe_LLaMA
{
    public class RecipeFunction
    {
        private readonly IRapidRecipeClient _rapidClient;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public RecipeFunction(IRapidRecipeClient rapidClient)
        {
            _rapidClient = rapidClient;
        }

        private static async Task<HttpResponseData> CreateJsonResponse(HttpRequestData req, object payload, HttpStatusCode code = HttpStatusCode.OK)
        {
            var response = req.CreateResponse(code);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(payload, JsonOptions));
            return response;
        }

        private static string? GetQuery(HttpRequestData req, params string[] keys)
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            foreach (var key in keys)
            {
                var value = query.Get(key);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
            return null;
        }
        

        [Function("SearchRecipes")]
        public async Task<HttpResponseData> Search(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/search")] HttpRequestData req)
        {
            try
            {
                var requestObj = new RecipeSearchRequest(
                    SearchQuery: GetQuery(req, "searchQuery", "query") ?? string.Empty,
                    Ingredients: GetQuery(req, "ingredients", "includeIngredients") ?? string.Empty,
                    Diet: GetQuery(req, "diet") ?? string.Empty,
                    Cuisine: GetQuery(req, "cuisine") ?? string.Empty,
                    ResultsAmount: int.TryParse(GetQuery(req, "resultsAmount", "number"), out var n) ? n : 10
                );

                var results = await _rapidClient.ComplexSearchAsync(requestObj).ConfigureAwait(false);
                return await CreateJsonResponse(req, results);
            }
            catch (Exception ex)
            {
                return await CreateJsonResponse(req, new { error = ex.Message }, HttpStatusCode.InternalServerError);
            }
        }

        [Function("GetRecipe")]
        public async Task<HttpResponseData> GetRecipe(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/{recipeId:int}/details")] HttpRequestData req,
            int recipeId)
        {
            try
            {
                var details = await _rapidClient.GetRecipeAsync(recipeId).ConfigureAwait(false);
                return await CreateJsonResponse(req, details);
            }
            catch (Exception ex)
            {
                return await CreateJsonResponse(req, new { error = ex.Message }, HttpStatusCode.InternalServerError);
            }
        }

        [Function("GetRandomRecipes")]
        public async Task<HttpResponseData> GetRandom(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/random")] HttpRequestData req)
        {
            var tags = GetQuery(req, "tags") ?? string.Empty;
            var number = int.TryParse(GetQuery(req, "number"), out var n) ? n : 1;

            var results = await _rapidClient.GetRandomAsync(tags, number).ConfigureAwait(false);
            return await CreateJsonResponse(req, results);
        }

        [Function("GetSimilarRecipes")]
        public async Task<HttpResponseData> GetSimilar(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/similar")] HttpRequestData req)
        {
            if (!int.TryParse(GetQuery(req, "recipeId"), out var recipeId))
                return await CreateJsonResponse(req, new { error = "Missing or invalid recipeId" }, HttpStatusCode.BadRequest);

            var number = int.TryParse(GetQuery(req, "number"), out var n) ? n : 5;

            var results = await _rapidClient.GetSimilarAsync(recipeId, number).ConfigureAwait(false);
            return await CreateJsonResponse(req, results);
        }

        [Function("GetVeganRecipes")]
        public async Task<HttpResponseData> GetVegan(
    [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/vegan")] HttpRequestData req)
        {
            var number = int.TryParse(GetQuery(req, "number"), out var n) ? n : 10;
            var results = await _rapidClient.GetVeganRecipesAsync(number).ConfigureAwait(false);
            return await CreateJsonResponse(req, results);
        }

        [Function("GetVegetarianRecipes")]
        public async Task<HttpResponseData> GetVegetarian(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/vegetarian")] HttpRequestData req)
        {
            var number = int.TryParse(GetQuery(req, "number"), out var n) ? n : 10;
            var results = await _rapidClient.GetVegetarianRecipesAsync(number).ConfigureAwait(false);
            return await CreateJsonResponse(req, results);
        }

        [Function("GetChickenRecipes")]
        public async Task<HttpResponseData> GetChicken(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/chicken")] HttpRequestData req)
        {
            var number = int.TryParse(GetQuery(req, "number"), out var n) ? n : 10;
            var results = await _rapidClient.GetChickenRecipesAsync(number).ConfigureAwait(false);
            return await CreateJsonResponse(req, results);
        }

        [Function("GetFishRecipes")]
        public async Task<HttpResponseData> GetFish(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/fish")] HttpRequestData req)
        {
            var number = int.TryParse(GetQuery(req, "number"), out var n) ? n : 10;
            var results = await _rapidClient.GetFishRecipesAsync(number).ConfigureAwait(false);
            return await CreateJsonResponse(req, results);
        }

        [Function("GetDessertRecipes")]
        public async Task<HttpResponseData> GetDesserts(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/desserts")] HttpRequestData req)
        {
            var number = int.TryParse(GetQuery(req, "number"), out var n) ? n : 10;
            var results = await _rapidClient.GetDessertRecipesAsync(number).ConfigureAwait(false);
            return await CreateJsonResponse(req, results);
        }

        [Function("GetHalaalRecipes")]
        public async Task<HttpResponseData> GetHalaal(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/halaal")] HttpRequestData req)
        {
            var number = int.TryParse(GetQuery(req, "number"), out var n) ? n : 10;
            var results = await _rapidClient.GetHalaalRecipesAsync(number).ConfigureAwait(false);
            return await CreateJsonResponse(req, results);
        }

        [Function("GetCuisineRecipes")]
        public async Task<HttpResponseData> GetCuisine(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/cuisine/{cuisine}")] HttpRequestData req,
            string cuisine)
        {
            var number = int.TryParse(GetQuery(req, "number"), out var n) ? n : 10;
            var results = await _rapidClient.GetCuisineRecipesAsync(cuisine, number).ConfigureAwait(false);
            return await CreateJsonResponse(req, results);
        }

        [Function("GetQuickRecipes")]
        public async Task<HttpResponseData> GetQuick(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recipes/quick/{minutes:int?}")] HttpRequestData req,
            int? minutes)
        {
            var number = int.TryParse(GetQuery(req, "number"), out var n) ? n : 10;
            var maxMinutes = minutes ?? 30; 
            var results = await _rapidClient.GetQuickRecipesAsync(maxMinutes, number).ConfigureAwait(false);
            return await CreateJsonResponse(req, results);
        }

    }
}
