# Tarea: Generar JSON Schema de validación para los CSVs del importador Abuvi.Setup

## Contexto

Este proyecto ./src/Abuvi.Setup es un importador .NET que lee archivos CSV y crea entidades en una base de datos PostgreSQL. Necesito extraer un "contrato" de validación para cada CSV que el importador consume, de forma que otro proyecto (Python) pueda validar los CSVs ANTES de enviarlos al importador.

## Lo que necesito

1. **Analiza el código del importador** — Revisa los modelos de entidad, los mapeos CSV-to-entity, y cualquier validación existente (DataAnnotations, FluentValidation, checks manuales, restricciones de DB).

2. **Genera un fichero JSON schema por cada tipo de CSV** que el importador procesa. Cada schema debe incluir:
   - `fileName`: nombre esperado del CSV
   - `separator`: delimitador usado (`;`, `,`, `\t`)
   - `encoding`: encoding esperado (UTF-8, etc.)
   - `columns`: array con cada columna definiendo:
     - `name`: nombre exacto del header
     - `type`: string | integer | decimal | date | boolean
     - `required`: true/false
     - `maxLength`: si aplica
     - `format`: regex de validación si hay patrón específico (emails, teléfonos, códigos postales, etc.)
     - `allowedValues`: array de valores permitidos si es un enum
     - `references`: si esta columna es FK a otra entidad/CSV, indicar cuál
   - `constraints`: validaciones a nivel de fichero:
     - `uniqueColumns`: columnas o combinaciones que deben ser únicas
     - `requiredRowCount`: mínimo de filas esperado si aplica
     - `dependsOn`: otros CSVs que deben importarse antes (orden de dependencia)

3. **Genera un fichero `import-order.json`** que defina el orden correcto de importación de los CSVs, respetando las dependencias entre entidades.

4. **Documenta las validaciones implícitas** — Si hay validaciones que solo existen en lógica de negocio (no en annotations ni en DB constraints), documéntalas como comentarios en el schema.

## Formato de salida

Genera los schemas en una carpeta `schemas/` con un fichero por entidad:

```
schemas/
├── familias.schema.json
├── miembros.schema.json
├── inscripciones.schema.json
├── ... (uno por cada CSV)
└── import-order.json
```

## Ejemplo de formato esperado

```json
{
  "fileName": "familias.csv",
  "separator": ";",
  "encoding": "UTF-8",
  "columns": [
    {
      "name": "nombre",
      "type": "string",
      "required": true,
      "maxLength": 200
    },
    {
      "name": "email",
      "type": "string",
      "required": true,
      "format": "^[\\w.-]+@[\\w.-]+\\.[a-zA-Z]{2,}$"
    }
  ],
  "constraints": {
    "uniqueColumns": ["email"],
    "dependsOn": []
  }
}
```

## Importante

- Extrae las reglas del CÓDIGO REAL, no inventes validaciones.
- Si una validación es ambigua o no está clara, márcala con `"confidence": "low"` y un comentario explicando la duda.
- Incluye las restricciones de la DB (NOT NULL, UNIQUE, FK) además de las del código.
