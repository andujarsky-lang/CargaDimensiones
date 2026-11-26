using OpinionesExtraccion.Data.Models.Csv;
using OpinionesExtraccion.Data.Models.Dw;

namespace OpinionesExtraccion.Data.Transformers;

/// <summary>
/// Transforma registros de encuestas CSV al formato del Data Warehouse.
/// La clasificación proviene directamente del campo Clasificacion del CSV.
/// </summary>
public static class SurveyTransformer
{
    private static readonly Dictionary<string, int> ClasificacionMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Negativa"]       = 1,
            ["Neutra"]         = 2,
            ["Positiva"]       = 3,
            ["Sin clasificar"] = 4,
        };

    /// <summary>
    /// Convierte un <see cref="SurveyCsvRecord"/> en un <see cref="OpinionDwRecord"/>.
    /// Devuelve null si el registro no pasa las validaciones mínimas.
    /// </summary>
    public static OpinionDwRecord? Transform(SurveyCsvRecord source)
    {
        if (string.IsNullOrWhiteSpace(source.Comentario)) return null;
        if (source.IdProducto <= 0)                        return null;
        if (source.Fecha == default)                       return null;

        int? puntaje = null;
        if (source.PuntajeSatisfaccion is >= 1 and <= 5)
            puntaje = source.PuntajeSatisfaccion;

        int clasificacionKey = ClasificacionMap.TryGetValue(
            source.Clasificacion?.Trim() ?? string.Empty, out int ck) ? ck : 4;

        return new OpinionDwRecord
        {
            SistemaOrigen       = "Encuestas",
            IdOpinionOriginal   = source.IdOpinion.ToString(),
            Fecha               = source.Fecha,
            IdClienteOriginal   = source.IdCliente?.ToString(),
            IdProductoOriginal  = source.IdProducto.ToString(),
            Plataforma          = "Encuesta interna",
            ClasificacionKey    = clasificacionKey,
            Comentario          = source.Comentario.Trim(),
            PuntajeSatisfaccion = puntaje,
        };
    }

    public static IEnumerable<OpinionDwRecord> TransformAll(IEnumerable<SurveyCsvRecord> records)
        => records.Select(Transform).Where(r => r is not null)!;
}
