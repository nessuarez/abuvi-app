import { describe, it, expect, vi, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { ref } from 'vue'

const mockFetchCurrentCampEdition = vi.fn().mockResolvedValue(undefined)
const mockCurrentCampEdition = ref<{ id: string } | null>(null)

vi.mock('@/composables/useCampEditions', () => ({
  useCampEditions: () => ({
    currentCampEdition: mockCurrentCampEdition,
    loading: ref(false),
    error: ref(null),
    fetchCurrentCampEdition: mockFetchCurrentCampEdition,
  }),
}))

describe('useCampEditionsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    mockCurrentCampEdition.value = null
    mockFetchCurrentCampEdition.mockClear()
  })

  it('should call fetchCurrentCampEdition on first call', async () => {
    const { useCampEditionsStore } = await import('@/stores/camp-editions')
    const store = useCampEditionsStore()

    await store.fetchCurrentCampEdition()

    expect(mockFetchCurrentCampEdition).toHaveBeenCalledTimes(1)
  })

  it('should not call fetchCurrentCampEdition again on repeated calls', async () => {
    const { useCampEditionsStore } = await import('@/stores/camp-editions')
    const store = useCampEditionsStore()

    await store.fetchCurrentCampEdition()
    await store.fetchCurrentCampEdition()
    await store.fetchCurrentCampEdition()

    expect(mockFetchCurrentCampEdition).toHaveBeenCalledTimes(1)
  })

  it('should expose currentCampEdition from the composable', async () => {
    const { useCampEditionsStore } = await import('@/stores/camp-editions')
    const store = useCampEditionsStore()

    expect(store.currentCampEdition).toBeNull()

    mockCurrentCampEdition.value = { id: 'edition-123' }

    expect(store.currentCampEdition?.id).toBe('edition-123')
  })
})
