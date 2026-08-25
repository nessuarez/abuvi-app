<script setup lang="ts">
import { ref } from 'vue'
import AnniversaryHero from '@/components/anniversary/AnniversaryHero.vue'
import AnniversaryJourney from '@/components/anniversary/AnniversaryJourney.vue'
import AnniversaryUploadForm from '@/components/anniversary/AnniversaryUploadForm.vue'
import AnniversaryGallery from '@/components/anniversary/AnniversaryGallery.vue'
import AnniversaryContactForm from '@/components/anniversary/AnniversaryContactForm.vue'

/** Shared between the journey and the gallery: picking a year on the map filters the grid. */
const selectedYear = ref<number | null>(null)
</script>

<template>
  <div>
    <!-- Sticky internal navigation (desktop only) -->
    <nav
      aria-label="Navegación de secciones del aniversario"
      class="sticky top-0 z-30 hidden bg-amber-900/95 shadow-md backdrop-blur-sm md:block"
    >
      <div class="mx-auto flex max-w-6xl items-center justify-center gap-8 px-6 py-3">
        <a
          href="#inicio"
          class="text-sm font-medium text-amber-100 transition-colors hover:text-white"
          >Inicio</a
        >
        <a
          href="#historia"
          class="text-sm font-medium text-amber-100 transition-colors hover:text-white"
          >Historia</a
        >
        <a
          href="#subir-recuerdo"
          class="text-sm font-medium text-amber-100 transition-colors hover:text-white"
          >Comparte</a
        >
        <a
          href="#galeria"
          class="text-sm font-medium text-amber-100 transition-colors hover:text-white"
          >Galería</a
        >
        <a
          href="#contacto"
          class="text-sm font-medium text-amber-100 transition-colors hover:text-white"
          >Contacto</a
        >
      </div>
    </nav>

    <!-- Section 1: Hero -->
    <div id="inicio">
      <AnniversaryHero />
    </div>

    <div class="h-px bg-amber-200" />

    <!-- Section 2: Historical journey (map + venue list + year strip) -->
    <section id="historia" class="bg-amber-50 py-16">
      <AnniversaryJourney @update:year="selectedYear = $event" />
    </section>

    <div class="h-px bg-amber-200" />

    <!-- Section 3: Upload Form -->
    <section class="bg-white py-16">
      <AnniversaryUploadForm />
    </section>

    <div class="h-px bg-amber-200" />

    <!-- Section 4: Gallery -->
    <section id="galeria" class="bg-amber-50 py-16">
      <AnniversaryGallery :year="selectedYear" @clear-year="selectedYear = null" />
    </section>

    <div class="h-px bg-amber-200" />

    <!-- Section 5: Contact Form -->
    <section id="contacto" class="bg-white py-16">
      <AnniversaryContactForm />
    </section>

    <!--
      Narrative timeline: still hidden. AnniversaryTimeline now takes its milestones as a prop,
      but the ones it used to hardcode were invented and some contradicted the imported history
      (it claimed a virtual camp in 2020; there was a real edition at Los Palancares that year).
      It stays out until someone who knows the history writes verified milestones.
      The year-by-year navigation lives in AnniversaryJourney above.
    -->
    <!-- <section id="hitos" class="bg-amber-50 py-16">
      <AnniversaryTimeline :milestones="milestones" />
    </section> -->
  </div>
</template>
