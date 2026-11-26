using System.Data;
using Microsoft.Data.SqlClient;
using OpinionesExtraccion.Data.Interfaces;
using OpinionesExtraccion.Data.Models.Dw;

namespace OpinionesExtraccion.Data.Repositories.Dw;

/// <summary>
/// Repositorio de carga al Data Warehouse.
/// Implementa la fase T+L del ETL consumiendo stored procedures
/// de SQL Server mediante ADO.NET (Microsoft.Data.SqlClient).
///
/// Stored procedures consumidos:
///   · dim.usp_UpsertCliente    — inserta o recupera ClienteKey
///   · dim.usp_UpsertProducto   — inserta o recupera ProductoKey
///   · dim.usp_GetFuenteKey     — resuelve FuenteKey por plataforma
///   · fact.usp_InsertOpinion   — inserta en tabla de hechos con control de duplicados
/// </summary>
public class OpinionDwRepository : IOpinionDwRepository
{
    private readonly string _connectionString;

    public OpinionDwRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <inheritdoc/>
    public async Task<int> LoadOpinionAsync(OpinionDwRecord record, CancellationToken cancellationToken = default)
    {
        // --- Validaciones previas en C# antes de llamar al SP ---
        if (string.IsNullOrWhiteSpace(record.Comentario))        return -1;
        if (string.IsNullOrWhiteSpace(record.IdProductoOriginal)) return -1;
        if (string.IsNullOrWhiteSpace(record.SistemaOrigen))     return -1;
        if (record.Fecha == default)                              return -1;

        // FechaKey = YYYYMMDD como entero (ej: 20250315)
        int fechaKey = int.Parse(record.Fecha.ToString("yyyyMMdd"));

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // PASO 1: Upsert en dim.Cliente → obtener ClienteKey
        int clienteKey = await UpsertClienteAsync(connection, record, cancellationToken);

        // PASO 2: Upsert en dim.Producto → obtener ProductoKey
        int productoKey = await UpsertProductoAsync(connection, record, cancellationToken);
        if (productoKey <= 0) return -1;

        // PASO 3: Obtener FuenteKey desde dim.Fuente
        int fuenteKey = await GetFuenteKeyAsync(connection, record.SistemaOrigen, record.Plataforma, cancellationToken);
        if (fuenteKey <= 0) return -1;

        // PASO 4: Insertar en fact.Opinion → 1=ok, 0=duplicado, -1=error
        return await InsertOpinionAsync(connection, record, fechaKey, clienteKey, productoKey, fuenteKey, cancellationToken);
    }

    // ================================================================
    // PASO 1 — dim.usp_UpsertCliente
    // ================================================================
    private static async Task<int> UpsertClienteAsync(
        SqlConnection connection,
        OpinionDwRecord record,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand("dim.usp_UpsertCliente", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.AddWithValue("@SistemaOrigen",     record.SistemaOrigen);
        cmd.Parameters.AddWithValue("@IdClienteOriginal", (object?)record.IdClienteOriginal ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Nombre",            DBNull.Value);
        cmd.Parameters.AddWithValue("@Email",             DBNull.Value);
        cmd.Parameters.AddWithValue("@Pais",              DBNull.Value);
        cmd.Parameters.AddWithValue("@RangoEdad",         DBNull.Value);
        cmd.Parameters.AddWithValue("@TipoCliente",       DBNull.Value);

        var outputParam = new SqlParameter("@ClienteKey", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(outputParam);

        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return outputParam.Value is int key ? key : -1;
    }

    // ================================================================
    // PASO 2 — dim.usp_UpsertProducto
    // ================================================================
    private static async Task<int> UpsertProductoAsync(
        SqlConnection connection,
        OpinionDwRecord record,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand("dim.usp_UpsertProducto", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.AddWithValue("@SistemaOrigen",      record.SistemaOrigen);
        cmd.Parameters.AddWithValue("@IdProductoOriginal", record.IdProductoOriginal);
        cmd.Parameters.AddWithValue("@NombreProducto",     DBNull.Value);
        cmd.Parameters.AddWithValue("@Categoria",          DBNull.Value);

        var outputParam = new SqlParameter("@ProductoKey", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(outputParam);

        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return outputParam.Value is int key ? key : 0;
    }

    // ================================================================
    // PASO 3 — dim.usp_GetFuenteKey
    // ================================================================
    private static async Task<int> GetFuenteKeyAsync(
        SqlConnection connection,
        string sistemaOrigen,
        string plataforma,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand("dim.usp_GetFuenteKey", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.AddWithValue("@SistemaOrigen", sistemaOrigen);
        cmd.Parameters.AddWithValue("@Plataforma",    plataforma);

        var outputParam = new SqlParameter("@FuenteKey", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(outputParam);

        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return outputParam.Value is int key ? key : 0;
    }

    // ================================================================
    // PASO 4 — fact.usp_InsertOpinion
    // ================================================================
    private static async Task<int> InsertOpinionAsync(
        SqlConnection connection,
        OpinionDwRecord record,
        int fechaKey,
        int clienteKey,
        int productoKey,
        int fuenteKey,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand("fact.usp_InsertOpinion", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        cmd.Parameters.AddWithValue("@SistemaOrigen",       record.SistemaOrigen);
        cmd.Parameters.AddWithValue("@IdOpinionOriginal",   record.IdOpinionOriginal);
        cmd.Parameters.AddWithValue("@FechaKey",            fechaKey);
        cmd.Parameters.AddWithValue("@ClienteKey",          clienteKey);
        cmd.Parameters.AddWithValue("@ProductoKey",         productoKey);
        cmd.Parameters.AddWithValue("@FuenteKey",           fuenteKey);
        cmd.Parameters.AddWithValue("@ClasificacionKey",    record.ClasificacionKey);
        cmd.Parameters.AddWithValue("@Comentario",          record.Comentario);
        cmd.Parameters.AddWithValue("@PuntajeSatisfaccion",
            record.PuntajeSatisfaccion.HasValue
                ? (object)record.PuntajeSatisfaccion.Value
                : DBNull.Value);

        var outputParam = new SqlParameter("@Resultado", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(outputParam);

        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return outputParam.Value is int result ? result : -1;
    }
}
