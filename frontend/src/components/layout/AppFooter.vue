<script setup lang="ts">
const currentYear = new Date().getFullYear()

const linkGroups = [
  {
    title: 'Enlaces',
    links: [
      { label: 'Camp 2026', path: '/camp' },
      { label: '50 Aniversario', path: '/anniversary' },
      { label: 'Mi Perfil', path: '/profile' }
    ]
  },
  {
    title: 'Legal',
    links: [
      { label: 'Aviso Legal', path: '/legal/notice' },
      { label: 'Política de Privacidad', path: '/legal/privacy' },
      { label: 'Estatutos', path: '/legal/bylaws' },
      { label: 'Transparencia', path: '/legal/transparency' }
    ]
  }
]

const contactInfo = {
  email: 'info@abuvi.org',
  phone: '+34 600 000 000'
}
</script>

<template>
  <footer class="border-t border-gray-200 bg-gray-50">
    <div class="mx-auto max-w-screen-xl px-4 py-12 sm:px-6 lg:px-8">
      <div class="grid grid-cols-1 gap-8 md:grid-cols-2 lg:grid-cols-4">
        <!-- Column 1: Branding & Description -->
        <div>
          <h3 class="mb-4 text-lg font-bold text-gray-900">ABUVI</h3>
          <p class="text-sm text-gray-600">
            Amigos de la Buena Vida. Promoviendo la amistad, la naturaleza y la
            convivencia desde 1976.
          </p>
        </div>

        <!-- Column 2: Enlaces -->
        <div>
          <h4 class="mb-4 text-sm font-semibold uppercase text-gray-900">
            {{ linkGroups[0].title }}
          </h4>
          <ul class="space-y-2">
            <li v-for="link in linkGroups[0].links" :key="link.path">
              <router-link :to="link.path" class="text-sm text-gray-600 transition-colors hover:text-primary-600">
                {{ link.label }}
              </router-link>
            </li>
          </ul>
        </div>

        <!-- Column 3: Legal -->
        <div>
          <h4 class="mb-4 text-sm font-semibold uppercase text-gray-900">
            {{ linkGroups[1].title }}
          </h4>
          <ul class="space-y-2">
            <li v-for="link in linkGroups[1].links" :key="link.path">
              <router-link :to="link.path" class="text-sm text-gray-600 transition-colors hover:text-primary-600">
                {{ link.label }}
              </router-link>
            </li>
          </ul>
        </div>

        <!-- Column 4: Contacto -->
        <div>
          <h4 class="mb-4 text-sm font-semibold uppercase text-gray-900">Contacto</h4>

          <!-- Contact Information -->
          <div class="space-y-2 text-sm text-gray-600">
            <p>
              <i class="pi pi-envelope mr-2" />
              <a :href="`mailto:${contactInfo.email}`" class="hover:text-primary-600">
                {{ contactInfo.email }}
              </a>
            </p>
            <p>
              <i class="pi pi-phone mr-2" />
              <a :href="`tel:${contactInfo.phone}`" class="hover:text-primary-600">
                {{ contactInfo.phone }}
              </a>
            </p>
          </div>
        </div>
      </div>

      <!-- Footer Bottom - Copyright & Credits -->
      <div class="mt-8 border-t border-gray-200 pt-8">
        <!-- Copyright -->
        <div class="text-center">
          <p class="text-sm text-gray-600">
            © {{ currentYear }} Asociación ABUVI. Todos los derechos reservados.
          </p>
          <p class="mt-2 text-sm text-gray-500">
            Hecha con ❤️ por Abuvinos para Abuvinos
          </p>
        </div>

        <!-- Open Source Credits -->
        <div class="mt-6 flex flex-col items-center gap-3 text-center sm:flex-row sm:justify-center">
          <div class="flex items-center gap-2">
            <i class="pi pi-github text-gray-600"></i>
            <a href="https://github.com/nessuarez/abuvi-app" target="_blank" rel="noopener noreferrer"
              class="text-sm text-gray-600 hover:text-primary-600 underline">
              Código fuente
            </a>
          </div>
          <span class="hidden sm:inline text-gray-400">•</span>
          <div class="flex items-center gap-2">
            <i class="pi pi-file text-gray-600"></i>
            <a href="https://www.apache.org/licenses/LICENSE-2.0" target="_blank" rel="noopener noreferrer"
              class="text-sm text-gray-600 hover:text-primary-600 underline"
              title="Libre para usar, modificar y distribuir. Incluye protección de patentes.">
              Licencia Apache 2.0
            </a>
          </div>
        </div>
        <p class="mt-3 text-center text-xs text-gray-500">
          Software de código abierto: libre para usar, modificar y compartir
        </p>
      </div>
    </div>
  </footer>
</template>
