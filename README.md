# Sistema de Análisis de Opiniones de Clientes — Proceso ETL en .NET

**Estudiante:** Sky Luisahanie Andujar Victorino  
**Matrícula:** 2025-1063  
**Asignatura:** Electiva 1 (Big Data)  
**Profesor:** Francis Ramírez  
**Sección:** #5  

---

## 📌 Descripción del Proyecto

Este repositorio contiene la solución integral del proceso **ETL (Extracción, Transformación y Carga)** para el **Sistema de Análisis de Opiniones de Clientes**, diseñada para alimentar el Data Warehouse relacional `AnalisisOpinionesdb` en SQL Server.

El sistema consolida opiniones provenientes de tres fuentes heterogéneas:
1. **Encuestas internas:** Archivos planos en formato CSV (`surveys_part1.csv`).
2. **Reseñas web:** Base de datos relacional transaccional (tabla `WebReviews`).
3. **Redes sociales:** Comentarios consumidos a través de una API REST propia desarrollada en ASP.NET Core (`GET /api/socialcomments`).

La solución realiza la extracción paralela de los datos, aplica reglas de negocio para estandarización y derivación de sentimientos, y ejecuta la carga hacia el modelo dimensional utilizando **ADO.NET (`Microsoft.Data.SqlClient`)** mediante el consumo de **Procedimientos Almacenados (Stored Procedures)** en SQL Server.

---

## 🏗️ Arquitectura de la Solución

La solución sigue una arquitectura en capas modular y limpia (Clean Architecture simplificada):

```text
OpinionesExtraccion/
├── OpinionesExtraccion.Api/        # Microservicio Web API en ASP.NET Core
│   └── Controllers/
│       └── SocialCommentsController.cs   # Expone comentarios de redes sociales
├── OpinionesExtraccion.Data/       # Capa de Acceso a Datos, Modelos y Negocio
│   ├── Interfaces/                 # Contratos de repositorios (DIP)
│   ├── Models/
│   │   ├── Api/                    # Modelos de entrada API
│   │   ├── Csv/                    # Modelos de entrada CSV
│   │   ├── Db/                     # Modelos de entrada Base de Datos
│   │   └── Dw/                     # Modelos dimensionales (OpinionDwRecord, EtlLoadResult)
│   ├── Repositories/
│   │   ├── Api/                    # Consumo HTTP asíncrono
│   │   ├── Csv/                    # Lectura con CsvHelper
│   │   ├── Db/                     # Extracción con SqlDataReader
│   │   ├── Dw/                     # Carga al DW con ADO.NET y Stored Procedures
│   │   └── Files/                  # Almacenamiento temporal en JSON
│   └── Transformers/               # Reglas de transformación y mapeo dimensional
├── OpinionesExtraccion.Worker/     # Orquestador del proceso ETL (Worker Service)
│   ├── Program.cs                  # Configuración de Inyección de Dependencias
│   └── Worker.cs                   # Ejecución paralela E-T-L y métricas
├── 3_StoredProcedures_Carga.sql    # Procedimientos almacenados para dim y fact
└── OpinionesExtraccion.slnx        # Archivo de solución
```

---

## ⚙️ Procedimientos Almacenados (Stored Procedures)

Para garantizar atomicidad, integridad referencial y alto rendimiento, la fase de carga (L) delega la persistencia al motor de base de datos a través de 4 procedimientos almacenados:

| Stored Procedure | Esquema | Propósito |
|---|---|---|
| `dim.usp_UpsertCliente` | `dim` | Inserta o recupera el `ClienteKey`. Asigna `-1` para clientes anónimos. |
| `dim.usp_UpsertProducto` | `dim` | Inserta o recupera el `ProductoKey` garantizando unicidad por origen. |
| `dim.usp_GetFuenteKey` | `dim` | Resuelve el `FuenteKey` a partir del sistema de origen y la plataforma. |
| `fact.usp_InsertOpinion` | `fact` | Inserta el hecho con validación previa de FKs y control estricto de duplicados. |

---

## 🚀 Instrucciones de Ejecución

### Prerrequisitos
* **.NET SDK 9.0** o superior.
* **SQL Server** (LocalDB o SQLEXPRESS) con la base de datos `AnalisisOpinionesdb` creada.
* Ejecutar en SQL Server el script `3_StoredProcedures_Carga.sql`.

### 1. Iniciar el servicio Web API
En una terminal:
```bash
cd OpinionesExtraccion.Api
dotnet run
```
La API quedará escuchando en `http://localhost:5000` con documentación Swagger disponible en `/swagger`.

### 2. Ejecutar el Orquestador ETL (Worker)
En una segunda terminal:
```bash
cd OpinionesExtraccion.Worker
dotnet run
```
El Worker extraerá las tres fuentes en paralelo, aplicará las transformaciones, cargará los datos en SQL Server y presentará en consola la tabla de resumen con los registros procesados, insertados y duplicados.

---

## 📊 Atributos de Calidad Implementados

* **Rendimiento:** Extracción y carga asíncrona no bloqueante utilizando `Task.WhenAll`, `SqlDataReader` y llamadas a Stored Procedures optimizados.
* **Integridad Referencial:** Comprobación estricta de llaves foráneas (`dim.Fecha`, `dim.Fuente`, `dim.Cliente`, `dim.Producto`).
* **Idempotencia y Control de Duplicados:** Validación mediante claves naturales compuestas `(SistemaOrigen, IdOpinionOriginal)` para evitar redundancia en múltiples ejecuciones.
## 🔗 Repositorio

https://github.com/andujarsky-lang/CargaDimensiones.git
