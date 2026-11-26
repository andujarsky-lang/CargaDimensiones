namespace OpinionesExtraccion.Data.Models.Dw;

/// <summary>
/// Registro transformado listo para cargarse en el Data Warehouse (fact.Opinion).
/// Contiene los datos en crudo necesarios para resolver las claves dimensionales
/// mediante los stored procedures.
/// </summary>
public class OpinionDwRecord
{
    /// <summary>Origen del sistema: "Encuestas", "Web" o "Social".</summary>
    public string SistemaOrigen { get; set; } = string.Empty;

    /// <summary>Identificador único de la opinión en el sistema origen.</summary>
    public string IdOpinionOriginal { get; set; } = string.Empty;

    /// <summary>Fecha de la opinión (se convierte a FechaKey YYYYMMDD en el repositorio).</summary>
    public DateTime Fecha { get; set; }

    /// <summary>ID del cliente en el sistema origen. Null = cliente anónimo.</summary>
    public string? IdClienteOriginal { get; set; }

    /// <summary>ID del producto en el sistema origen.</summary>
    public string IdProductoOriginal { get; set; } = string.Empty;

    /// <summary>Plataforma que resuelve FuenteKey (ej: "Encuesta interna", "Sitio web", "Instagram").</summary>
    public string Plataforma { get; set; } = string.Empty;

    /// <summary>ClasificacionKey ya resuelto: 1=Negativa, 2=Neutra, 3=Positiva, 4=Sin clasificar.</summary>
    public int ClasificacionKey { get; set; } = 4;

    /// <summary>Texto completo del comentario.</summary>
    public string Comentario { get; set; } = string.Empty;

    /// <summary>Puntaje de satisfacción (1–5). Null si no aplica.</summary>
    public int? PuntajeSatisfaccion { get; set; }
}
