<script setup lang="ts">
import QuickAccessCard from './QuickAccessCard.vue'

const cards = [
  {
    icon: 'pi pi-map',
    label: 'Campamento 2026',
    description: '15 días inolvidables en plena naturaleza. Segunda quincena de agosto.',
    ctaLabel: 'Ver detalles',
    ctaPath: '/camp'
  },
  {
    icon: 'pi pi-star',
    label: '50 Aniversario',
    description: 'Celebrando medio siglo de historias. Participa en los eventos conmemorativos.',
    ctaLabel: 'Participar',
    ctaPath: '/anniversary'
  },
  {
    icon: 'pi pi-user',
    label: 'Mi Perfil',
    description: 'Gestiona tu información personal y preferencias.',
    ctaLabel: 'Ver perfil',
    ctaPath: '/profile'
  }
]
</script>

<template>
  <section class="py-12">
    <h2 class="mb-8 text-center text-3xl font-bold text-gray-900">Acceso Rápido</h2>
    <div class="grid grid-cols-1 gap-6 md:grid-cols-2 lg:grid-cols-3">
      <QuickAccessCard
        v-for="card in cards"
        :key="card.label"
        :icon="card.icon"
        :label="card.label"
        :description="card.description"
        :cta-label="card.ctaLabel"
        :cta-path="card.ctaPath"
      />
    </div>
  </section>
</template>
