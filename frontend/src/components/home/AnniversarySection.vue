<script setup lang="ts">
import Button from 'primevue/button'
import { useRouter } from 'vue-router'

const router = useRouter()

const goToAnniversary = () => {
  router.push('/anniversary')
}
</script>

<template>
  <section class="py-16">
    <div class="grid grid-cols-1 gap-12 lg:grid-cols-2 lg:items-center">
      <!-- Left Column: Content -->
      <div>
        <p class="mb-2 text-sm font-semibold uppercase tracking-wide text-primary-600">
          Camino al 2026
        </p>
        <h2 class="mb-6 text-4xl font-bold text-gray-900">Medio siglo de ABUVI</h2>

        <div class="space-y-4 text-gray-700">
          <p>
            Desde 1976, hemos sido mucho más que un campamento. Hemos sido una escuela
            de vida, un lugar donde generaciones han aprendido el valor de la amistad, el
            respeto por la naturaleza y la alegría de vivir sencillamente.
          </p>
          <p>
            En 2026 cumplimos 50 años y queremos celebrarlo contigo. Estamos recopilando
            historias, fotos y recuerdos para crear el mayor archivo de nuestra historia.
          </p>
        </div>

        <Button
          label="Participar en el Aniversario"
          icon="pi pi-arrow-right"
          icon-pos="right"
          class="mt-8"
          @click="goToAnniversary"
        />
      </div>

      <!-- Right Column: Images -->
      <div class="relative">
        <img
          src="@/assets/images/grupo-abuvi.jpg"
          alt="Grupo ABUVI - Comunidad reunida"
          class="h-auto w-full rounded-lg shadow-lg"
        />
        <div class="absolute -bottom-6 -right-6 hidden lg:block">
          <img
            src="@/assets/images/50-aniversario-badge.png"
            alt="50 Aniversario ABUVI"
            class="h-32 w-32"
          />
        </div>
      </div>
    </div>
  </section>
</template>
