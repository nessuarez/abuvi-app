<script setup lang="ts">
import { RouterLink } from 'vue-router'
import Button from 'primevue/button'
import TableOfContents from '@/components/legal/TableOfContents.vue'

// TocEntry type must match TableOfContents component interface
interface TocEntry {
  id: string
  label: string
  level: 1 | 2
}

interface Props {
  title: string
  lastUpdated: string
  tocEntries?: TocEntry[]
  showToc?: boolean
  showPrintButton?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  tocEntries: () => [],
  showToc: true,
  showPrintButton: true
})

const handlePrint = () => {
  window.print()
}
</script>

<template>
  <div class="min-h-screen bg-white">
    <!-- Top navigation bar — always visible, works without AppHeader -->
    <div class="legal-top-bar border-b border-gray-200 bg-gray-50">
      <div class="mx-auto flex max-w-5xl items-center justify-between px-4 py-3 sm:px-6">
        <RouterLink
          to="/"
          class="text-lg font-bold text-primary-700 transition-colors hover:text-primary-900"
        >
          ABUVI
        </RouterLink>
        <RouterLink
          to="/"
          data-testid="legal-back-link"
          class="flex items-center gap-1.5 text-sm text-gray-600 transition-colors hover:text-primary-700"
        >
          <i class="pi pi-arrow-left text-xs" />
          Volver al inicio
        </RouterLink>
      </div>
    </div>

    <div class="mx-auto max-w-5xl px-4 py-8 sm:px-6 lg:px-8">
      <!-- Page header -->
      <header class="mb-8 border-b border-gray-200 pb-6">
        <h1
          data-testid="legal-page-title"
          class="text-3xl font-bold text-gray-900 sm:text-4xl"
        >
          {{ title }}
        </h1>
        <p
          data-testid="legal-last-updated"
          class="mt-2 text-sm text-gray-500"
        >
          Última actualización: {{ lastUpdated }}
        </p>
      </header>

      <!-- Content area: TOC sidebar + main content -->
      <div
        class="gap-10"
        :class="showToc && tocEntries.length > 0 ? 'lg:grid lg:grid-cols-[260px_1fr]' : ''"
      >
        <!-- TOC sidebar (desktop only) -->
        <aside
          v-if="showToc && tocEntries.length > 0"
          class="legal-toc mb-8 hidden lg:block"
        >
          <TableOfContents :entries="tocEntries" />
        </aside>

        <!-- Main document content -->
        <main class="legal-content min-w-0">
          <slot />
        </main>
      </div>

      <!-- Actions bar -->
      <div
        v-if="showPrintButton"
        class="legal-actions mt-10 flex items-center gap-4 border-t border-gray-200 pt-6"
      >
        <Button
          data-testid="print-button"
          label="Imprimir / Descargar PDF"
          icon="pi pi-print"
          outlined
          size="small"
          aria-label="Imprimir o descargar como PDF"
          @click="handlePrint"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
@media print {
  .legal-top-bar,
  .legal-actions,
  .legal-toc {
    display: none !important;
  }

  .legal-content {
    max-width: 100%;
    padding: 0;
  }

  a[href]::after {
    content: ' (' attr(href) ')';
    font-size: 0.8em;
    color: #555;
  }
}
</style>
