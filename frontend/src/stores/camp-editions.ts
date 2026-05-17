import { ref } from 'vue'
import { defineStore } from 'pinia'
import { useCampEditions } from '@/composables/useCampEditions'

export const useCampEditionsStore = defineStore('campEditions', () => {
  const { currentCampEdition, loading, error, fetchCurrentCampEdition: fetchFromComposable } =
    useCampEditions()

  const fetched = ref(false)

  const fetchCurrentCampEdition = async (): Promise<void> => {
    if (fetched.value) return
    fetched.value = true
    await fetchFromComposable()
  }

  return {
    currentCampEdition,
    loading,
    error,
    fetchCurrentCampEdition,
  }
})
