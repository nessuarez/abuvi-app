import { ref } from 'vue'
import type { GitHubRelease } from '@/types/changelog'

const GITHUB_RELEASES_URL = 'https://api.github.com/repos/nessuarez/abuvi-app/releases'

export function useChangelog() {
  const releases = ref<GitHubRelease[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)

  const fetchReleases = async (): Promise<void> => {
    loading.value = true
    error.value = null
    try {
      const response = await fetch(GITHUB_RELEASES_URL, {
        headers: { 'Accept': 'application/vnd.github+json' }
      })
      if (!response.ok) throw new Error(`GitHub API error: ${response.status}`)
      releases.value = await response.json()
    } catch (err: unknown) {
      error.value = 'No se pudieron cargar las novedades'
      console.error('Failed to fetch releases:', err)
    } finally {
      loading.value = false
    }
  }

  return { releases, loading, error, fetchReleases }
}
