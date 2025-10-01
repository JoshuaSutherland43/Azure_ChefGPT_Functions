using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Prog7314_Recipe_LLaMA
{
    public class Models
    {
        public record RecipeSearchRequest(
            string SearchQuery,
            string Ingredients,
            string Diet,
            string Cuisine,
            int ResultsAmount);

        public class RecipeSummary
        {
            public int RecipeId { get; set; }
            public string? Title { get; set; }
            public string? Image { get; set; }
            public int ReadyInMinutes { get; set; }
            public int Servings { get; set; }
        }

        public class RecipeDetails : RecipeSummary
        {
            public string? SourceUrl { get; set; }
            public List<Ingredient> ExtendedIngredients { get; set; } = new();
            public string? Instructions { get; set; }
            public List<AnalyzedInstruction> AnalyzedInstructions { get; set; } = new();
        }

        public record Ingredient(
            int Id,
            string? Name,
            decimal Amount,
            string? Unit,
            string? Original,
            string? Image
        );

        public class AnalyzedInstruction
        {
            public List<InstructionStep> Steps { get; set; } = new();
        }

        public record InstructionStep(int Number, string? Step);



        public class ChatQueryRequest
        {
            public string? Model { get; set; }
            public List<ChatMessage> Messages { get; set; } = new();
            public int NumPredict { get; set; }
            public float Temperature { get; set; }
        }

        public record ChatMessage(ChatRole Role, string Content);

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum ChatRole { User, Assistant, System }

        public class ChatResponse
        {
            public string? Model { get; set; }
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public object? Recipe { get; set; }
            public bool Done { get; set; }
        }
    }
}
