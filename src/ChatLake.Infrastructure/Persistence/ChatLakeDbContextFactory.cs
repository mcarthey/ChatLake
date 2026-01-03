using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace ChatLake.Infrastructure.Persistence;

public class ChatLakeDbContextFactory
    : IDesignTimeDbContextFactory<ChatLakeDbContext>
{
    public ChatLakeDbContext CreateDbContext(string[] args)
    {
        // dotnet ef sets the working directory to the startup project
        var basePath = Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("ChatLake");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ChatLake' not found.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ChatLakeDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ChatLakeDbContext(optionsBuilder.Options);
    }
}
