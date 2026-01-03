using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ChatLake.Core.Services;
using ChatLake.Infrastructure.Importing.Services;
using ChatLake.Infrastructure.Conversations.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("ChatLake")
    ?? throw new InvalidOperationException("Connection string 'ChatLake' not found.");

builder.Services.AddDbContext<ChatLakeDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IImportBatchService, ImportBatchService>();
builder.Services.AddScoped<IRawArtifactService, RawArtifactService>();
builder.Services.AddScoped<IImportOrchestrator, ImportOrchestrator>();
builder.Services.AddScoped<IConversationIngestionService, ConversationIngestionService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

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
app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
