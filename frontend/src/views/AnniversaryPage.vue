<script setup lang="ts">
import AnniversaryHero from '@/components/anniversary/AnniversaryHero.vue'
import AnniversaryUploadForm from '@/components/anniversary/AnniversaryUploadForm.vue'
import AnniversaryGallery from '@/components/anniversary/AnniversaryGallery.vue'
import AnniversaryContactForm from '@/components/anniversary/AnniversaryContactForm.vue'
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

    <!-- Section 2: Upload Form -->
    <section class="bg-white py-16">
      <AnniversaryUploadForm />
    </section>

    <div class="h-px bg-amber-200" />

    <!-- Section 3: Gallery -->
    <section id="galeria" class="bg-amber-50 py-16">
      <AnniversaryGallery />
    </section>

    <div class="h-px bg-amber-200" />

    <!-- Section 4: Contact Form -->
    <section id="contacto" class="bg-white py-16">
      <AnniversaryContactForm />
    </section>

    <!-- Timeline: hidden until real milestones data is available -->
    <!-- <section id="historia" class="bg-amber-50 py-16">
      <AnniversaryTimeline />
    </section> -->
  </div>
</template>
