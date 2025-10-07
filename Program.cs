using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prog7314_Recipe_LLaMA;
using System;
using System.Net.Http;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((ctx, config) =>
    {
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        // RapidAPI Client for recipes
        services.AddHttpClient("RapidApiClient", client =>
        {
            var baseUrl = context.Configuration["RECIPE_BASE_URL"];
            var apiKey = context.Configuration["RAPIDAPI_KEY"];
            var apiHost = context.Configuration["RAPIDAPI_HOST"];

            if (!string.IsNullOrEmpty(baseUrl))
                client.BaseAddress = new Uri(baseUrl);

            if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiHost))
            {
                client.DefaultRequestHeaders.Add("x-rapidapi-key", apiKey);
                client.DefaultRequestHeaders.Add("x-rapidapi-host", apiHost);
            }
        });

        // Hugging Face Client for Gradio Space
        services.AddHttpClient("HuggingFaceClient", client =>
        {
            var spaceUrl = context.Configuration["HUGGINGFACE_SPACE_URL"]
                          ?? "https://samkelo28-chefgpt3.hf.space";
            
            client.BaseAddress = new Uri(spaceUrl);
            client.Timeout = TimeSpan.FromSeconds(120);
            
            // Standard headers
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", "AzureFunction-ChefGPT/1.0");
        });

        // Caching
        var redisConnection = context.Configuration.GetConnectionString("RedisCache")
                             ?? context.Configuration["REDIS_CONNECTION_STRING"];

        if (!string.IsNullOrEmpty(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "Prog7314_Recipe_LLaMA_";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        // App services
        services.AddSingleton<IRapidRecipeClient, RapidRecipeClient>();
        services.AddSingleton<ModelChatClient>(); 
    })
    .Build();

host.Run();
