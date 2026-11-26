using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpinionesExtraccion.Data.Interfaces;
using OpinionesExtraccion.Data.Models.Dw;
using OpinionesExtraccion.Data.Transformers;

namespace OpinionesExtraccion.Worker;

/// <summary>
/// Worker principal del proceso ETL.
/// Fase E (Extracción): lee las tres fuentes en paralelo.
/// Fase T (Transformación): convierte cada registro al modelo del DW.
/// Fase L (Carga): llama a los stored procedures del DW mediante ADO.NET.
/// Al finalizar muestra un resumen con procesados, insertados, duplicados y rechazados.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ISurveyCsvRepository _surveyCsvRepository;
    private readonly IWebReviewRepository _webReviewRepository;
    private readonly ISocialCommentApiRepository _socialCommentApiRepository;
    private readonly ITemporaryFileRepository _temporaryFileRepository;
    private readonly IOpinionDwRepository _opinionDwRepository;
    private readonly IHostApplicationLifetime _applicationLifetime;

    public Worker(
        ILogger<Worker> logger,
        ISurveyCsvRepository surveyCsvRepository,
        IWebReviewRepository webReviewRepository,
        ISocialCommentApiRepository socialCommentApiRepository,
        ITemporaryFileRepository temporaryFileRepository,
        IOpinionDwRepository opinionDwRepository,
        IHostApplicationLifetime applicationLifetime)
    {
        _logger = logger;
        _surveyCsvRepository = surveyCsvRepository;
        _webReviewRepository = webReviewRepository;
        _socialCommentApiRepository = socialCommentApiRepository;
        _temporaryFileRepository = temporaryFileRepository;
        _opinionDwRepository = opinionDwRepository;
        _applicationLifetime = applicationLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var totalStopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("=========================================================");
            _logger.LogInformation("  INICIO DEL PROCESO ETL - Sistema Opiniones de Clientes");
            _logger.LogInformation("=========================================================");

            // Ejecutar las tres fuentes en paralelo (Extracción + Transformación + Carga)
            var results = await Task.WhenAll(
                ExtractTransformLoadSurveysAsync(stoppingToken),
                ExtractTransformLoadWebReviewsAsync(stoppingToken),
                ExtractTransformLoadSocialCommentsAsync(stoppingToken));

            totalStopwatch.Stop();

            // Resumen final
            LogResumen(results, totalStopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning("El proceso ETL fue cancelado.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ocurrió un error durante el proceso ETL.");
        }
        finally
        {
            _logger.LogInformation("El proceso finalizará en 10 segundos.");
            await Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None);
            _applicationLifetime.StopApplication();
        }
    }

    // ================================================================
    // FUENTE 1 — CSV (Encuestas internas)
    // ================================================================
    private async Task<EtlLoadResult> ExtractTransformLoadSurveysAsync(CancellationToken cancellationToken)
    {
        var result = new EtlLoadResult { Fuente = "CSV (Encuestas)" };
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("[CSV] Iniciando extracción de encuestas.");

        // E — Extraer
        var surveys = await _surveyCsvRepository.GetSurveysAsync(cancellationToken);
        await _temporaryFileRepository.SaveJsonAsync("surveys-csv.json", surveys, cancellationToken);
        result.Procesados = surveys.Count;
        _logger.LogInformation("[CSV] Encuestas extraídas: {Count}", surveys.Count);

        // T — Transformar
        var records = SurveyTransformer.TransformAll(surveys).ToList();
        int rechazadosTransform = surveys.Count - records.Count;

        // L — Cargar vía stored procedures (ADO.NET)
        foreach (var record in records)
        {
            int res = await _opinionDwRepository.LoadOpinionAsync(record, cancellationToken);
            if (res == 1)       result.Insertados++;
            else if (res == 0)  result.Duplicados++;
            else                result.Rechazados++;
        }

        result.Rechazados += rechazadosTransform;
        result.ElapsedMs   = stopwatch.ElapsedMilliseconds;

        _logger.LogInformation("[CSV] Carga finalizada en {Ms} ms.", result.ElapsedMs);
        return result;
    }

    // ================================================================
    // FUENTE 2 — Base de datos relacional (Reseñas web)
    // ================================================================
    private async Task<EtlLoadResult> ExtractTransformLoadWebReviewsAsync(CancellationToken cancellationToken)
    {
        var result = new EtlLoadResult { Fuente = "BD (Reseñas web)" };
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("[BD] Iniciando extracción de reseñas web.");

        // E — Extraer
        var reviews = await _webReviewRepository.GetWebReviewsAsync(cancellationToken);
        await _temporaryFileRepository.SaveJsonAsync("webreviews-db.json", reviews, cancellationToken);
        result.Procesados = reviews.Count;
        _logger.LogInformation("[BD] Reseñas web extraídas: {Count}", reviews.Count);

        // T — Transformar
        var records = WebReviewTransformer.TransformAll(reviews).ToList();
        int rechazadosTransform = reviews.Count - records.Count;

        // L — Cargar vía stored procedures (ADO.NET)
        foreach (var record in records)
        {
            int res = await _opinionDwRepository.LoadOpinionAsync(record, cancellationToken);
            if (res == 1)       result.Insertados++;
            else if (res == 0)  result.Duplicados++;
            else                result.Rechazados++;
        }

        result.Rechazados += rechazadosTransform;
        result.ElapsedMs   = stopwatch.ElapsedMilliseconds;

        _logger.LogInformation("[BD] Carga finalizada en {Ms} ms.", result.ElapsedMs);
        return result;
    }

    // ================================================================
    // FUENTE 3 — API REST (Comentarios de redes sociales)
    // ================================================================
    private async Task<EtlLoadResult> ExtractTransformLoadSocialCommentsAsync(CancellationToken cancellationToken)
    {
        var result = new EtlLoadResult { Fuente = "API (Redes sociales)" };
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("[API] Iniciando extracción de comentarios sociales.");

        // E — Extraer
        var comments = await _socialCommentApiRepository.GetSocialCommentsAsync(cancellationToken);
        await _temporaryFileRepository.SaveJsonAsync("socialcomments-api.json", comments, cancellationToken);
        result.Procesados = comments.Count;
        _logger.LogInformation("[API] Comentarios extraídos: {Count}", comments.Count);

        // T — Transformar
        var records = SocialCommentTransformer.TransformAll(comments).ToList();
        int rechazadosTransform = comments.Count - records.Count;

        // L — Cargar vía stored procedures (ADO.NET)
        foreach (var record in records)
        {
            int res = await _opinionDwRepository.LoadOpinionAsync(record, cancellationToken);
            if (res == 1)       result.Insertados++;
            else if (res == 0)  result.Duplicados++;
            else                result.Rechazados++;
        }

        result.Rechazados += rechazadosTransform;
        result.ElapsedMs   = stopwatch.ElapsedMilliseconds;

        _logger.LogInformation("[API] Carga finalizada en {Ms} ms.", result.ElapsedMs);
        return result;
    }

    // ================================================================
    // Resumen final del proceso ETL
    // ================================================================
    private void LogResumen(EtlLoadResult[] results, long totalMs)
    {
        int totalProcesados = 0, totalInsertados = 0, totalDuplicados = 0, totalRechazados = 0;

        _logger.LogInformation("=========================================================");
        _logger.LogInformation("  RESUMEN DEL PROCESO ETL");
        _logger.LogInformation("=========================================================");
        _logger.LogInformation("{Fuente,-25} {Proc,10} {Ins,10} {Dup,10} {Rech,10} {Ms,10}",
            "Fuente", "Procesados", "Insertados", "Duplicados", "Rechazados", "Tiempo(ms)");
        _logger.LogInformation(new string('-', 80));

        foreach (var r in results)
        {
            _logger.LogInformation("{Fuente,-25} {Proc,10} {Ins,10} {Dup,10} {Rech,10} {Ms,10}",
                r.Fuente, r.Procesados, r.Insertados, r.Duplicados, r.Rechazados, r.ElapsedMs);
            totalProcesados  += r.Procesados;
            totalInsertados  += r.Insertados;
            totalDuplicados  += r.Duplicados;
            totalRechazados  += r.Rechazados;
        }

        _logger.LogInformation(new string('-', 80));
        _logger.LogInformation("{Fuente,-25} {Proc,10} {Ins,10} {Dup,10} {Rech,10} {Ms,10}",
            "TOTAL", totalProcesados, totalInsertados, totalDuplicados, totalRechazados, totalMs);
        _logger.LogInformation("=========================================================");
    }
}
