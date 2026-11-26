namespace OpinionesExtraccion.Data.Models.Dw;

/// <summary>
/// Resultado del proceso de carga ETL para una fuente de datos.
/// Se usa para mostrar el resumen al finalizar la ejecución.
/// </summary>
public class EtlLoadResult
{
    /// <summary>Nombre descriptivo de la fuente (CSV, BD, API).</summary>
    public string Fuente { get; set; } = string.Empty;

    /// <summary>Total de registros leídos de la fuente.</summary>
    public int Procesados { get; set; }

    /// <summary>Registros insertados correctamente en el Data Warehouse.</summary>
    public int Insertados { get; set; }

    /// <summary>Registros ignorados por ser duplicados.</summary>
    public int Duplicados { get; set; }

    /// <summary>Registros rechazados por errores de validación o integridad.</summary>
    public int Rechazados { get; set; }

    /// <summary>Tiempo de ejecución en milisegundos.</summary>
    public long ElapsedMs { get; set; }
}
