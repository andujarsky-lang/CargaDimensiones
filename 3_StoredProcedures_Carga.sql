-- ==============================================================================================
-- ACTIVIDAD 2: STORED PROCEDURES PARA CARGA ETL (FASE T+L)
-- Sistema de Análisis de Opiniones de Clientes
-- ==============================================================================================
-- Estudiante: Sky Luisahanie Andujar Victorino
-- Matrícula:  2025-1063
-- Asignatura: Electiva 1 (Big Data)
-- Profesor:   Francis Ramírez
-- ==============================================================================================
-- Descripción: Procedimientos almacenados que implementan la fase de Transformación y Carga
-- (T+L) del proceso ETL. Son consumidos desde C# vía ADO.NET.
-- ==============================================================================================

USE AnalisisOpinionesdb;
GO

-- ============================================================
-- SP 1: dim.usp_UpsertCliente
-- Inserta un cliente si no existe; devuelve su ClienteKey.
-- Si no viene IdClienteOriginal devuelve -1 (anónimo).
-- ============================================================
CREATE OR ALTER PROCEDURE dim.usp_UpsertCliente
    @SistemaOrigen      VARCHAR(50),
    @IdClienteOriginal  VARCHAR(50)     = NULL,
    @Nombre             NVARCHAR(150)   = NULL,
    @Email              NVARCHAR(150)   = NULL,
    @Pais               NVARCHAR(80)    = NULL,
    @RangoEdad          NVARCHAR(20)    = NULL,
    @TipoCliente        NVARCHAR(50)    = NULL,
    @ClienteKey         INT             OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Cliente anónimo: devolver -1 directamente
    IF @IdClienteOriginal IS NULL OR LTRIM(RTRIM(@IdClienteOriginal)) = ''
    BEGIN
        SET @ClienteKey = -1;
        RETURN;
    END

    -- Buscar si ya existe
    SELECT @ClienteKey = ClienteKey
    FROM   dim.Cliente
    WHERE  SistemaOrigen     = @SistemaOrigen
      AND  IdClienteOriginal = @IdClienteOriginal;

    -- Si no existe, insertar
    IF @ClienteKey IS NULL
    BEGIN
        INSERT INTO dim.Cliente
            (SistemaOrigen, IdClienteOriginal, Nombre, Email, Pais, RangoEdad, TipoCliente)
        VALUES
            (@SistemaOrigen, @IdClienteOriginal, @Nombre, @Email, @Pais, @RangoEdad, @TipoCliente);

        SET @ClienteKey = SCOPE_IDENTITY();
    END
END;
GO

-- ============================================================
-- SP 2: dim.usp_UpsertProducto
-- Inserta un producto si no existe; devuelve su ProductoKey.
-- ============================================================
CREATE OR ALTER PROCEDURE dim.usp_UpsertProducto
    @SistemaOrigen       VARCHAR(50),
    @IdProductoOriginal  VARCHAR(50),
    @NombreProducto      NVARCHAR(150)  = NULL,
    @Categoria           NVARCHAR(100)  = NULL,
    @ProductoKey         INT            OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Buscar si ya existe
    SELECT @ProductoKey = ProductoKey
    FROM   dim.Producto
    WHERE  SistemaOrigen       = @SistemaOrigen
      AND  IdProductoOriginal  = @IdProductoOriginal;

    -- Si no existe, insertar
    IF @ProductoKey IS NULL
    BEGIN
        INSERT INTO dim.Producto
            (SistemaOrigen, IdProductoOriginal, NombreProducto, Categoria)
        VALUES
            (@SistemaOrigen, @IdProductoOriginal, @NombreProducto, @Categoria);

        SET @ProductoKey = SCOPE_IDENTITY();
    END
END;
GO

-- ============================================================
-- SP 3: dim.usp_GetFuenteKey
-- Devuelve FuenteKey para una plataforma y sistema de origen.
-- (dim.Fuente está pre-poblada desde el script de modelado)
-- ============================================================
CREATE OR ALTER PROCEDURE dim.usp_GetFuenteKey
    @SistemaOrigen  VARCHAR(50),
    @Plataforma     NVARCHAR(50),
    @FuenteKey      INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT @FuenteKey = FuenteKey
    FROM   dim.Fuente
    WHERE  SistemaOrigen = @SistemaOrigen
      AND  Plataforma    = @Plataforma;
END;
GO

-- ============================================================
-- SP 4: fact.usp_InsertOpinion
-- Inserta una opinión en la tabla de hechos.
-- @Resultado: 1 = insertado, 0 = duplicado, -1 = error/rechazado
-- ============================================================
CREATE OR ALTER PROCEDURE fact.usp_InsertOpinion
    @SistemaOrigen        VARCHAR(50),
    @IdOpinionOriginal    VARCHAR(50),
    @FechaKey             INT,
    @ClienteKey           INT,
    @ProductoKey          INT,
    @FuenteKey            INT,
    @ClasificacionKey     INT,
    @Comentario           NVARCHAR(MAX),
    @PuntajeSatisfaccion  TINYINT = NULL,
    @Resultado            INT     OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;

    BEGIN TRY
        -- Validar comentario no vacío
        IF @Comentario IS NULL OR LEN(LTRIM(RTRIM(@Comentario))) = 0
        BEGIN
            SET @Resultado = -1;
            RETURN;
        END

        -- Validar que FechaKey exista en dim.Fecha
        IF NOT EXISTS (SELECT 1 FROM dim.Fecha WHERE FechaKey = @FechaKey)
        BEGIN
            SET @Resultado = -1;
            RETURN;
        END

        -- Validar que FuenteKey exista
        IF NOT EXISTS (SELECT 1 FROM dim.Fuente WHERE FuenteKey = @FuenteKey)
        BEGIN
            SET @Resultado = -1;
            RETURN;
        END

        -- Verificar duplicado por SistemaOrigen + IdOpinionOriginal
        IF EXISTS (
            SELECT 1 FROM fact.Opinion
            WHERE SistemaOrigen     = @SistemaOrigen
              AND IdOpinionOriginal = @IdOpinionOriginal
        )
        BEGIN
            SET @Resultado = 0;  -- duplicado, no se inserta
            RETURN;
        END

        -- Insertar la opinión en la tabla de hechos
        INSERT INTO fact.Opinion
            (SistemaOrigen, IdOpinionOriginal, FechaKey, ClienteKey, ProductoKey,
             FuenteKey, ClasificacionKey, Comentario, PuntajeSatisfaccion)
        VALUES
            (@SistemaOrigen, @IdOpinionOriginal, @FechaKey, @ClienteKey, @ProductoKey,
             @FuenteKey, @ClasificacionKey, @Comentario, @PuntajeSatisfaccion);

        SET @Resultado = 1;  -- insertado exitosamente
    END TRY
    BEGIN CATCH
        SET @Resultado = -1;  -- error inesperado
    END CATCH
END;
GO

-- ============================================================
-- Verificación: listar los stored procedures creados
-- ============================================================
SELECT
    SCHEMA_NAME(schema_id) AS Esquema,
    name                   AS Procedimiento,
    create_date,
    modify_date
FROM sys.procedures
WHERE SCHEMA_NAME(schema_id) IN ('dim', 'fact')
ORDER BY Esquema, Procedimiento;
GO
