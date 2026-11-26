using OpinionesExtraccion.Data.Models.Db;
using OpinionesExtraccion.Data.Models.Dw;

namespace OpinionesExtraccion.Data.Transformers;

/// <summary>
/// Transforma reseñas web (base de datos relacional) al formato del Data Warehouse.
/// La clasificación se deriva del campo Rating: 1-2 Negativa, 3 Neutra, 4-5 Positiva.
/// </summary>
public static class WebReviewTransformer
{
    /// <summary>
    /// Convierte un <see cref="WebReviewRecord"/> en un <see cref="OpinionDwRecord"/>.
    /// Devuelve null si el registro no pasa las validaciones mínimas.
    /// </summary>
    public static OpinionDwRecord? Transform(WebReviewRecord source)
    {
        if (string.IsNullOrWhiteSpace(source.Comentario)) return null;
        if (source.IdProducto <= 0)                        return null;
        if (source.Fecha == default)                       return null;

        int? puntaje = source.Rating is >= 1 and <= 5 ? source.Rating : null;

        int clasificacionKey = source.Rating switch
        {
            >= 4 => 3,  // Positiva
            3    => 2,  // Neutra
            _    => 1,  // Negativa (1 o 2)
        };

        return new OpinionDwRecord
        {
            SistemaOrigen       = "Web",
            IdOpinionOriginal   = source.IdOpinion.ToString(),
            Fecha               = source.Fecha,
            IdClienteOriginal   = source.IdCliente?.ToString(),
            IdProductoOriginal  = source.IdProducto.ToString(),
            Plataforma          = "Sitio web",
            ClasificacionKey    = clasificacionKey,
            Comentario          = source.Comentario.Trim(),
            PuntajeSatisfaccion = puntaje,
        };
    }

    public static IEnumerable<OpinionDwRecord> TransformAll(IEnumerable<WebReviewRecord> records)
        => records.Select(Transform).Where(r => r is not null)!;
}
