using System.Reflection;
using HealthChecks.UI.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ProjetoBanco.API.BackgroundServices;
using ProjetoBanco.API.Data;
using ProjetoBanco.API.Services;
using Serilog;
using Serilog.Events;

// ── Serilog (configuração antecipada para capturar falhas no startup) ──────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/banco-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    Log.Information("Iniciando ProjetoBanco.API...");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext()
           .WriteTo.Console()
           .WriteTo.File("logs/banco-.log", rollingInterval: RollingInterval.Day));

    // ── EF Core ───────────────────────────────────────────────────────────────
    if (builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.AddDbContext<AppDbContext>(opt =>
            opt.UseInMemoryDatabase("TestDb"));
    }
    else
    {
        builder.Services.AddDbContext<AppDbContext>(opt =>
            opt.UseOracle(builder.Configuration.GetConnectionString("Oracle")));
    }

    // ── Serviços de Negócio ───────────────────────────────────────────────────
    builder.Services.AddSingleton<IRabbitMQPublisher, RabbitMQPublisher>();
    builder.Services.AddScoped<IRegrasProdutoService, RegrasProdutoService>();
    builder.Services.AddHostedService<ContratacaoConsumerService>();

    // ── Controllers ───────────────────────────────────────────────────────────
    builder.Services.AddControllers();

    // ── Health Checks ─────────────────────────────────────────────────────────
    var healthChecks = builder.Services
        .AddHealthChecks();

    if (!builder.Environment.IsEnvironment("Testing"))
    {
        healthChecks.AddDbContextCheck<AppDbContext>("oracle-db");
    }

    // ── Swagger ───────────────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(opt =>
    {
        opt.SwaggerDoc("v1", new OpenApiInfo
        {
            Title       = "Projeto Banco — API",
            Version     = "v1",
            Description = "Backend do banco digital com mensageria RabbitMQ.\n\n" +
                          "**Produtos implementados:** Máquina de Cartão, Empréstimo\n\n" +
                          "**Trio:** 2 produtos + regras extra (score MDR variável, juros por score) + OpenTelemetry Jaeger"
        });

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath)) opt.IncludeXmlComments(xmlPath);
    });

    // ── OpenTelemetry (Trio: Jaeger + Console) ────────────────────────────────
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService("ProjetoBanco.API"))
        .WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter();

            // Jaeger — apenas quando não estiver em Testing
            if (!builder.Environment.IsEnvironment("Testing"))
            {
                tracing.AddJaegerExporter(opt =>
                {
                    opt.AgentHost = builder.Configuration["Jaeger:Host"] ?? "localhost";
                    opt.AgentPort = int.Parse(builder.Configuration["Jaeger:Port"] ?? "6831");
                });
            }
        });

    // ── CORS (desenvolvimento) ────────────────────────────────────────────────
    builder.Services.AddCors(opt =>
        opt.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    // ═════════════════════════════════════════════════════════════════════════
    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCors();

    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Projeto Banco v1");
            c.RoutePrefix = string.Empty; // Swagger na raiz
        });
    }

    app.UseRouting();
    app.UseAuthorization();
    app.MapControllers();

    // Health Check endpoint
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    Log.Information("ProjetoBanco.API iniciada com sucesso.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Falha fatal ao iniciar ProjetoBanco.API.");
}
finally
{
    Log.CloseAndFlush();
}

// Necessário para WebApplicationFactory nos testes de integração
public partial class Program { }
