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
        // RapidAPI Client
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

        services.AddHttpClient("HuggingFaceClient")
         .ConfigureHttpClient(client =>
         {
             var baseUrl = context.Configuration["HUGGINGFACE_BASE_URL"];
             if (!string.IsNullOrEmpty(baseUrl))
                 client.BaseAddress = new Uri(baseUrl);

             var apiKey = context.Configuration["HUGGINGFACE_API_KEY"];
             if (!string.IsNullOrEmpty(apiKey))
                 client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
         })
         .ConfigurePrimaryHttpMessageHandler(() =>
         {
             // Force SocketsHttpHandler with SSL bypass for local dev
             return new SocketsHttpHandler
             {
                 SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                 {
                     RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => true
                 }
             };
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
        services.AddSingleton<IModelChatClient, ModelChatClient>();
    })
    .Build();

host.Run();
