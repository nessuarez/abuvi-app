# Pre-rellenar datos del tutor legal con el primer adulto seleccionado

## Descripción

Actualmente, cuando un usuario inscribe a miembros de su unidad familiar en un campamento y selecciona menores de edad, aparecen los campos de **tutor/a legal** (nombre y documento) vacíos para cada menor. El usuario debe rellenarlos manualmente, lo cual es tedioso porque en la gran mayoría de los casos el tutor legal es uno de los adultos que ya está inscrito en la misma inscripción.

**Mejora solicitada:** Al seleccionar un menor, los campos del tutor legal deben pre-rellenarse automáticamente con los datos del **primer adulto seleccionado** en la inscripción (nombre completo y número de documento). El usuario podrá modificar estos valores si el tutor es otra persona.

## Criterios de aceptación

1. **AC-1: Pre-relleno automático al seleccionar un menor**
   - DADO que hay al menos un adulto (≥18 años) ya seleccionado en la inscripción
   - CUANDO el usuario selecciona (marca el checkbox de) un menor (<18 años)
   - ENTONCES los campos `guardianName` y `guardianDocumentNumber` del menor se pre-rellenan con `firstName + ' ' + lastName` y `documentNumber` del primer adulto seleccionado (por orden de aparición en la lista de miembros)

2. **AC-2: Sin pre-relleno si no hay adultos seleccionados**
   - DADO que no hay ningún adulto seleccionado
   - CUANDO el usuario selecciona un menor
   - ENTONCES los campos del tutor quedan vacíos (comportamiento actual)

3. **AC-3: Pre-relleno retroactivo al seleccionar el primer adulto**
   - DADO que ya hay menores seleccionados con campos de tutor vacíos (porque se seleccionaron antes que cualquier adulto)
   - CUANDO el usuario selecciona el primer adulto
   - ENTONCES los campos de tutor de todos los menores seleccionados que **aún estén vacíos** se pre-rellenan con los datos de ese adulto

4. **AC-4: No sobrescribir datos editados manualmente**
   - DADO que el usuario ha modificado manualmente los campos de tutor de un menor
   - CUANDO se selecciona o deselecciona un adulto
   - ENTONCES los campos de tutor modificados manualmente NO se sobrescriben

5. **AC-5: Campos editables**
   - Los campos pre-rellenados son completamente editables
   - El usuario puede cambiar el nombre y documento del tutor a cualquier valor

6. **AC-6: Sin cambios en backend**
   - Esta funcionalidad es puramente frontend (UX)
   - No se requieren cambios en la API, validaciones ni modelo de datos del backend

## Análisis técnico

### Datos disponibles

Del `FamilyMemberResponse` de cada adulto disponemos de:

- `firstName` + `lastName` → mapeado a `guardianName`
- `documentNumber` → mapeado a `guardianDocumentNumber`

### Componente afectado

**Único archivo a modificar:** `frontend/src/components/registrations/RegistrationMemberSelector.vue`

### Cambios necesarios

#### 1. Computed: primer adulto seleccionado

Añadir un `computed` que identifique al primer adulto seleccionado:

```typescript
const firstSelectedAdult = computed(() => {
  for (const member of props.members) {
    if (isSelected(member.id) && !isMinor(member)) {
      return member
    }
  }
  return null
})
```

#### 2. Modificar `toggleMember` para pre-rellenar al seleccionar un menor

En la función `toggleMember`, al añadir un nuevo miembro que sea menor, pre-rellenar los campos de tutor con los datos del primer adulto seleccionado (si existe):

```typescript
const toggleMember = (memberId: string) => {
  if (isSelected(memberId)) {
    emit(
      'update:modelValue',
      props.modelValue.filter((s) => s.memberId !== memberId)
    )
  } else {
    const member = props.members.find((m) => m.id === memberId)
    const adult = firstSelectedAdult.value

    // Pre-fill guardian data for minors from first selected adult
    const guardianName = member && isMinor(member) && adult
      ? `${adult.firstName} ${adult.lastName}`
      : null
    const guardianDocumentNumber = member && isMinor(member) && adult
      ? adult.documentNumber
      : null

    emit('update:modelValue', [
      ...props.modelValue,
      {
        memberId,
        attendancePeriod: 'Complete',
        visitStartDate: null,
        visitEndDate: null,
        guardianName,
        guardianDocumentNumber
      }
    ])
  }
}
```

#### 3. Pre-relleno retroactivo al seleccionar un adulto (AC-3)

Cuando se selecciona un miembro que es adulto y es el **primer adulto** en la selección, iterar los menores ya seleccionados y rellenar los campos vacíos:

```typescript
const toggleMember = (memberId: string) => {
  if (isSelected(memberId)) {
    // ... deselección sin cambios
  } else {
    const member = props.members.find((m) => m.id === memberId)
    const adult = firstSelectedAdult.value
    const isNewMemberAdult = member && !isMinor(member)

    // Pre-fill guardian for minor being added
    const guardianName = member && isMinor(member) && adult
      ? `${adult.firstName} ${adult.lastName}`
      : null
    const guardianDocumentNumber = member && isMinor(member) && adult
      ? adult.documentNumber
      : null

    let updatedSelections = [
      ...props.modelValue,
      {
        memberId,
        attendancePeriod: 'Complete' as AttendancePeriod,
        visitStartDate: null,
        visitEndDate: null,
        guardianName,
        guardianDocumentNumber
      }
    ]

    // If this is the first adult being selected, backfill empty guardian fields on existing minors
    if (isNewMemberAdult && !adult) {
      const newAdultName = `${member!.firstName} ${member!.lastName}`
      const newAdultDoc = member!.documentNumber
      updatedSelections = updatedSelections.map((s) => {
        if (s.memberId === memberId) return s
        const m = props.members.find((fm) => fm.id === s.memberId)
        if (m && isMinor(m) && !s.guardianName && !s.guardianDocumentNumber) {
          return { ...s, guardianName: newAdultName, guardianDocumentNumber: newAdultDoc }
        }
        return s
      })
    }

    emit('update:modelValue', updatedSelections)
  }
}
```

### Archivos a modificar

| Archivo | Cambio |
|---------|--------|
| `frontend/src/components/registrations/RegistrationMemberSelector.vue` | Añadir `firstSelectedAdult` computed, modificar `toggleMember` para pre-relleno y backfill |

### Archivos de test

| Archivo | Cambio |
|---------|--------|
| `frontend/src/components/registrations/__tests__/RegistrationMemberSelector.spec.ts` | Añadir tests para pre-relleno del tutor (si existe el archivo, o crear uno nuevo) |

## Casos de prueba

1. **Seleccionar adulto + luego menor** → campos de tutor del menor pre-rellenados con datos del adulto
2. **Seleccionar menor sin adultos** → campos de tutor vacíos
3. **Seleccionar menor sin adultos + luego seleccionar adulto** → campos de tutor del menor se rellenan retroactivamente
4. **Editar manualmente campo de tutor + luego seleccionar otro adulto** → campo editado no se sobrescribe
5. **Seleccionar 2 adultos + 1 menor** → se usan datos del primer adulto (por orden de aparición en la lista `members`)
6. **Deseleccionar el primer adulto** → campos de tutor de menores ya rellenados no se modifican (el usuario puede editarlos manualmente si quiere)
7. **Seleccionar 2 menores con 1 adulto** → ambos menores reciben pre-relleno independiente

## Requisitos no funcionales

- **Rendimiento:** Sin impacto perceptible, son computed/reactivos locales
- **Accesibilidad:** Sin cambios, los campos mantienen sus placeholders y estructura existente
- **Backend:** Sin cambios necesarios. Los campos `guardianName` y `guardianDocumentNumber` ya se envían y almacenan como parte del `CreateRegistrationRequest`

## Fuera de alcance

- No se cambia la obligatoriedad de los campos de tutor (siguen siendo opcionales)
- No se añade selector de "¿quién es el tutor?" entre los adultos seleccionados
- No se modifican los campos del modelo ni la API
