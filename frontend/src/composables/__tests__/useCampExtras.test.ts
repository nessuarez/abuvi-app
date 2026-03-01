import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useCampExtras } from '@/composables/useCampExtras'
import { api } from '@/utils/api'
import type { CampEditionExtra } from '@/types/camp-edition'

vi.mock('@/utils/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
    patch: vi.fn()
  }
}))

const editionId = 'edition-123'

const makeExtra = (overrides: Partial<CampEditionExtra> = {}): CampEditionExtra => ({
  id: 'extra-1',
  campEditionId: editionId,
  name: 'Camp T-Shirt',
  description: 'Official t-shirt',
  price: 15,
  pricingType: 'PerPerson',
  pricingPeriod: 'OneTime',
  isRequired: false,
  maxQuantity: 100,
  currentQuantitySold: 0,
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  ...overrides
})

describe('useCampExtras', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('fetchExtras', () => {
    it('should call edition-scoped endpoint without params', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: [], error: null }
      })

      const { fetchExtras } = useCampExtras(editionId)
      await fetchExtras()

      expect(api.get).toHaveBeenCalledWith(
        `/camps/editions/${editionId}/extras`,
        { params: {} }
      )
    })

    it('should pass activeOnly query param when provided', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: [], error: null }
      })

      const { fetchExtras } = useCampExtras(editionId)
      await fetchExtras(true)

      expect(api.get).toHaveBeenCalledWith(
        `/camps/editions/${editionId}/extras`,
        { params: { activeOnly: true } }
      )
    })

    it('should populate extras array on success', async () => {
      const mockExtras = [makeExtra({ id: 'extra-1' }), makeExtra({ id: 'extra-2' })]
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: mockExtras, error: null }
      })

      const { extras, fetchExtras } = useCampExtras(editionId)
      await fetchExtras()

      expect(extras.value).toEqual(mockExtras)
    })

    it('should set error and empty array on failure', async () => {
      vi.mocked(api.get).mockRejectedValue(new Error('Network error'))

      const { extras, error, fetchExtras } = useCampExtras(editionId)
      await fetchExtras()

      expect(extras.value).toEqual([])
      expect(error.value).toBe('Error al cargar extras')
    })

    it('should set loading to false after completion', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: [], error: null }
      })

      const { loading, fetchExtras } = useCampExtras(editionId)
      await fetchExtras()

      expect(loading.value).toBe(false)
    })
  })

  describe('getExtraById', () => {
    it('should call global extras endpoint without editionId', async () => {
      const extra = makeExtra()
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: extra, error: null }
      })

      const { getExtraById } = useCampExtras(editionId)
      await getExtraById('extra-1')

      expect(api.get).toHaveBeenCalledWith('/camps/editions/extras/extra-1')
    })

    it('should return extra on success', async () => {
      const extra = makeExtra()
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: extra, error: null }
      })

      const { getExtraById } = useCampExtras(editionId)
      const result = await getExtraById('extra-1')

      expect(result).toEqual(extra)
    })

    it('should return null on failure', async () => {
      vi.mocked(api.get).mockRejectedValue(new Error('Not found'))

      const { getExtraById, error } = useCampExtras(editionId)
      const result = await getExtraById('extra-1')

      expect(result).toBeNull()
      expect(error.value).toBe('Error al cargar extra')
    })
  })

  describe('createExtra', () => {
    it('should add extra to local array on success', async () => {
      const newExtra = makeExtra({ id: 'extra-new' })
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: newExtra, error: null }
      })

      const { extras, createExtra } = useCampExtras(editionId)
      await createExtra({
        name: 'Camp T-Shirt',
        price: 15,
        pricingType: 'PerPerson',
        pricingPeriod: 'OneTime',
        isRequired: false
      })

      expect(extras.value).toContainEqual(newExtra)
    })

    it('should return created extra on success', async () => {
      const newExtra = makeExtra({ id: 'extra-new' })
      vi.mocked(api.post).mockResolvedValue({
        data: { success: true, data: newExtra, error: null }
      })

      const { createExtra } = useCampExtras(editionId)
      const result = await createExtra({
        name: 'Camp T-Shirt',
        price: 15,
        pricingType: 'PerPerson',
        pricingPeriod: 'OneTime',
        isRequired: false
      })

      expect(result).toEqual(newExtra)
      expect(api.post).toHaveBeenCalledWith(
        `/camps/editions/${editionId}/extras`,
        expect.objectContaining({ name: 'Camp T-Shirt' })
      )
    })

    it('should return null and set error on failure', async () => {
      vi.mocked(api.post).mockRejectedValue(new Error('Server error'))

      const { createExtra, error } = useCampExtras(editionId)
      const result = await createExtra({
        name: 'Camp T-Shirt',
        price: 15,
        pricingType: 'PerPerson',
        pricingPeriod: 'OneTime',
        isRequired: false
      })

      expect(result).toBeNull()
      expect(error.value).toBe('Error al crear extra')
    })
  })

  describe('updateExtra', () => {
    it('should call global extras endpoint without editionId', async () => {
      const updated = makeExtra({ id: 'extra-1', name: 'Updated' })
      vi.mocked(api.put).mockResolvedValue({
        data: { success: true, data: updated, error: null }
      })

      const { updateExtra } = useCampExtras(editionId)
      await updateExtra('extra-1', {
        name: 'Updated',
        price: 15,
        isRequired: false,
        isActive: true
      })

      expect(api.put).toHaveBeenCalledWith(
        '/camps/editions/extras/extra-1',
        expect.objectContaining({ name: 'Updated' })
      )
    })

    it('should update local array at correct index on success', async () => {
      const original = makeExtra({ id: 'extra-1', name: 'Original' })
      const updated = makeExtra({ id: 'extra-1', name: 'Updated' })
      vi.mocked(api.put).mockResolvedValue({
        data: { success: true, data: updated, error: null }
      })

      const { extras, updateExtra } = useCampExtras(editionId)
      extras.value = [original]

      await updateExtra('extra-1', {
        name: 'Updated',
        price: 15,
        isRequired: false,
        isActive: true
      })

      expect(extras.value[0].name).toBe('Updated')
    })

    it('should return null and set error on failure', async () => {
      vi.mocked(api.put).mockRejectedValue(new Error('Server error'))

      const { updateExtra, error } = useCampExtras(editionId)
      const result = await updateExtra('extra-1', {
        name: 'Updated',
        price: 15,
        isRequired: false,
        isActive: true
      })

      expect(result).toBeNull()
      expect(error.value).toBe('Error al actualizar extra')
    })
  })

  describe('deleteExtra', () => {
    it('should call global extras endpoint without editionId', async () => {
      vi.mocked(api.delete).mockResolvedValue({})

      const { extras, deleteExtra } = useCampExtras(editionId)
      extras.value = [makeExtra({ id: 'extra-1' })]

      await deleteExtra('extra-1')

      expect(api.delete).toHaveBeenCalledWith('/camps/editions/extras/extra-1')
    })

    it('should remove extra from local array on success', async () => {
      vi.mocked(api.delete).mockResolvedValue({})

      const { extras, deleteExtra } = useCampExtras(editionId)
      extras.value = [makeExtra({ id: 'extra-1' })]

      const result = await deleteExtra('extra-1')

      expect(result).toBe(true)
      expect(extras.value).toHaveLength(0)
    })

    it('should return false and set error on failure', async () => {
      vi.mocked(api.delete).mockRejectedValue(new Error('Server error'))

      const { extras, deleteExtra, error } = useCampExtras(editionId)
      extras.value = [makeExtra({ id: 'extra-1' })]

      const result = await deleteExtra('extra-1')

      expect(result).toBe(false)
      expect(extras.value).toHaveLength(1)
      expect(error.value).toBe('Error al eliminar extra')
    })
  })

  describe('activateExtra', () => {
    it('should call activate endpoint', async () => {
      const activated = makeExtra({ id: 'extra-1', isActive: true })
      vi.mocked(api.patch).mockResolvedValue({
        data: { success: true, data: activated, error: null }
      })

      const { activateExtra } = useCampExtras(editionId)
      await activateExtra('extra-1')

      expect(api.patch).toHaveBeenCalledWith('/camps/editions/extras/extra-1/activate')
    })

    it('should update extra in local array on success', async () => {
      const inactive = makeExtra({ id: 'extra-1', isActive: false })
      const activated = makeExtra({ id: 'extra-1', isActive: true })
      vi.mocked(api.patch).mockResolvedValue({
        data: { success: true, data: activated, error: null }
      })

      const { extras, activateExtra } = useCampExtras(editionId)
      extras.value = [inactive]

      const result = await activateExtra('extra-1')

      expect(result).toEqual(activated)
      expect(extras.value[0].isActive).toBe(true)
    })

    it('should return null and set error on failure', async () => {
      vi.mocked(api.patch).mockRejectedValue(new Error('Server error'))

      const { activateExtra, error } = useCampExtras(editionId)
      const result = await activateExtra('extra-1')

      expect(result).toBeNull()
      expect(error.value).toBe('Error al activar extra')
    })
  })

  describe('deactivateExtra', () => {
    it('should call deactivate endpoint', async () => {
      const deactivated = makeExtra({ id: 'extra-1', isActive: false })
      vi.mocked(api.patch).mockResolvedValue({
        data: { success: true, data: deactivated, error: null }
      })

      const { deactivateExtra } = useCampExtras(editionId)
      await deactivateExtra('extra-1')

      expect(api.patch).toHaveBeenCalledWith('/camps/editions/extras/extra-1/deactivate')
    })

    it('should update extra in local array on success', async () => {
      const active = makeExtra({ id: 'extra-1', isActive: true })
      const deactivated = makeExtra({ id: 'extra-1', isActive: false })
      vi.mocked(api.patch).mockResolvedValue({
        data: { success: true, data: deactivated, error: null }
      })

      const { extras, deactivateExtra } = useCampExtras(editionId)
      extras.value = [active]

      const result = await deactivateExtra('extra-1')

      expect(result).toEqual(deactivated)
      expect(extras.value[0].isActive).toBe(false)
    })

    it('should return null and set error on failure', async () => {
      vi.mocked(api.patch).mockRejectedValue(new Error('Server error'))

      const { deactivateExtra, error } = useCampExtras(editionId)
      const result = await deactivateExtra('extra-1')

      expect(result).toBeNull()
      expect(error.value).toBe('Error al desactivar extra')
    })
  })
})
