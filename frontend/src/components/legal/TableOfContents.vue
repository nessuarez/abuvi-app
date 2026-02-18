<script setup lang="ts">
// TableOfContents: static prop-driven anchor navigation for legal pages
interface TocEntry {
  id: string
  label: string
  level: 1 | 2
}

interface Props {
  entries: TocEntry[]
}

defineProps<Props>()
</script>

<template>
  <nav
    aria-label="Tabla de contenidos"
    data-testid="legal-toc"
    class="lg:sticky lg:top-8 lg:self-start"
  >
    <p class="mb-3 text-xs font-semibold uppercase tracking-wider text-gray-500">
      Contenido
    </p>
    <ol class="space-y-1">
      <li v-for="entry in entries" :key="entry.id" :class="{ 'pl-4': entry.level === 2 }">
        <a
          :href="'#' + entry.id"
          class="block rounded px-2 py-1 text-sm text-gray-600 transition-colors hover:bg-gray-100 hover:text-primary-700"
        >
          {{ entry.label }}
        </a>
      </li>
    </ol>
  </nav>
</template>
