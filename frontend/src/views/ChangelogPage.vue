<script setup lang="ts">
import { onMounted } from 'vue'
import { marked } from 'marked'
import Card from 'primevue/card'
import Skeleton from 'primevue/skeleton'
import Message from 'primevue/message'
import Container from '@/components/ui/Container.vue'
import { useChangelog } from '@/composables/useChangelog'

const { releases, loading, error, fetchReleases } = useChangelog()

const formatDate = (iso: string) =>
  new Intl.DateTimeFormat('es-ES', {
    day: '2-digit', month: 'long', year: 'numeric'
  }).format(new Date(iso))

const renderMarkdown = (md: string): string => {
  return marked.parse(md) as string
}

onMounted(() => fetchReleases())
</script>

<template>
  <Container>
    <div class="py-8 space-y-6 max-w-4xl">
      <div>
        <h1 class="text-4xl font-bold text-gray-900">Novedades</h1>
        <p class="mt-1 text-sm text-gray-500">Historial de cambios y mejoras de la plataforma</p>
      </div>

      <!-- Loading -->
      <div v-if="loading" class="space-y-4">
        <Card v-for="i in 3" :key="i">
          <template #content>
            <Skeleton width="30%" height="1.5rem" class="mb-2" />
            <Skeleton width="20%" height="0.75rem" class="mb-4" />
            <Skeleton width="100%" height="4rem" />
          </template>
        </Card>
      </div>

      <!-- Error -->
      <Message v-else-if="error" severity="warn" :closable="false">
        {{ error }}
      </Message>

      <!-- Empty -->
      <div v-else-if="releases.length === 0" class="text-center py-12 text-gray-500">
        <i class="pi pi-info-circle text-3xl mb-2" />
        <p>No hay novedades disponibles todavía.</p>
      </div>

      <!-- Releases list -->
      <Card v-for="release in releases" :key="release.id" v-else>
        <template #title>
          <div class="flex items-center justify-between">
            <span class="text-xl font-semibold">{{ release.tag_name }}</span>
            <span class="text-sm text-gray-500">{{ formatDate(release.published_at) }}</span>
          </div>
        </template>
        <template #content>
          <div
            v-if="release.body"
            class="prose prose-sm prose-gray max-w-none"
            v-html="renderMarkdown(release.body)"
          />
          <p v-else class="text-sm text-gray-400 italic">Sin descripción de cambios.</p>

          <a
            :href="release.html_url"
            target="_blank"
            rel="noopener noreferrer"
            class="inline-flex items-center gap-1 mt-4 text-xs text-primary-500 hover:underline"
          >
            <i class="pi pi-external-link" />
            Ver en GitHub
          </a>
        </template>
      </Card>
    </div>
  </Container>
</template>
