using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ChatLake.Core.Services;
using ChatLake.Infrastructure.Importing.Services;
using ChatLake.Infrastructure.Conversations.Services;
using ChatLake.Core.Parsing;
using ChatLake.Infrastructure.Parsing;
using ChatLake.Infrastructure.Projects.Services;
using ChatLake.Infrastructure.Gold.Services;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("ChatLake")
    ?? throw new InvalidOperationException("Connection string 'ChatLake' not found.");

builder.Services.AddDbContext<ChatLakeDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IImportBatchService, ImportBatchService>();
builder.Services.AddScoped<IRawArtifactService, RawArtifactService>();
builder.Services.AddScoped<IImportOrchestrator, ImportOrchestrator>();
builder.Services.AddScoped<IImportCleanupService, ImportCleanupService>();
builder.Services.AddScoped<IConversationIngestionService, ConversationIngestionService>();
builder.Services.AddScoped<IRawArtifactParserResolver, RawArtifactParserResolver>();
builder.Services.AddScoped<IngestionPipeline>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IConversationQueryService, ConversationQueryService>();
builder.Services.AddScoped<IConversationSummaryBuilder, ConversationSummaryBuilder>();
builder.Services.AddScoped<IInferenceRunService, InferenceRunService>();
builder.Services.AddSingleton<ILlmService>(new OllamaService("http://localhost:11434", "mistral:7b"));
builder.Services.AddScoped<ISegmentationService, SegmentationService>();
builder.Services.AddScoped<IEmbeddingCacheService, EmbeddingCacheService>();
builder.Services.AddScoped<IClusteringService, ClusteringService>();
builder.Services.AddScoped<IProjectSuggestionService, ProjectSuggestionService>();
builder.Services.AddScoped<ITopicExtractionService, TopicExtractionService>();
builder.Services.AddScoped<IDriftDetectionService, DriftDetectionService>();
builder.Services.AddScoped<ISimilarityService, SimilarityService>();
builder.Services.AddScoped<IBlogSuggestionService, BlogSuggestionService>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1024L * 1024L * 1024L; // 1 GB
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1024L * 1024L * 1024L; // 1 GB
});

var app = builder.Build();

// Clean up any stale "Running" inference runs from previous crashes
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatLakeDbContext>();
    var staleRuns = await db.InferenceRuns
        .Where(r => r.Status == "Running")
        .ToListAsync();

    if (staleRuns.Any())
    {
        foreach (var run in staleRuns)
        {
            run.Status = "Failed";
            run.CompletedAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"[Startup] Cleaned up {staleRuns.Count} stale 'Running' inference runs");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Intentionally no automatic migrations here.
    // Migrations are applied explicitly during development.
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
