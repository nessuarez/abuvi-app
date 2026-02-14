# Frontend Implementation Plan: Spanish Language Support

## Overview

Translate all user-facing frontend text from English to Spanish, including UI components, validation messages, route titles, and error handling. This plan follows **Test-Driven Development (TDD)** principles where applicable.

## TDD Approach for Testable Components

**Critical**: Follow the Red-Green-Refactor cycle for components with tests:

1. **RED**: Update test expectations to Spanish (tests will fail)
2. **GREEN**: Update component code to Spanish (tests will pass)
3. **REFACTOR**: Review and improve (if needed)

**Note**: Many Vue components don't have tests yet. For those, we'll implement directly and add manual testing verification.

## Implementation Phases

### Phase 1: Core Authentication Components (TDD where tests exist)

**Duration**: 2 hours

#### Step 1.1: Update LoginForm Component

**File**: `frontend/src/components/auth/LoginForm.vue`

**Current State**: Has Spanish UI text but English validation messages

**Changes Required**:

**Template Section**:
```vue
<!-- Already Spanish, verify -->
<label>Correo Electrónico</label>
<label>Contraseña</label>
<label>Recordarme</label>
<router-link>¿Olvidaste tu contraseña?</router-link>
<Button label="Iniciar Sesión" />
<p>¿No tienes una cuenta? <router-link>Regístrate</router-link></p>
```

**Script Section - Validation Messages**:
```typescript
// Update validate function
const validate = (): boolean => {
  errors.value = {}

  if (!formData.email.trim()) {
    errors.value.email = 'El correo electrónico es obligatorio'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
    errors.value.email = 'Formato de correo electrónico inválido'
  }

  if (!formData.password) {
    errors.value.password = 'La contraseña es obligatoria'
  }

  return Object.keys(errors.value).length === 0
}
```

**Manual Testing**:
1. Navigate to login page
2. Submit empty form → "El correo electrónico es obligatorio"
3. Enter invalid email → "Formato de correo electrónico inválido"
4. Enter email without password → "La contraseña es obligatoria"

#### Step 1.2: Update RegisterForm Component

**File**: `frontend/src/components/auth/RegisterForm.vue`

**Changes Required**:

**Template Section**:
```vue
<h2>Crear Cuenta</h2>

<!-- Form fields -->
<label for="email">Correo Electrónico</label>
<label for="password">Contraseña</label>
<label for="confirmPassword">Confirmar Contraseña</label>
<label for="firstName">Nombre</label>
<label for="lastName">Apellidos</label>
<label for="documentNumber">Número de Documento (opcional)</label>
<label for="phone">Teléfono (opcional)</label>

<!-- Checkbox -->
<label for="acceptedTerms">
  Acepto los <router-link to="/terms">términos y condiciones</router-link>
</label>

<!-- Buttons -->
<Button label="Registrarse" />
<p>¿Ya tienes una cuenta? <router-link to="/login">Inicia sesión</router-link></p>
```

**Script Section - Validation Messages**:
```typescript
const validate = (): boolean => {
  errors.value = {}

  if (!formData.email.trim()) {
    errors.value.email = 'El correo electrónico es obligatorio'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
    errors.value.email = 'Formato de correo electrónico inválido'
  }

  if (!formData.password) {
    errors.value.password = 'La contraseña es obligatoria'
  } else if (formData.password.length < 8) {
    errors.value.password = 'La contraseña debe tener al menos 8 caracteres'
  } else if (!/[A-Z]/.test(formData.password)) {
    errors.value.password = 'La contraseña debe contener al menos una letra mayúscula'
  } else if (!/[a-z]/.test(formData.password)) {
    errors.value.password = 'La contraseña debe contener al menos una letra minúscula'
  } else if (!/\d/.test(formData.password)) {
    errors.value.password = 'La contraseña debe contener al menos un dígito'
  } else if (!/[@$!%*?&#]/.test(formData.password)) {
    errors.value.password = 'La contraseña debe contener al menos un carácter especial'
  }

  if (!formData.confirmPassword) {
    errors.value.confirmPassword = 'Confirmar contraseña es obligatorio'
  } else if (formData.password !== formData.confirmPassword) {
    errors.value.confirmPassword = 'Las contraseñas deben coincidir'
  }

  if (!formData.firstName.trim()) {
    errors.value.firstName = 'El nombre es obligatorio'
  }

  if (!formData.lastName.trim()) {
    errors.value.lastName = 'Los apellidos son obligatorios'
  }

  if (formData.documentNumber && !/^[A-Z0-9]+$/.test(formData.documentNumber)) {
    errors.value.documentNumber = 'El número de documento solo debe contener letras mayúsculas y números'
  }

  if (formData.phone && !/^\+?[1-9]\d{1,14}$/.test(formData.phone)) {
    errors.value.phone = 'Formato de número de teléfono inválido'
  }

  if (!formData.acceptedTerms) {
    errors.value.acceptedTerms = 'Debes aceptar los términos y condiciones'
  }

  return Object.keys(errors.value).length === 0
}
```

**Manual Testing**:
1. Navigate to register page
2. Test all validation scenarios
3. Verify all messages in Spanish

#### Step 1.3: Update AuthContainer Component

**File**: `frontend/src/components/auth/AuthContainer.vue`

**Changes Required**:
```vue
<!-- Update any English text -->
<h1>Bienvenido a Abuvi</h1>
<p>Gestiona tus campamentos y aniversarios</p>
```

---

### Phase 2: Layout Components

**Duration**: 1.5 hours

#### Step 2.1: Update AppHeader Component

**File**: `frontend/src/components/layout/AppHeader.vue`

**Current State**: All English navigation labels

**Changes Required**:

**Template Section**:
```vue
<template>
  <header>
    <!-- Logo -->
    <router-link to="/home">
      <span>ABUVI</span>
    </router-link>

    <!-- Navigation -->
    <nav>
      <router-link to="/home">Inicio</router-link>
      <router-link to="/camp">Campamento</router-link>
      <router-link to="/anniversary">Aniversario</router-link>
      <router-link to="/users" v-if="isBoard">Usuarios</router-link>
      <router-link to="/admin" v-if="isAdmin">Administración</router-link>
    </nav>

    <!-- User Menu -->
    <div>
      <router-link to="/profile">Mi Perfil</router-link>
      <button @click="handleLogout">Cerrar Sesión</button>
    </div>
  </header>
</template>
```

**Manual Testing**:
1. Check all navigation labels are Spanish
2. Verify logout button text

#### Step 2.2: Update AppFooter Component

**File**: `frontend/src/components/layout/AppFooter.vue`

**Changes Required**:
```vue
<template>
  <footer>
    <p>&copy; 2026 Abuvi. Todos los derechos reservados.</p>
    <div>
      <router-link to="/about">Acerca de</router-link>
      <router-link to="/privacy">Privacidad</router-link>
      <router-link to="/terms">Términos</router-link>
      <router-link to="/contact">Contacto</router-link>
    </div>
  </footer>
</template>
```

#### Step 2.3: Update UserMenu Component

**File**: `frontend/src/components/layout/UserMenu.vue`

**Changes Required**:
```vue
<template>
  <div class="user-menu">
    <button>{{ user.firstName }} {{ user.lastName }}</button>
    <div class="dropdown">
      <router-link to="/profile">Mi Perfil</router-link>
      <router-link to="/settings">Configuración</router-link>
      <button @click="logout">Cerrar Sesión</button>
    </div>
  </div>
</template>
```

---

### Phase 3: Router Meta Titles

**Duration**: 30 minutes

#### Step 3.1: Update Router Configuration

**File**: `frontend/src/router/index.ts`

**Changes Required**:

```typescript
const routes: RouteRecordRaw[] = [
  {
    path: '/',
    component: LandingPage,
    meta: { title: 'ABUVI - Bienvenido' }
  },
  {
    path: '/login',
    component: LoginPage,
    meta: { title: 'Iniciar Sesión - ABUVI' }
  },
  {
    path: '/register',
    component: RegisterPage,
    meta: { title: 'Registrarse - ABUVI' }
  },
  {
    path: '/home',
    component: HomePage,
    meta: { title: 'Inicio - ABUVI', requiresAuth: true }
  },
  {
    path: '/camp',
    component: CampPage,
    meta: { title: 'Campamento - ABUVI', requiresAuth: true }
  },
  {
    path: '/anniversary',
    component: AnniversaryPage,
    meta: { title: 'Aniversario - ABUVI', requiresAuth: true }
  },
  {
    path: '/profile',
    component: ProfilePage,
    meta: { title: 'Mi Perfil - ABUVI', requiresAuth: true }
  },
  {
    path: '/users',
    component: UsersPage,
    meta: { title: 'Usuarios - ABUVI', requiresAuth: true, requiresBoard: true }
  },
  {
    path: '/users/:id',
    component: UserDetailPage,
    meta: { title: 'Detalle de Usuario - ABUVI', requiresAuth: true, requiresBoard: true }
  },
  {
    path: '/admin',
    component: AdminPage,
    meta: { title: 'Administración - ABUVI', requiresAuth: true, requiresAdmin: true }
  }
]
```

**Manual Testing**:
1. Navigate to each route
2. Verify browser tab title is in Spanish

---

### Phase 4: Home & Quick Access Components

**Duration**: 1.5 hours

#### Step 4.1: Update LandingPage View

**File**: `frontend/src/views/LandingPage.vue`

**Changes Required**:
```vue
<template>
  <div class="landing-page">
    <h1>Bienvenido a Abuvi</h1>
    <p class="subtitle">Gestión de campamentos y aniversarios para toda la familia</p>

    <div class="features">
      <div class="feature">
        <h3>Campamentos</h3>
        <p>Organiza y gestiona los campamentos de verano</p>
      </div>
      <div class="feature">
        <h3>Aniversarios</h3>
        <p>Celebra y recuerda momentos especiales</p>
      </div>
      <div class="feature">
        <h3>Familia</h3>
        <p>Mantén unida a tu familia a través de las actividades</p>
      </div>
    </div>

    <div class="cta">
      <router-link to="/register">
        <Button label="Comenzar Ahora" />
      </router-link>
      <router-link to="/login">
        <Button label="Iniciar Sesión" severity="secondary" />
      </router-link>
    </div>
  </div>
</template>
```

#### Step 4.2: Update HomePage View

**File**: `frontend/src/views/HomePage.vue`

**Changes Required**:
```vue
<template>
  <div class="home-page">
    <h1>Bienvenido, {{ user?.firstName }}</h1>
    <p class="welcome-message">¿Qué te gustaría hacer hoy?</p>

    <QuickAccessCards />
    <AnniversarySection />
  </div>
</template>
```

#### Step 4.3: Update QuickAccessCards Component

**File**: `frontend/src/components/home/QuickAccessCards.vue`

**Changes Required**:
```vue
<template>
  <div class="quick-access-cards">
    <h2>Acceso Rápido</h2>
    <div class="cards-grid">
      <QuickAccessCard
        title="Campamentos"
        description="Ver próximos campamentos"
        icon="pi pi-sun"
        to="/camp"
      />
      <QuickAccessCard
        title="Aniversarios"
        description="Consultar fechas importantes"
        icon="pi pi-calendar"
        to="/anniversary"
      />
      <QuickAccessCard
        title="Mi Perfil"
        description="Actualizar información personal"
        icon="pi pi-user"
        to="/profile"
      />
      <QuickAccessCard
        v-if="isBoard"
        title="Usuarios"
        description="Gestionar usuarios del sistema"
        icon="pi pi-users"
        to="/users"
      />
    </div>
  </div>
</template>
```

#### Step 4.4: Update AnniversarySection Component

**File**: `frontend/src/components/home/AnniversarySection.vue`

**Changes Required**:
```vue
<template>
  <div class="anniversary-section">
    <h2>Próximos Aniversarios</h2>
    <p v-if="!anniversaries.length">No hay aniversarios próximos</p>
    <div v-else class="anniversaries-list">
      <!-- Anniversary items -->
    </div>
    <router-link to="/anniversary">
      <Button label="Ver Todos" severity="secondary" />
    </router-link>
  </div>
</template>
```

---

### Phase 5: User Management Components (TDD where tests exist)

**Duration**: 2 hours

#### Step 5.1: Update UserCard Component Tests (RED)

**File**: `frontend/src/components/users/__tests__/UserCard.test.ts` (if exists)

**Update assertions to expect**:
- "Administrador", "Junta Directiva", "Socio", "Tutor" (roles)
- "Activo", "Inactivo" (status)

#### Step 5.2: Update UserCard Component (GREEN)

**File**: `frontend/src/components/users/UserCard.vue`

**Changes Required**:

**Template Section**:
```vue
<template>
  <Card>
    <template #header>
      <div class="user-avatar">
        {{ user.firstName[0] }}{{ user.lastName[0] }}
      </div>
    </template>
    <template #title>
      {{ user.firstName }} {{ user.lastName }}
    </template>
    <template #subtitle>
      {{ user.email }}
    </template>
    <template #content>
      <div class="user-details">
        <div class="detail-row">
          <span class="label">Rol:</span>
          <span class="value">{{ translateRole(user.role) }}</span>
        </div>
        <div class="detail-row">
          <span class="label">Estado:</span>
          <Tag
            :value="user.isActive ? 'Activo' : 'Inactivo'"
            :severity="user.isActive ? 'success' : 'danger'"
          />
        </div>
        <div class="detail-row" v-if="user.documentNumber">
          <span class="label">Documento:</span>
          <span class="value">{{ user.documentNumber }}</span>
        </div>
        <div class="detail-row" v-if="user.phone">
          <span class="label">Teléfono:</span>
          <span class="value">{{ user.phone }}</span>
        </div>
      </div>
    </template>
    <template #footer>
      <div class="actions">
        <Button label="Ver Detalles" @click="viewDetails" />
        <Button
          v-if="canEditRole"
          label="Editar Rol"
          severity="secondary"
          @click="editRole"
        />
      </div>
    </template>
  </Card>
</template>

<script setup lang="ts">
const translateRole = (role: string): string => {
  const roleMap: Record<string, string> = {
    'Admin': 'Administrador',
    'Board': 'Junta Directiva',
    'Member': 'Socio',
    'Guardian': 'Tutor'
  }
  return roleMap[role] || role
}
</script>
```

#### Step 5.3: Update UserForm Component Tests (RED then GREEN)

**File**: `frontend/src/components/users/__tests__/UserForm.test.ts` (if exists)

Update assertions and then update component.

#### Step 5.4: Update UserForm Component

**File**: `frontend/src/components/users/UserForm.vue`

**Changes Required**:
```vue
<template>
  <form @submit.prevent="handleSubmit">
    <h2>{{ isEdit ? 'Editar Usuario' : 'Crear Usuario' }}</h2>

    <div class="form-field">
      <label for="firstName">Nombre *</label>
      <InputText id="firstName" v-model="formData.firstName" />
      <small v-if="errors.firstName" class="p-error">{{ errors.firstName }}</small>
    </div>

    <div class="form-field">
      <label for="lastName">Apellidos *</label>
      <InputText id="lastName" v-model="formData.lastName" />
      <small v-if="errors.lastName" class="p-error">{{ errors.lastName }}</small>
    </div>

    <div class="form-field">
      <label for="email">Correo Electrónico *</label>
      <InputText id="email" v-model="formData.email" type="email" />
      <small v-if="errors.email" class="p-error">{{ errors.email }}</small>
    </div>

    <div class="form-field">
      <label for="documentNumber">Número de Documento</label>
      <InputText id="documentNumber" v-model="formData.documentNumber" />
      <small v-if="errors.documentNumber" class="p-error">{{ errors.documentNumber }}</small>
    </div>

    <div class="form-field">
      <label for="phone">Teléfono</label>
      <InputText id="phone" v-model="formData.phone" />
      <small v-if="errors.phone" class="p-error">{{ errors.phone }}</small>
    </div>

    <div class="form-field">
      <label for="role">Rol *</label>
      <Dropdown
        id="role"
        v-model="formData.role"
        :options="roleOptions"
        optionLabel="label"
        optionValue="value"
      />
      <small v-if="errors.role" class="p-error">{{ errors.role }}</small>
    </div>

    <div class="form-actions">
      <Button label="Guardar" type="submit" />
      <Button label="Cancelar" severity="secondary" @click="handleCancel" />
    </div>
  </form>
</template>

<script setup lang="ts">
const roleOptions = [
  { label: 'Administrador', value: 'Admin' },
  { label: 'Junta Directiva', value: 'Board' },
  { label: 'Socio', value: 'Member' },
  { label: 'Tutor', value: 'Guardian' }
]

// Validation messages in Spanish
const validate = (): boolean => {
  errors.value = {}

  if (!formData.firstName.trim()) {
    errors.value.firstName = 'El nombre es obligatorio'
  }

  if (!formData.lastName.trim()) {
    errors.value.lastName = 'Los apellidos son obligatorios'
  }

  if (!formData.email.trim()) {
    errors.value.email = 'El correo electrónico es obligatorio'
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
    errors.value.email = 'Formato de correo electrónico inválido'
  }

  return Object.keys(errors.value).length === 0
}
</script>
```

#### Step 5.5: Update UserRoleDialog Component

**File**: `frontend/src/components/users/UserRoleDialog.vue`

**Changes Required**:
```vue
<template>
  <Dialog
    :visible="visible"
    @update:visible="$emit('update:visible', $event)"
    :header="`Cambiar Rol de ${user?.firstName} ${user?.lastName}`"
    :modal="true"
  >
    <div class="role-dialog-content">
      <p>Rol actual: <Tag :value="translateRole(user?.role)" /></p>

      <div class="form-field">
        <label for="newRole">Nuevo Rol</label>
        <Dropdown
          id="newRole"
          v-model="selectedRole"
          :options="roleOptions"
          optionLabel="label"
          optionValue="value"
        />
      </div>

      <Message v-if="error" severity="error">{{ error }}</Message>
    </div>

    <template #footer>
      <Button label="Cancelar" severity="secondary" @click="handleCancel" />
      <Button label="Guardar" @click="handleSave" :loading="loading" />
    </template>
  </Dialog>
</template>

<script setup lang="ts">
const roleOptions = [
  { label: 'Administrador', value: 'Admin' },
  { label: 'Junta Directiva', value: 'Board' },
  { label: 'Socio', value: 'Member' },
  { label: 'Tutor', value: 'Guardian' }
]

const translateRole = (role: string): string => {
  const roleMap: Record<string, string> = {
    'Admin': 'Administrador',
    'Board': 'Junta Directiva',
    'Member': 'Socio',
    'Guardian': 'Tutor'
  }
  return roleMap[role] || role
}
</script>
```

---

### Phase 6: Views & Pages

**Duration**: 2 hours

#### Step 6.1: Update ProfilePage View

**File**: `frontend/src/views/ProfilePage.vue`

**Changes Required**:
```vue
<template>
  <div class="profile-page">
    <h1>Mi Perfil</h1>

    <Card>
      <template #title>Información Personal</template>
      <template #content>
        <div class="profile-info">
          <div class="info-row">
            <span class="label">Nombre:</span>
            <span class="value">{{ user?.firstName }} {{ user?.lastName }}</span>
          </div>
          <div class="info-row">
            <span class="label">Correo:</span>
            <span class="value">{{ user?.email }}</span>
          </div>
          <div class="info-row">
            <span class="label">Rol:</span>
            <span class="value">{{ translateRole(user?.role) }}</span>
          </div>
          <div class="info-row" v-if="user?.documentNumber">
            <span class="label">Documento:</span>
            <span class="value">{{ user?.documentNumber }}</span>
          </div>
          <div class="info-row" v-if="user?.phone">
            <span class="label">Teléfono:</span>
            <span class="value">{{ user?.phone }}</span>
          </div>
        </div>
      </template>
      <template #footer>
        <Button label="Editar Perfil" @click="editProfile" />
        <Button label="Cambiar Contraseña" severity="secondary" @click="changePassword" />
      </template>
    </Card>
  </div>
</template>
```

#### Step 6.2: Update UsersPage (legacy)

**File**: `frontend/src/pages/UsersPage.vue`

**Changes Required**:
```vue
<template>
  <div class="users-page">
    <h1>Usuarios</h1>

    <div class="page-header">
      <InputText
        v-model="searchQuery"
        placeholder="Buscar usuarios..."
        class="search-input"
      />
      <Button label="Nuevo Usuario" @click="createUser" />
    </div>

    <DataTable :value="users" :loading="loading">
      <Column field="firstName" header="Nombre" />
      <Column field="lastName" header="Apellidos" />
      <Column field="email" header="Correo" />
      <Column field="role" header="Rol">
        <template #body="{ data }">
          {{ translateRole(data.role) }}
        </template>
      </Column>
      <Column field="isActive" header="Estado">
        <template #body="{ data }">
          <Tag
            :value="data.isActive ? 'Activo' : 'Inactivo'"
            :severity="data.isActive ? 'success' : 'danger'"
          />
        </template>
      </Column>
      <Column header="Acciones">
        <template #body="{ data }">
          <Button label="Ver" @click="viewUser(data.id)" />
          <Button label="Editar" severity="secondary" @click="editUser(data.id)" />
        </template>
      </Column>
    </DataTable>

    <p v-if="!loading && !users.length" class="no-results">
      No se encontraron usuarios
    </p>
  </div>
</template>
```

#### Step 6.3: Update CampPage View

**File**: `frontend/src/views/CampPage.vue`

**Changes Required**:
```vue
<template>
  <div class="camp-page">
    <h1>Campamentos</h1>
    <p class="subtitle">Próximos campamentos y actividades</p>

    <div class="camps-grid">
      <!-- Camp cards will go here -->
      <Card v-for="camp in camps" :key="camp.id">
        <template #title>{{ camp.name }}</template>
        <template #content>
          <p>Fecha: {{ formatDate(camp.startDate) }} - {{ formatDate(camp.endDate) }}</p>
          <p>Ubicación: {{ camp.location }}</p>
        </template>
        <template #footer>
          <Button label="Ver Detalles" @click="viewCamp(camp.id)" />
          <Button label="Inscribirse" severity="success" @click="register(camp.id)" />
        </template>
      </Card>
    </div>

    <p v-if="!camps.length" class="no-results">
      No hay campamentos programados
    </p>
  </div>
</template>
```

#### Step 6.4: Update AnniversaryPage View

**File**: `frontend/src/views/AnniversaryPage.vue`

**Changes Required**:
```vue
<template>
  <div class="anniversary-page">
    <h1>Aniversarios</h1>
    <p class="subtitle">Fechas importantes y celebraciones</p>

    <div class="filters">
      <Calendar v-model="filterDate" placeholder="Filtrar por fecha" />
      <Button label="Limpiar Filtros" severity="secondary" @click="clearFilters" />
    </div>

    <div class="anniversaries-list">
      <!-- Anniversary items -->
    </div>

    <p v-if="!anniversaries.length" class="no-results">
      No hay aniversarios para mostrar
    </p>
  </div>
</template>
```

#### Step 6.5: Update AdminPage View

**File**: `frontend/src/views/AdminPage.vue`

**Changes Required**:
```vue
<template>
  <div class="admin-page">
    <h1>Administración</h1>
    <p class="subtitle">Panel de administración del sistema</p>

    <div class="admin-sections">
      <Card>
        <template #title>Gestión de Usuarios</template>
        <template #content>
          <p>Administra roles y permisos de usuarios</p>
        </template>
        <template #footer>
          <Button label="Ir a Usuarios" @click="goToUsers" />
        </template>
      </Card>

      <Card>
        <template #title>Configuración del Sistema</template>
        <template #content>
          <p>Configura parámetros generales</p>
        </template>
        <template #footer>
          <Button label="Configurar" @click="goToSettings" />
        </template>
      </Card>

      <Card>
        <template #title>Reportes</template>
        <template #content>
          <p>Genera reportes y estadísticas</p>
        </template>
        <template #footer>
          <Button label="Ver Reportes" @click="goToReports" />
        </template>
      </Card>
    </div>
  </div>
</template>
```

---

### Phase 7: Stores & Composables Error Handling

**Duration**: 1.5 hours

#### Step 7.1: Update Auth Store Tests (RED)

**File**: `frontend/src/stores/__tests__/auth.test.ts` (if exists)

Update assertions to expect Spanish error messages.

#### Step 7.2: Update Auth Store (GREEN)

**File**: `frontend/src/stores/auth.ts`

**Changes Required**:

```typescript
async function login(credentials: LoginRequest): Promise<{ success: boolean; error?: string }> {
  try {
    const response = await api.post<ApiResponse<AuthResponse>>('/auth/login', credentials)

    if (response.data.success && response.data.data) {
      token.value = response.data.data.token
      user.value = response.data.data.user
      localStorage.setItem('auth_token', response.data.data.token)
      return { success: true }
    }

    return {
      success: false,
      error: response.data.error?.message || 'Error al iniciar sesión'
    }
  } catch (error: any) {
    if (error.response?.data?.error?.message) {
      return { success: false, error: error.response.data.error.message }
    }
    return { success: false, error: 'Ocurrió un error durante el inicio de sesión' }
  }
}

async function register(data: RegisterUserRequest): Promise<{ success: boolean; error?: string }> {
  try {
    const response = await api.post<ApiResponse<AuthResponse>>('/auth/register', data)

    if (response.data.success) {
      return { success: true }
    }

    return {
      success: false,
      error: response.data.error?.message || 'Error al registrarse'
    }
  } catch (error: any) {
    if (error.response?.data?.error?.message) {
      return { success: false, error: error.response.data.error.message }
    }
    return { success: false, error: 'Ocurrió un error durante el registro' }
  }
}

async function logout(): Promise<void> {
  try {
    // Call logout endpoint if exists
    await api.post('/auth/logout')
  } catch (error) {
    console.error('Error al cerrar sesión:', error)
  } finally {
    token.value = null
    user.value = null
    localStorage.removeItem('auth_token')
  }
}
```

#### Step 7.3: Update useAuth Composable

**File**: `frontend/src/composables/useAuth.ts`

**Changes Required**:
```typescript
export function useAuth() {
  const authStore = useAuthStore()
  const router = useRouter()
  const toast = useToast()

  const login = async (credentials: LoginRequest) => {
    const result = await authStore.login(credentials)

    if (result.success) {
      toast.add({
        severity: 'success',
        summary: 'Éxito',
        detail: 'Has iniciado sesión correctamente',
        life: 3000
      })
      await router.push('/home')
    } else {
      toast.add({
        severity: 'error',
        summary: 'Error',
        detail: result.error || 'Error al iniciar sesión',
        life: 5000
      })
    }
  }

  const register = async (data: RegisterUserRequest) => {
    const result = await authStore.register(data)

    if (result.success) {
      toast.add({
        severity: 'success',
        summary: 'Éxito',
        detail: 'Registro exitoso. Por favor verifica tu correo electrónico.',
        life: 5000
      })
      await router.push('/login')
    } else {
      toast.add({
        severity: 'error',
        summary: 'Error',
        detail: result.error || 'Error al registrarse',
        life: 5000
      })
    }
  }

  const logout = async () => {
    await authStore.logout()
    toast.add({
      severity: 'info',
      summary: 'Sesión Cerrada',
      detail: 'Has cerrado sesión correctamente',
      life: 3000
    })
    await router.push('/login')
  }

  return {
    login,
    register,
    logout,
    user: computed(() => authStore.user),
    isAuthenticated: computed(() => authStore.isAuthenticated)
  }
}
```

#### Step 7.4: Update useUsers Composable

**File**: `frontend/src/composables/useUsers.ts`

**Changes Required**:
```typescript
export function useUsers() {
  const toast = useToast()
  const loading = ref(false)
  const users = ref<User[]>([])

  const fetchUsers = async () => {
    loading.value = true
    try {
      const response = await api.get<ApiResponse<User[]>>('/users')
      if (response.data.success && response.data.data) {
        users.value = response.data.data
      }
    } catch (error: any) {
      toast.add({
        severity: 'error',
        summary: 'Error',
        detail: error.response?.data?.error?.message || 'Error al cargar usuarios',
        life: 5000
      })
    } finally {
      loading.value = false
    }
  }

  const updateUserRole = async (userId: string, role: string) => {
    loading.value = true
    try {
      const response = await api.patch<ApiResponse<User>>(
        `/users/${userId}/role`,
        { role }
      )

      if (response.data.success) {
        toast.add({
          severity: 'success',
          summary: 'Éxito',
          detail: 'Rol actualizado correctamente',
          life: 3000
        })
        await fetchUsers()
        return { success: true }
      }

      return {
        success: false,
        error: response.data.error?.message || 'Error al actualizar rol'
      }
    } catch (error: any) {
      const errorMessage = error.response?.data?.error?.message || 'Error al actualizar rol'
      toast.add({
        severity: 'error',
        summary: 'Error',
        detail: errorMessage,
        life: 5000
      })
      return { success: false, error: errorMessage }
    } finally {
      loading.value = false
    }
  }

  return {
    users,
    loading,
    fetchUsers,
    updateUserRole
  }
}
```

---

### Phase 8: Frontend Tests Update (TDD)

**Duration**: 1.5 hours

#### Step 8.1: Update All Existing Tests

**Files to check and update**:
- `frontend/src/components/users/__tests__/UserForm.test.ts`
- `frontend/src/components/users/__tests__/UserCard.test.ts`
- `frontend/src/composables/__tests__/useAuth.test.ts`
- `frontend/src/composables/__tests__/useUsers.test.ts`
- `frontend/src/stores/__tests__/auth.test.ts`

**For each test file**:
1. Update all assertions to expect Spanish text
2. Run tests: `npm run test:unit`
3. Fix any failing tests
4. Verify all tests pass

**Example test updates**:
```typescript
// Before
expect(errorMessage).toBe('Email is required')
expect(buttonText).toBe('Login')
expect(roleLabel).toBe('Admin')

// After
expect(errorMessage).toBe('El correo electrónico es obligatorio')
expect(buttonText).toBe('Iniciar Sesión')
expect(roleLabel).toBe('Administrador')
```

#### Step 8.2: Run Full Test Suite

**Verification**:
```bash
npm run test:unit
```

Expected: All tests PASS

---

### Phase 9: Documentation Updates

**Duration**: 30 minutes

#### Step 9.1: Update Frontend Standards

**File**: `ai-specs/specs/frontend-standards.mdc`

**Add section** (append to existing content):

```markdown
## Language Standards

### User-Facing Text

- **Default Language**: Spanish (Castellano)
- **Tone**: Informal and friendly (use "tú" form, not "usted")
- All UI text, labels, buttons, navigation, error messages, and validation messages must be in Spanish
- Use gender-neutral language where possible

### Code and Documentation

- **Code**: English only (variables, functions, classes, interfaces, types)
- **Comments**: English only
- **Commit Messages**: English only
- **Log Messages**: English only (for developer debugging)
- **Console Messages**: English only

### Common Translations

#### Navigation & Actions
| English | Spanish |
|---------|---------|
| Home | Inicio |
| Camp | Campamento |
| Anniversary | Aniversario |
| My Profile | Mi Perfil |
| Users | Usuarios |
| Admin | Administración |
| Logout | Cerrar Sesión |
| Login | Iniciar Sesión |
| Register | Registrarse |
| Submit | Enviar |
| Save | Guardar |
| Cancel | Cancelar |
| Delete | Eliminar |
| Edit | Editar |
| Create | Crear |
| Search | Buscar |

#### Form Fields
| English | Spanish |
|---------|---------|
| Email | Correo Electrónico |
| Password | Contraseña |
| First Name | Nombre |
| Last Name | Apellidos |
| Phone | Teléfono |
| Document Number | Número de Documento |
| Role | Rol |
| Status | Estado |

#### User Roles
| English | Spanish |
|---------|---------|
| Admin | Administrador |
| Board | Junta Directiva |
| Member | Socio |
| Guardian | Tutor |

#### Validation Messages
| English | Spanish |
|---------|---------|
| {field} is required | {field} es obligatorio/a |
| Invalid email format | Formato de correo electrónico inválido |
| Password must be at least 8 characters | La contraseña debe tener al menos 8 caracteres |
| Passwords must match | Las contraseñas deben coincidir |

#### Status Messages
| English | Spanish |
|---------|---------|
| Loading... | Cargando... |
| Success | Éxito |
| Error | Error |
| No results found | No se encontraron resultados |
| Active | Activo |
| Inactive | Inactivo |

### Gender Agreement Rules

Spanish adjectives must agree with noun gender:

**Masculine nouns**: el campo, el correo, el nombre, el teléfono
- "El correo electrónico es **obligatorio**"
- "El nombre es **obligatorio**"

**Feminine nouns**: la contraseña, la fecha, la dirección
- "La contraseña es **obligatoria**"
- "La fecha es **obligatoria**"

### Validation Message Patterns

**Client-side validation**:
```typescript
// ✅ Good: Spanish with correct gender
if (!formData.email.trim()) {
  errors.value.email = 'El correo electrónico es obligatorio' // masculine
}

if (!formData.password) {
  errors.value.password = 'La contraseña es obligatoria' // feminine
}

// ❌ Bad: English
if (!formData.email.trim()) {
  errors.value.email = 'Email is required'
}
```

### Toast Notifications

Use consistent Spanish messages:
```typescript
// Success
toast.add({
  severity: 'success',
  summary: 'Éxito',
  detail: 'Operación completada correctamente',
  life: 3000
})

// Error
toast.add({
  severity: 'error',
  summary: 'Error',
  detail: errorMessage || 'Ocurrió un error',
  life: 5000
})

// Info
toast.add({
  severity: 'info',
  summary: 'Información',
  detail: 'Información actualizada',
  life: 3000
})
```

### Role Translation Helper

Always use a helper function for role translation:
```typescript
const translateRole = (role: string): string => {
  const roleMap: Record<string, string> = {
    'Admin': 'Administrador',
    'Board': 'Junta Directiva',
    'Member': 'Socio',
    'Guardian': 'Tutor'
  }
  return roleMap[role] || role
}
```
```

---

## Manual Testing Checklist

### Authentication Flow
- [ ] Navigate to landing page → All text in Spanish
- [ ] Go to register page → All labels and buttons in Spanish
- [ ] Submit empty form → Spanish validation errors
- [ ] Register with valid data → Spanish success message
- [ ] Check email → Verification email in Spanish
- [ ] Verify email → Welcome email in Spanish
- [ ] Login page → All text in Spanish
- [ ] Invalid login → Spanish error message
- [ ] Valid login → Spanish success toast

### Navigation
- [ ] Check all navigation links → Spanish labels
- [ ] Verify all route titles → Spanish browser tab titles
- [ ] Check footer links → Spanish labels
- [ ] User menu → Spanish options

### User Management
- [ ] Users page → All UI in Spanish
- [ ] User card → Roles translated, status in Spanish
- [ ] Create user → Form in Spanish
- [ ] Edit user role → Dialog in Spanish
- [ ] Validation errors → Spanish messages
- [ ] Success messages → Spanish toasts

### Other Pages
- [ ] Home page → Spanish welcome message
- [ ] Quick access cards → Spanish titles and descriptions
- [ ] Camp page → Spanish content
- [ ] Anniversary page → Spanish content
- [ ] Profile page → Spanish labels and content
- [ ] Admin page → Spanish content

### Error Handling
- [ ] API errors → Display in Spanish
- [ ] Network errors → Spanish fallback messages
- [ ] 404 page → Spanish content
- [ ] Unauthorized → Spanish error message

### Responsive Design
- [ ] Test on mobile → All Spanish text visible
- [ ] Test on tablet → Layout and text correct
- [ ] Test on desktop → Full experience in Spanish

---

## Verification Checklist

### Code Review
- [ ] All component templates use Spanish text
- [ ] All validation messages in Spanish
- [ ] All toast notifications in Spanish
- [ ] All error messages in Spanish
- [ ] Role translations implemented consistently
- [ ] Gender agreement correct in validation messages
- [ ] No English text in user-facing areas
- [ ] Code, comments, and logs remain in English

### Testing
- [ ] All unit tests passing
- [ ] All component tests updated to expect Spanish
- [ ] Manual testing completed
- [ ] No console errors
- [ ] All features working correctly

### Documentation
- [ ] Frontend standards updated
- [ ] Translation reference provided
- [ ] Examples included
- [ ] Gender agreement rules documented

---

## Success Criteria

1. ✅ All UI text in Spanish (0% English in user interface)
2. ✅ All validation messages in Spanish with correct gender agreement
3. ✅ All route meta titles in Spanish
4. ✅ All error messages and toasts in Spanish
5. ✅ Role translations implemented consistently
6. ✅ Frontend standards documentation updated
7. ✅ All tests passing (unit tests)
8. ✅ Manual testing confirms full Spanish experience
9. ✅ No mixed English/Spanish in any component
10. ✅ Code, comments, and logs remain in English (per standards)

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Missing translations | Use comprehensive component checklist |
| Grammatical errors | Use provided translation reference, verify gender agreement |
| Test failures | Update tests incrementally with component changes |
| Inconsistent role translations | Use shared helper function |
| Mixed languages | Manual testing checklist, thorough review |

---

## Estimated Time

- Phase 1: Core Auth Components - 2 hours
- Phase 2: Layout Components - 1.5 hours
- Phase 3: Router Meta Titles - 30 minutes
- Phase 4: Home & Quick Access - 1.5 hours
- Phase 5: User Management - 2 hours
- Phase 6: Views & Pages - 2 hours
- Phase 7: Stores & Composables - 1.5 hours
- Phase 8: Frontend Tests - 1.5 hours
- Phase 9: Documentation - 30 minutes

**Total**: ~13 hours

---

## Dependencies

- PrimeVue components (already installed)
- Vue Router (already configured)
- Pinia stores (already implemented)
- Backend API must return Spanish error messages (separate ticket)

---

## Next Steps

After frontend completion:
1. Verify backend Spanish implementation is complete
2. Full end-to-end testing
3. User acceptance testing
4. Deploy to staging environment

---

**Plan Status**: Ready for development
**TDD Approach**: ✅ Enforced where tests exist
**Estimated Effort**: ~13 hours
**Priority**: High
