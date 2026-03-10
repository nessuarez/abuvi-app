# Ampliar categorías de relaciones familiares

## Descripción

Actualmente el enum `FamilyRelationship` solo contempla 5 valores: `Parent`, `Child`, `Sibling`, `Spouse` y `Other`. Se necesitan más categorías para reflejar correctamente relaciones familiares habituales como abuelos/as, nietos/as, tíos/as, etc.

## Valores actuales

| Enum | Etiqueta ES |
|------|-------------|
| Parent | Padre/Madre |
| Child | Hijo/Hija |
| Sibling | Hermano/Hermana |
| Spouse | Cónyuge |
| Other | Otro |

## Nuevos valores a añadir

| Enum | Etiqueta ES | Justificación |
|------|-------------|---------------|
| Grandparent | Abuelo/Abuela | Relación directa muy común, relevante para autorizaciones |
| Grandchild | Nieto/Nieta | Inversa de Grandparent |
| UncleAunt | Tío/Tía | Frecuente como contacto de emergencia o autorizado para recoger |
| NephewNiece | Sobrino/Sobrina | Inversa de UncleAunt |
| Cousin | Primo/Prima | Relación habitual en unidades familiares extensas |
| InLaw | Familia política | Suegro/a, cuñado/a u otra relación política |

> **Nota:** La columna `relationship` en BD tiene `maxLength: 20`. Todos los valores caben holgadamente (máximo 11 chars: "NephewNiece").

## Archivos a modificar

### Backend

1. **`src/Abuvi.API/Features/FamilyUnits/FamilyUnitsModels.cs`** (~línea 48)
   - Añadir los nuevos valores al enum `FamilyRelationship`

2. **`src/Abuvi.Setup/Importers/FamilyMemberImporter.cs`**
   - Verificar que el parsing `Enum.Parse<FamilyRelationship>` sigue funcionando (debería, ya que usa `ignoreCase: true`)

### Frontend

3. **`frontend/src/types/family-unit.ts`**
   - Añadir los nuevos valores al enum `FamilyRelationship`
   - Añadir las etiquetas en español a `FamilyRelationshipLabels`

### Documentación

4. **`ai-specs/specs/data-model.md`** (~línea 93)
   - Actualizar la lista de valores del enum en la documentación del modelo

### Migraciones

5. **No se requiere migración de base de datos**: el campo `relationship` se almacena como `string` (no como int), por lo que nuevos valores del enum se almacenan directamente sin cambios de esquema.

## Criterios de aceptación

- [ ] El enum backend `FamilyRelationship` incluye los 6 nuevos valores
- [ ] El enum frontend `FamilyRelationship` incluye los 6 nuevos valores con sus etiquetas en español
- [ ] El dropdown de selección de relación en `FamilyMemberForm.vue` muestra todas las opciones
- [ ] Los miembros existentes con relaciones antiguas siguen funcionando correctamente
- [ ] El importador CSV acepta los nuevos valores
- [ ] La documentación del modelo de datos está actualizada

## Requisitos no funcionales

- **Retrocompatibilidad:** Los datos existentes no se ven afectados. No hay migración de datos.
- **Ordenación:** Considerar ordenar las opciones en el dropdown de forma lógica (no alfabética): padres/madres primero, luego hijos, abuelos, etc.

## Estimación de impacto

Cambio de bajo riesgo. No requiere migración de BD ni cambios de API. Solo se amplía un enum y sus etiquetas.
