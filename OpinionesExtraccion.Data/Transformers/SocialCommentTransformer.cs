using OpinionesExtraccion.Data.Models.Api;
using OpinionesExtraccion.Data.Models.Dw;

namespace OpinionesExtraccion.Data.Transformers;

/// <summary>
/// Transforma comentarios de redes sociales (API REST) al formato del Data Warehouse.
/// No hay rating ni clasificación disponible → ClasificacionKey = 4 (Sin clasificar).
/// La plataforma se resuelve del campo Fuente del registro.
/// </summary>
public static class SocialCommentTransformer
{
    private static readonly Dictionary<string, string> PlataformaMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Instagram"] = "Instagram",
            ["Facebook"]  = "Facebook",
            ["Twitter"]   = "Twitter",
            ["X"]         = "Twitter",
        };

    /// <summary>
    /// Convierte un <see cref="SocialCommentRecord"/> en un <see cref="OpinionDwRecord"/>.
    /// Devuelve null si el registro no pasa las validaciones mínimas.
    /// </summary>
    public static OpinionDwRecord? Transform(SocialCommentRecord source)
    {
        if (string.IsNullOrWhiteSpace(source.Comentario)) return null;
        if (source.IdProducto <= 0)                        return null;
        if (source.Fecha == default)                       return null;

        string plataforma = PlataformaMap.TryGetValue(
            source.Fuente?.Trim() ?? string.Empty, out string? p) ? p : "Instagram";

        return new OpinionDwRecord
        {
            SistemaOrigen       = "Social",
            IdOpinionOriginal   = source.IdOpinion.ToString(),
            Fecha               = source.Fecha,
            IdClienteOriginal   = source.IdCliente?.ToString(),
            IdProductoOriginal  = source.IdProducto.ToString(),
            Plataforma          = plataforma,
            ClasificacionKey    = 4,  // Sin clasificar
            Comentario          = source.Comentario.Trim(),
            PuntajeSatisfaccion = null,
        };
    }

    public static IEnumerable<OpinionDwRecord> TransformAll(IEnumerable<SocialCommentRecord> records)
        => records.Select(Transform).Where(r => r is not null)!;
}
