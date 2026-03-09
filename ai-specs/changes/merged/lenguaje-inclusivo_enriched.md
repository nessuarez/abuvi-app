# Lenguaje Inclusivo - Revisión de textos estáticos del frontend

## Descripción

Revisar todos los textos estáticos del frontend que usan forma masculina en español y actualizarlos a lenguaje inclusivo (usando formas neutras o la fórmula "o/a" donde sea necesario).

---

## Cambios propuestos (claros)

### 1. Saludos y bienvenidas

| Archivo | Texto actual | Texto propuesto |
|---------|-------------|-----------------|
| `components/auth/AuthContainer.vue:27` | `Bienvenidos a ABUVI` | `Te damos la bienvenida a ABUVI` |
| `onboarding/tours/welcome.tour.ts:12` | `Bienvenido a ABUVI` | `Te damos la bienvenida a ABUVI` |

### 2. "Nosotros" → alternativa neutra

| Archivo | Texto actual | Texto propuesto |
|---------|-------------|-----------------|
| `components/anniversary/AnniversaryContactForm.vue:50` | `Colabora con nosotros` | `Colabora con la asociación` |
| `components/home/HomeHeroCarousel.vue:26` | `Celebra con nosotros medio siglo...` | `Únete a celebrar medio siglo...` |

### 3. "Niño" → "Infantil" (en contexto de precios y edades)

| Archivo | Texto actual | Texto propuesto |
|---------|-------------|-----------------|
| `views/camps/CampEditionDetailPage.vue:100` | `Precio niño:` | `Precio infantil:` |
| `views/camps/CampLocationDetailPage.vue:310` | `Precio niño:` | `Precio infantil:` |
| `components/camps/CampEditionProposeDialog.vue:267` | `Precio niño *` | `Precio infantil *` |
| `components/camps/CampEditionProposeDialog.vue:312` | `Precio niño/sem` | `Precio infantil/sem` |
| `components/camps/CampEditionProposeDialog.vue:373` | `Precio niño/fds` | `Precio infantil/fds` |
| `components/camps/CampEditionProposeDialog.vue:430` | `Edad mín. niño` | `Edad mín. infantil` |
| `components/camps/CampEditionProposeDialog.vue:440` | `Edad máx. niño` | `Edad máx. infantil` |
| `components/camps/CampEditionUpdateDialog.vue:288` | `Precio niño` | `Precio infantil` |
| `components/camps/CampEditionUpdateDialog.vue:359` | `Precio niño/sem` | `Precio infantil/sem` |
| `components/camps/CampEditionUpdateDialog.vue:435` | `Precio niño/fds` | `Precio infantil/fds` |
| `components/camps/CampEditionUpdateDialog.vue:538` | `Edad mín. niño` | `Edad mín. infantil` |
| `components/camps/CampEditionUpdateDialog.vue:549` | `Edad máx. niño` | `Edad máx. infantil` |
| `components/camps/CampLocationForm.vue:509` | `Precio niño (€) *` | `Precio infantil (€) *` |
| `components/admin/AssociationSettingsPanel.vue:97` | `Edad mín. niño` | `Edad mín. infantil` |
| `components/admin/AssociationSettingsPanel.vue:108` | `Edad máx. niño` | `Edad máx. infantil` |

Mensajes de validación con "niño":

| Archivo | Texto actual | Texto propuesto |
|---------|-------------|-----------------|
| `components/admin/AssociationSettingsPanel.vue:37` | `La edad máxima de bebé debe ser menor a la edad mínima de niño` | `La edad máxima de bebé debe ser menor a la edad mínima infantil` |
| `components/admin/AssociationSettingsPanel.vue:40` | `La edad máxima de niño debe ser menor a la edad mínima de adulto` | `La edad máxima infantil debe ser menor a la edad mínima de adulto` |
| `components/camps/CampEditionProposeDialog.vue:137` | `El precio por niño/semana es obligatorio` | `El precio infantil/semana es obligatorio` |
| `components/camps/CampEditionProposeDialog.vue:145` | `La edad mínima de niño es obligatoria` | `La edad mínima infantil es obligatoria` |
| `components/camps/CampEditionProposeDialog.vue:147` | `La edad máxima de niño es obligatoria` | `La edad máxima infantil es obligatoria` |
| `components/camps/CampEditionProposeDialog.vue:152` | `La edad máxima de bebé debe ser menor a la edad mínima de niño` | `La edad máxima de bebé debe ser menor a la edad mínima infantil` |
| `components/camps/CampEditionProposeDialog.vue:156` | `La edad máxima de niño debe ser menor a la edad mínima de adulto` | `La edad máxima infantil debe ser menor a la edad mínima de adulto` |
| `components/camps/CampEditionProposeDialog.vue:170` | `El precio por niño/fds es obligatorio` | `El precio infantil/fds es obligatorio` |
| `components/camps/CampEditionUpdateDialog.vue:140` | `El precio por niño debe ser mayor o igual a 0` | `El precio infantil debe ser mayor o igual a 0` |
| `components/camps/CampEditionUpdateDialog.vue:152` | `El precio por niño/semana es obligatorio` | `El precio infantil/semana es obligatorio` |
| `components/camps/CampEditionUpdateDialog.vue:166` | `El precio por niño/fds es obligatorio` | `El precio infantil/fds es obligatorio` |
| `components/camps/CampEditionUpdateDialog.vue:174` | `La edad mínima de niño es obligatoria` | `La edad mínima infantil es obligatoria` |
| `components/camps/CampEditionUpdateDialog.vue:175` | `La edad máxima de niño es obligatoria` | `La edad máxima infantil es obligatoria` |
| `components/camps/CampEditionUpdateDialog.vue:178` | `La edad máxima de bebé debe ser menor a la edad mínima de niño` | `La edad máxima de bebé debe ser menor a la edad mínima infantil` |
| `components/camps/CampEditionUpdateDialog.vue:181` | `La edad máxima de niño debe ser menor a la edad mínima de adulto` | `La edad máxima infantil debe ser menor a la edad mínima de adulto` |

### 4. "Invitado" → "Invitado/a"

| Archivo | Texto actual | Texto propuesto |
|---------|-------------|-----------------|
| `components/guests/GuestForm.vue:285` | `'Actualizar invitado' / 'Añadir invitado'` | `'Actualizar invitado/a' / 'Añadir invitado/a'` |
| `composables/useGuests.ts:25` | `'Error al obtener los invitados'` | `'Error al obtener la lista de invitados/as'` |
| `composables/useGuests.ts:49` | `'Error al crear el invitado'` | `'Error al crear el/la invitado/a'` |
| `composables/useGuests.ts:75` | `'Error al actualizar el invitado'` | `'Error al actualizar el/la invitado/a'` |
| `composables/useGuests.ts:93` | `'Error al eliminar el invitado'` | `'Error al eliminar el/la invitado/a'` |

### 5. "Usuario" → alternativa neutra donde sea posible

| Archivo | Texto actual | Texto propuesto |
|---------|-------------|-----------------|
| `components/users/UserForm.vue:226` | `'Crear usuario' / 'Actualizar usuario'` | `'Crear cuenta' / 'Actualizar cuenta'` |
| `pages/UsersPage.vue:100` | `Gestión de usuarios` | `Gestión de cuentas` |
| `pages/UsersPage.vue:101` | `Crear usuario` | `Crear cuenta` |
| `pages/UsersPage.vue:179` | `Crear nuevo usuario` | `Crear nueva cuenta` |
| `pages/UserDetailPage.vue:90` | `Detalle del usuario` | `Detalle de la cuenta` |
| `pages/UserDetailPage.vue:162` | `Editar usuario` | `Editar cuenta` |
| `components/admin/PaymentsReviewQueue.vue:227` | `El usuario podrá subir uno nuevo.` | `Se podrá subir uno nuevo.` |
| `components/camps/CampEditionExtrasFormDialog.vue:265` | `Texto que verá el usuario` | `Texto visible para quien se registre` |

### 6. "Todos" → alternativa neutra

| Archivo | Texto actual | Texto propuesto |
|---------|-------------|-----------------|
| `views/ResetPasswordPage.vue:72` | `La contraseña no cumple todos los requisitos` | `La contraseña no cumple con los requisitos` |
| `components/anniversary/AnniversaryUploadForm.vue:153` | `El 50 aniversario lo construimos entre todos` | `El 50 aniversario lo construimos entre todas las personas` |
| `components/family-units/FamilyUnitForm.vue:109` | `confirmo que tengo el consentimiento de todos los miembros` | `confirmo que tengo el consentimiento de cada miembro` |
| `components/registrations/RegistrationMemberSelector.vue:313` | `No todos los asistentes tienen la misma estancia` | `No todas las personas asistentes tienen la misma estancia` |
| `components/memberships/BulkMembershipDialog.vue:121` | `Se aplicará a todos los que no tengan membresía.` | `Se aplicará a quienes no tengan membresía.` |

### 7. "Registro completado" y otros participios en notificaciones

| Archivo | Texto actual | Texto propuesto |
|---------|-------------|-----------------|
| `components/auth/RegisterForm.vue:132` | `¡Registro completado!` | `¡Registro completo!` |
| `components/anniversary/AnniversaryGallery.vue:48` | `Aún no hay recuerdos aprobados.` | `Aún no hay recuerdos con aprobación.` |

### 8. "Amigos" → alternativa neutra

| Archivo | Texto actual | Texto propuesto |
|---------|-------------|-----------------|
| `views/legal/TransparencyPage.vue:76` | `Nació como un grupo de amigos` | `Nació como un grupo de amistades` |
| `views/legal/TransparencyPage.vue:78` | `que reúnen a familias y amigos` | `que reúnen a familias y amistades` |

---

## Pendientes de revisión (necesitan decisión del usuario)

### A. "Activo/Inactivo" en badges de estado

Aparece en 14 ubicaciones.

- Dejarlo como está (se refiere al estado, no a la persona: "Estado: Activo")

**Archivos afectados:**

- `pages/UsersPage.vue:151`
- `pages/UserDetailPage.vue:142`
- `components/users/UserCard.vue:66`
- `components/admin/UsersAdminPanel.vue:115`
- `components/camps/CampEditionExtrasList.vue:271`
- `components/camps/CampLocationCard.vue:95`
- `components/camps/CampEditionAccommodationsPanel.vue:124`
- `views/camps/CampLocationDetailPage.vue:192`

### B. "Socio activo" en membresías

Aparece en:

- `views/ProfilePage.vue:71`
- `components/memberships/MembershipDialog.vue:169`
- `views/legal/TransparencyPage.vue:48` ("Socios activos")
- `views/legal/PrivacyPage.vue:139` ("Datos de socios activos")

Sustituir por:

- "Socio/a activo/a"

### C. Participios en notificaciones tipo toast

Muchos toasts usan participio masculino por referirse al sustantivo (ej: "Extra creado", "Alojamiento actualizado", "Pago confirmado").
**Estos son gramaticalmente correctos** ya que concuerdan con el sustantivo masculino (extra, alojamiento, pago), NO con una persona. No requieren cambio.

### D. Textos legales (Estatutos, Política de Privacidad, Aviso Legal)

Los archivos legales (`BylawsPage.vue`, `PrivacyPage.vue`, `NoticeLegalPage.vue`) contienen texto que probablemente replica documentos oficiales registrados. **Se recomienda NO modificar** a menos que los documentos oficiales se actualicen también.

### E. "Miembro actualizado / Miembro añadido"

`views/FamilyUnitPage.vue:191` - "Miembro" es sustantivo epiceno (vale para ambos géneros). No requiere cambio.

### F. "El rol de X ha sido actualizado"

`components/admin/UsersAdminPanel.vue:55` y `pages/UsersPage.vue:70` - El participio "actualizado" concuerda con "rol" (masculino), no con la persona. No requiere cambio.

---

## Archivos que NO requieren cambios

- Participios que concuerdan con sustantivos masculinos (no con personas): "Pago registrado", "Extra eliminado", "Campamento creado", etc.
- Textos legales oficiales (estatutos, política de privacidad)
- "Miembro" (epiceno)
- Placeholder de email (`usuario@ejemplo.com`) - es una convención

---

## Resumen de alcance

- **Cambios claros a realizar:** ~50 textos en ~15 archivos
- **Pendientes de decisión:** ~20 textos (secciones A y B)
- **Sin cambio necesario:** ~70 textos (participios concordantes, textos legales, epicenos)

## Pasos para implementar

1. Confirmar las decisiones pendientes (secciones A-F)
2. Aplicar los cambios claros por categoría
3. Actualizar los tests que validen textos modificados
4. Verificar visualmente en la aplicación
