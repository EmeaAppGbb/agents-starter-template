using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.

builder.Services.AddTransient(CreateKernel);
builder.Services.AddTransient(CreateMemory);
// builder.Services.AddHttpClient();
// builder.Services.AddControllers();
// builder.Services.AddApplicationInsightsTelemetry();
// builder.Services.AddSwaggerGen();
// builder.Services.AddSignalR();
// builder.Services.AddSingleton<ISignalRService, SignalRService>();

// // Allow any CORS origin if in DEV
// const string AllowDebugOriginPolicy = "AllowDebugOrigin";
// if (builder.Environment.IsDevelopment())
// {
//     builder.Services.AddCors(options =>
//     {
//         options.AddPolicy(AllowDebugOriginPolicy, builder => {
//                 builder
//                 .WithOrigins("http://localhost:3000") // client url
//                 .AllowAnyHeader()
//                 .AllowAnyMethod()
//                 .AllowCredentials();
//             });
//     });
// }

// builder.Services.AddOptions<OpenAIOptions>()
//     .Configure<IConfiguration>((settings, configuration) =>
//     {
//         configuration.GetSection(nameof(OpenAIOptions)).Bind(settings);
//     })
//     .ValidateDataAnnotations()
//     .ValidateOnStart();

// builder.Services.AddOptions<QdrantOptions>()
//     .Configure<IConfiguration>((settings, configuration) =>
//     {
//         configuration.GetSection(nameof(QdrantOptions)).Bind(settings);
//     })
//     .ValidateDataAnnotations()
//     .ValidateOnStart();

// builder.Host.UseOrleans(siloBuilder =>
// {
//     siloBuilder.UseLocalhostClustering()
//                .AddMemoryStreams("StreamProvider")
//                .AddMemoryGrainStorage("PubSubStore")
//                .AddMemoryGrainStorage("messages");
//     siloBuilder.UseInMemoryReminderService();
//     siloBuilder.UseDashboard(x => x.HostSelf = true);
// });

// builder.Services.Configure<JsonSerializerOptions>(options =>
// {
//     options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
// });




// var app = builder.Build();

// // Configure the HTTP request pipeline.
// app.UseExceptionHandler();


// app.MapDefaultEndpoints();

// app.Run();

static ISemanticTextMemory CreateMemory(IServiceProvider provider)
{
    var openAiConfig = provider.GetService<IOptions<OpenAIOptions>>().Value;
    var qdrantConfig = provider.GetService<IOptions<QdrantOptions>>().Value;

    var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder
            .SetMinimumLevel(LogLevel.Debug)
            .AddConsole()
            .AddDebug();
    });

    var memoryBuilder = new MemoryBuilder();
    return memoryBuilder.WithLoggerFactory(loggerFactory)
                 .WithQdrantMemoryStore(qdrantConfig.Endpoint, qdrantConfig.VectorSize)
                 .WithAzureOpenAITextEmbeddingGeneration(openAiConfig.EmbeddingsDeploymentOrModelId, openAiConfig.EmbeddingsEndpoint, openAiConfig.EmbeddingsApiKey)
                 .Build();
}

static Kernel CreateKernel(IServiceProvider provider)
{
    var openAiConfig = provider.GetService<IOptions<OpenAIOptions>>().Value;
    var clientOptions = new OpenAIClientOptions();
    clientOptions.Retry.NetworkTimeout = TimeSpan.FromMinutes(5);
    var builder = Kernel.CreateBuilder();
    builder.Services.AddLogging(c => c.AddConsole().AddDebug().SetMinimumLevel(LogLevel.Debug));

    // Chat
    var openAIClient = new OpenAIClient(new Uri(openAiConfig.ChatEndpoint), new AzureKeyCredential(openAiConfig.ChatApiKey), clientOptions);
    if (openAiConfig.ChatEndpoint.Contains(".azure", StringComparison.OrdinalIgnoreCase))
    {
        builder.Services.AddAzureOpenAIChatCompletion(openAiConfig.ChatDeploymentOrModelId, openAIClient);
    }
    else
    {

        builder.Services.AddOpenAIChatCompletion(openAiConfig.ChatDeploymentOrModelId, openAIClient);
    }

    // Text to Image
    openAIClient = new OpenAIClient(new Uri(openAiConfig.ImageEndpoint), new AzureKeyCredential(openAiConfig.ImageApiKey), clientOptions);
    if (openAiConfig.ImageEndpoint.Contains(".azure", StringComparison.OrdinalIgnoreCase))
    {
        builder.Services.AddAzureOpenAITextToImage(openAiConfig.ImageDeploymentOrModelId, openAIClient);
    }
    else
    {
        builder.Services.AddOpenAITextToImage(openAiConfig.ImageApiKey);
    }

    // Embeddings
    openAIClient = new OpenAIClient(new Uri(openAiConfig.EmbeddingsEndpoint), new AzureKeyCredential(openAiConfig.EmbeddingsApiKey), clientOptions);
    if (openAiConfig.EmbeddingsEndpoint.Contains(".azure", StringComparison.OrdinalIgnoreCase))
    {  
        builder.Services.AddAzureOpenAITextEmbeddingGeneration(openAiConfig.EmbeddingsDeploymentOrModelId, openAIClient);
    }
    else
    {       
        builder.Services.AddOpenAITextEmbeddingGeneration(openAiConfig.EmbeddingsDeploymentOrModelId, openAIClient);
    }

    builder.Services.ConfigureHttpClientDefaults(c =>
    {
        c.AddStandardResilienceHandler().Configure(o =>
        {
            o.Retry.MaxRetryAttempts = 5;
            o.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
        });
    });
    return builder.Build();
}