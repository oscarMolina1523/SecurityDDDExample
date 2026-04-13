using DDDExample.Application.Interfaces;
using DDDExample.Application.Services;
using DDDExample.Domain.Repositories;
using DDDExample.Infrastructure.Persistence.MongoDB;
using DDDExample.Infrastructure.Persistence.SqlServer;
using DDDExample.Infrastructure.Repositories.MongoDB;
using DDDExample.Infrastructure.Repositories.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DDDExample.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure SQL Server settings
        var sqlServerSettings = configuration.GetSection("SQLServerSettings").Get<SqlServerSettings>() 
            ?? throw new InvalidOperationException("SQLServerSettings configuration section is missing or invalid");
        
        // Configure SQL Server for Products
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                sqlServerSettings.ConnectionString,
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));
        
        // Register SQL Server Product Repository
        services.AddScoped<IRepository<Domain.Entities.Product, Guid>, SqlProductRepository>();

        // Configure MongoDB settings
        var mongoDbSettings = configuration.GetSection("MongoDBSettings").Get<MongoDbSettings>()
            ?? throw new InvalidOperationException("MongoDBSettings configuration section is missing or invalid");

        // Register MongoDB context with settings
        services.AddSingleton<MongoDbContext>(_ =>
            new MongoDbContext(Options.Create(mongoDbSettings)));

        // Register MongoDB Category Repository
        services.AddScoped<IRepository<Domain.Entities.Category, string>>(sp =>
            new MongoCategoryRepository(sp.GetRequiredService<MongoDbContext>()));

        return services;
    }
}
