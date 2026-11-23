import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useMediaThemes } from '@/composables/useMediaThemes'
import { api } from '@/utils/api'
import type { MediaTheme } from '@/types/media-theme'

vi.mock('@/utils/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}))

const makeTheme = (overrides: Partial<MediaTheme> = {}): MediaTheme => ({
  id: 'theme-1',
  name: 'San Abuvino',
  slug: 'san-abuvino',
  description: 'Fiesta de San Abuvino',
  isActive: true,
  itemCount: 12,
  firstYear: 1998,
  lastYear: 2023,
  undatedCount: 2,
  ...overrides,
})

describe('useMediaThemes', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('fetchCatalogue', () => {
    it('calls GET /media-themes without query params by default', async () => {
      vi.mocked(api.get).mockResolvedValue({ data: { success: true, data: [], error: null } })

      const { fetchCatalogue } = useMediaThemes()
      await fetchCatalogue()

      expect(api.get).toHaveBeenCalledWith('/media-themes')
    })

    it('includes inactive themes when asked', async () => {
      vi.mocked(api.get).mockResolvedValue({ data: { success: true, data: [], error: null } })

      const { fetchCatalogue } = useMediaThemes()
      await fetchCatalogue(true)

      expect(api.get).toHaveBeenCalledWith('/media-themes?includeInactive=true')
    })

    it('populates themes on success', async () => {
      const themes = [makeTheme(), makeTheme({ id: 'theme-2', name: 'Excursiones' })]
      vi.mocked(api.get).mockResolvedValue({ data: { success: true, data: themes, error: null } })

      const { fetchCatalogue, themes: themesRef } = useMediaThemes()
      await fetchCatalogue()

      expect(themesRef.value).toEqual(themes)
    })

    it('sets a Spanish error message and leaves themes empty on failure', async () => {
      vi.mocked(api.get).mockRejectedValue(new Error('Network error'))

      const { fetchCatalogue, themes, error } = useMediaThemes()
      await fetchCatalogue()

      expect(themes.value).toEqual([])
      expect(error.value).toBe('No se pudieron cargar los temas')
    })
  })

  describe('fetchThemeItems', () => {
    it('builds the query string from the given filters', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: { theme: makeTheme(), items: [], total: 0 }, error: null },
      })

      const { fetchThemeItems } = useMediaThemes()
      await fetchThemeItems('san-abuvino', { page: 2, pageSize: 20, undatedOnly: true })

      expect(api.get).toHaveBeenCalledWith(
        '/media-themes/san-abuvino/items?page=2&pageSize=20&undatedOnly=true',
      )
    })

    it('omits the query string entirely when no filters are given', async () => {
      vi.mocked(api.get).mockResolvedValue({
        data: { success: true, data: { theme: makeTheme(), items: [], total: 0 }, error: null },
      })

      const { fetchThemeItems } = useMediaThemes()
      await fetchThemeItems('san-abuvino')

      expect(api.get).toHaveBeenCalledWith('/media-themes/san-abuvino/items')
    })
  })

  describe('createTheme', () => {
    it('posts the request and appends the new theme to the catalogue', async () => {
      const created = makeTheme({ id: 'theme-new', name: 'Talleres', slug: 'talleres' })
      vi.mocked(api.post).mockResolvedValue({ data: { success: true, data: created, error: null } })

      const { createTheme, themes } = useMediaThemes()
      const result = await createTheme({ name: 'Talleres' })

      expect(api.post).toHaveBeenCalledWith('/media-themes', { name: 'Talleres' })
      expect(result).toEqual(created)
      expect(themes.value).toContainEqual(created)
    })

    it('returns null and sets an error when the API rejects the request', async () => {
      vi.mocked(api.post).mockRejectedValue(new Error('Server error'))

      const { createTheme, error } = useMediaThemes()
      const result = await createTheme({ name: 'Duplicado' })

      expect(result).toBeNull()
      expect(error.value).toBe('No se pudo guardar el tema')
    })
  })

  describe('updateTheme', () => {
    it('replaces the matching entry in the catalogue on success', async () => {
      const original = makeTheme()
      const updated = makeTheme({ name: 'San Abuvino (renombrado)' })
      vi.mocked(api.put).mockResolvedValue({ data: { success: true, data: updated, error: null } })

      const { updateTheme, themes } = useMediaThemes()
      themes.value = [original]
      const ok = await updateTheme(original.id, {
        name: updated.name,
        isActive: true,
      })

      expect(ok).toBe(true)
      expect(themes.value[0]).toEqual(updated)
    })

    it('returns false and sets an error on failure', async () => {
      vi.mocked(api.put).mockRejectedValue(new Error('Server error'))

      const { updateTheme, error } = useMediaThemes()
      const ok = await updateTheme('theme-1', { name: 'X', isActive: true })

      expect(ok).toBe(false)
      expect(error.value).toBe('No se pudo guardar el tema')
    })
  })

  describe('deleteTheme', () => {
    it('removes the theme from the catalogue on success', async () => {
      vi.mocked(api.delete).mockResolvedValue({ data: { success: true } })

      const { deleteTheme, themes } = useMediaThemes()
      themes.value = [makeTheme(), makeTheme({ id: 'theme-2' })]
      const ok = await deleteTheme('theme-1')

      expect(ok).toBe(true)
      expect(api.delete).toHaveBeenCalledWith('/media-themes/theme-1')
      expect(themes.value.map((t) => t.id)).toEqual(['theme-2'])
    })

    it('passes force=true when forcing a delete with tagged items', async () => {
      vi.mocked(api.delete).mockResolvedValue({ data: { success: true } })

      const { deleteTheme } = useMediaThemes()
      await deleteTheme('theme-1', true)

      expect(api.delete).toHaveBeenCalledWith('/media-themes/theme-1?force=true')
    })

    it('surfaces the conflict message and keeps the theme when items are still tagged', async () => {
      vi.mocked(api.delete).mockRejectedValue({
        response: { status: 409, data: { error: { message: 'El tema tiene recuerdos asociados' } } },
      })

      const { deleteTheme, themes, error } = useMediaThemes()
      themes.value = [makeTheme()]
      const ok = await deleteTheme('theme-1')

      expect(ok).toBe(false)
      expect(error.value).toBe('El tema tiene recuerdos asociados')
      expect(themes.value).toHaveLength(1)
    })
  })

  describe('attachTheme', () => {
    it('posts the theme id to the media item and returns true on success', async () => {
      vi.mocked(api.post).mockResolvedValue({ data: { success: true } })

      const { attachTheme } = useMediaThemes()
      const ok = await attachTheme('item-1', 'theme-1')

      expect(ok).toBe(true)
      expect(api.post).toHaveBeenCalledWith('/media-items/item-1/themes', { themeId: 'theme-1' })
    })

    it('returns false and sets a Spanish error when the API rejects it', async () => {
      vi.mocked(api.post).mockRejectedValue(new Error('Network error'))

      const { attachTheme, error } = useMediaThemes()
      const ok = await attachTheme('item-1', 'theme-1')

      expect(ok).toBe(false)
      expect(error.value).toBe('No se pudo etiquetar el recuerdo')
    })

    it('surfaces the API-provided message instead of the generic one when present', async () => {
      vi.mocked(api.post).mockRejectedValue({
        response: { data: { error: { message: 'Este tema ya está retirado' } } },
      })

      const { attachTheme, error } = useMediaThemes()
      await attachTheme('item-1', 'theme-1')

      expect(error.value).toBe('Este tema ya está retirado')
    })
  })

  describe('detachTheme', () => {
    it('sends a DELETE for the item/theme pair and returns true on success', async () => {
      vi.mocked(api.delete).mockResolvedValue({ data: { success: true } })

      const { detachTheme } = useMediaThemes()
      const ok = await detachTheme('item-1', 'theme-1')

      expect(ok).toBe(true)
      expect(api.delete).toHaveBeenCalledWith('/media-items/item-1/themes/theme-1')
    })

    it('returns false and sets a Spanish error when the API refuses it', async () => {
      vi.mocked(api.delete).mockRejectedValue(new Error('Forbidden'))

      const { detachTheme, error } = useMediaThemes()
      const ok = await detachTheme('item-1', 'theme-1')

      expect(ok).toBe(false)
      expect(error.value).toBe('No se pudo etiquetar el recuerdo')
    })
  })
})
