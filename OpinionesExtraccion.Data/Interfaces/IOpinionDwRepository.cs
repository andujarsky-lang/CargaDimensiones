using OpinionesExtraccion.Data.Models.Dw;

namespace OpinionesExtraccion.Data.Interfaces;

/// <summary>
/// Contrato para el repositorio de carga al Data Warehouse.
/// Llama a los stored procedures de SQL Server mediante ADO.NET:
///   dim.usp_UpsertCliente, dim.usp_UpsertProducto,
///   dim.usp_GetFuenteKey, fact.usp_InsertOpinion.
/// </summary>
public interface IOpinionDwRepository
{
    /// <summary>
    /// Carga un registro de opinión transformado en el DW.
    /// </summary>
    /// <returns>1 = insertado, 0 = duplicado, -1 = rechazado.</returns>
    Task<int> LoadOpinionAsync(OpinionDwRecord record, CancellationToken cancellationToken = default);
}
