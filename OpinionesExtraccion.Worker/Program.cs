using Microsoft.Extensions.Hosting;
using OpinionesExtraccion.Data.Interfaces;
using OpinionesExtraccion.Data.Repositories.Api;
using OpinionesExtraccion.Data.Repositories.Csv;
using OpinionesExtraccion.Data.Repositories.Db;
using OpinionesExtraccion.Data.Repositories.Dw;
using OpinionesExtraccion.Data.Repositories.Files;
using OpinionesExtraccion.Worker;

var builder = Host.CreateApplicationBuilder(args);

var configuration = builder.Configuration;

// --- Connection string al Data Warehouse ---
var connectionString = configuration.GetConnectionString("AnalisisOpinionesDb");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("No se encontró la cadena de conexión AnalisisOpinionesDb.");

// --- Repositorio de extracción desde BD relacional (Reseñas web) ---
builder.Services.AddSingleton<IWebReviewRepository>(_ =>
    new WebReviewRepository(connectionString));

// --- Repositorio de carga al Data Warehouse (Stored Procedures via ADO.NET) ---
builder.Services.AddSingleton<IOpinionDwRepository>(_ =>
    new OpinionDwRepository(connectionString));

// --- Repositorio de extracción desde API REST (Comentarios sociales) ---
var apiBaseUrl = configuration["ApiSettings:BaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
    throw new InvalidOperationException("No se encontró ApiSettings:BaseUrl.");

builder.Services.AddHttpClient<ISocialCommentApiRepository, SocialCommentApiRepository>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout     = TimeSpan.FromSeconds(60);
});

// --- Repositorio de extracción desde CSV (Encuestas) ---
var csvRelativePath = configuration["CsvSettings:SurveysPath"];
if (string.IsNullOrWhiteSpace(csvRelativePath))
    throw new InvalidOperationException("No se encontró CsvSettings:SurveysPath.");

builder.Services.AddSingleton<ISurveyCsvRepository>(serviceProvider =>
{
    var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
    var fullPath    = Path.Combine(environment.ContentRootPath, csvRelativePath);
    return new SurveyCsvRepository(fullPath);
});

// --- Repositorio de archivos temporales JSON ---
var tempDirectory = configuration["TemporaryFiles:Directory"];
if (string.IsNullOrWhiteSpace(tempDirectory))
    throw new InvalidOperationException("No se encontró TemporaryFiles:Directory.");

builder.Services.AddSingleton<ITemporaryFileRepository>(serviceProvider =>
{
    var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
    var fullPath    = Path.Combine(environment.ContentRootPath, tempDirectory);
    return new JsonTemporaryFileRepository(fullPath);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
