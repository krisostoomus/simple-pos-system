using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Abstractions;
using Pos.Application.Catalog;
using Pos.Application.Checkout;
using Pos.Application.Reporting;
using Pos.Infrastructure.Auth;
using Pos.Infrastructure.Payments;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.RealTime;
using Pos.Infrastructure.Reporting;
using Pos.Infrastructure.Repositories;
using Pos.Infrastructure.Seeding;

namespace Pos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<PosDbContext>(o => o.UseNpgsql(config.GetConnectionString("Postgres")));

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IReportQueries, ReportQueries>();
        services.AddScoped<IStockNotifier, SignalRStockNotifier>();
        services.AddSingleton<IPaymentService, CashPaymentService>();
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<TokenService>();

        // Application use-case services (composition root).
        services.AddScoped<CheckoutService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<ReportingService>();

        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName);
        services.AddOptions<StaffCredentialOptions>().BindConfiguration(StaffCredentialOptions.SectionName);

        return services;
    }
}
