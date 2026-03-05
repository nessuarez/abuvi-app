import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { ref } from 'vue'
import { registerTour, useOnboarding } from '@/composables/useOnboarding'
import type { OnboardingTour } from '@/types/onboarding'

const mockRoute = ref({ path: '/home' })

vi.mock('vue-router', () => ({
  useRoute: () => mockRoute.value,
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => ({
    isBoard: false,
  }),
}))

const mockDrive = vi.fn()
const mockDestroy = vi.fn()
let capturedConfig: any = null

vi.mock('driver.js', () => ({
  driver: (config: any) => {
    capturedConfig = config
    return {
      drive: mockDrive,
      destroy: mockDestroy,
      isActive: () => true,
    }
  },
}))

const STORAGE_KEY = 'abuvi:onboarding:completed'

const createTestTour = (overrides: Partial<OnboardingTour> = {}): OnboardingTour => ({
  id: 'test-tour',
  name: 'Test Tour',
  description: 'A test tour',
  routes: ['/home'],
  requiresBoard: false,
  steps: [
    {
      element: '[data-onboarding="test-element"]',
      title: 'Test Step',
      description: 'Test step description',
    },
  ],
  ...overrides,
})

describe('useOnboarding', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    localStorage.clear()
    capturedConfig = null
    mockRoute.value = { path: '/home' }

    // Add a DOM element that matches our test selector
    const el = document.createElement('div')
    el.setAttribute('data-onboarding', 'test-element')
    document.body.appendChild(el)

    // Register test tours
    registerTour(createTestTour())
    registerTour(
      createTestTour({
        id: 'board-tour',
        name: 'Board Tour',
        routes: ['/camps/locations'],
        requiresBoard: true,
      }),
    )
  })

  afterEach(() => {
    document.body.innerHTML = ''
  })

  describe('registerTour', () => {
    it('should add tour to registry', () => {
      const { getAvailableTours } = useOnboarding()
      const tours = getAvailableTours()
      expect(tours.some((t) => t.id === 'test-tour')).toBe(true)
    })
  })

  describe('startTour', () => {
    it('should call Driver.js drive() with correct steps', () => {
      const { startTour } = useOnboarding()
      startTour('test-tour')

      expect(mockDrive).toHaveBeenCalled()
      expect(capturedConfig.steps).toHaveLength(1)
      expect(capturedConfig.steps[0].popover.title).toBe('Test Step')
    })

    it('should do nothing for invalid tour ID', () => {
      const { startTour } = useOnboarding()
      const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {})

      startTour('non-existent')

      expect(mockDrive).not.toHaveBeenCalled()
      expect(warnSpy).toHaveBeenCalledWith(
        expect.stringContaining('non-existent'),
      )
      warnSpy.mockRestore()
    })

    it('should filter out steps with missing DOM elements', () => {
      registerTour(
        createTestTour({
          id: 'missing-elements-tour',
          steps: [
            {
              element: '[data-onboarding="missing"]',
              title: 'Missing',
              description: 'This element does not exist',
            },
          ],
        }),
      )

      const { startTour } = useOnboarding()
      const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {})

      startTour('missing-elements-tour')

      expect(mockDrive).not.toHaveBeenCalled()
      warnSpy.mockRestore()
    })
  })

  describe('hasCompletedTour', () => {
    it('should return false initially', () => {
      const { hasCompletedTour } = useOnboarding()
      expect(hasCompletedTour('test-tour')).toBe(false)
    })

    it('should return true after tour completion', () => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(['test-tour']))

      const { hasCompletedTour } = useOnboarding()
      expect(hasCompletedTour('test-tour')).toBe(true)
    })
  })

  describe('resetTour', () => {
    it('should remove tour from completed list', () => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(['test-tour', 'other-tour']))

      const { resetTour, hasCompletedTour } = useOnboarding()
      resetTour('test-tour')

      expect(hasCompletedTour('test-tour')).toBe(false)
      expect(hasCompletedTour('other-tour')).toBe(true)
    })
  })

  describe('resetAllTours', () => {
    it('should clear all completion data', () => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(['test-tour', 'other-tour']))

      const { resetAllTours } = useOnboarding()
      resetAllTours()

      expect(localStorage.getItem(STORAGE_KEY)).toBeNull()
    })
  })

  describe('getAvailableTours', () => {
    it('should filter by current route', () => {
      mockRoute.value = { path: '/home' }
      const { getAvailableTours } = useOnboarding()
      const tours = getAvailableTours()

      expect(tours.some((t) => t.id === 'test-tour')).toBe(true)
      expect(tours.some((t) => t.id === 'board-tour')).toBe(false)
    })

    it('should filter board-only tours for non-board users', () => {
      mockRoute.value = { path: '/camps/locations' }
      const { getAvailableTours } = useOnboarding()
      const tours = getAvailableTours()

      expect(tours.some((t) => t.id === 'board-tour')).toBe(false)
    })
  })

  describe('autoTrigger', () => {
    it('should start first uncompleted tour', () => {
      const { autoTrigger } = useOnboarding()
      autoTrigger()

      expect(mockDrive).toHaveBeenCalled()
    })

    it('should do nothing when all tours are completed', () => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(['test-tour']))

      const { autoTrigger } = useOnboarding()
      autoTrigger()

      expect(mockDrive).not.toHaveBeenCalled()
    })
  })

  describe('isActive', () => {
    it('should be false initially', () => {
      const { isActive } = useOnboarding()
      expect(isActive.value).toBe(false)
    })

    it('should be true after starting a tour', () => {
      const { startTour, isActive } = useOnboarding()
      startTour('test-tour')

      expect(isActive.value).toBe(true)
    })
  })

  describe('media HTML generation', () => {
    it('should append image HTML to description', () => {
      registerTour(
        createTestTour({
          id: 'media-image-tour',
          steps: [
            {
              element: '[data-onboarding="test-element"]',
              title: 'Image Step',
              description: 'Has image',
              media: { type: 'image', src: '/onboarding/test.png', alt: 'Test' },
            },
          ],
        }),
      )

      const { startTour } = useOnboarding()
      startTour('media-image-tour')

      const step = capturedConfig.steps[0]
      expect(step.popover.description).toContain('<img')
      expect(step.popover.description).toContain('/onboarding/test.png')
      expect(step.popover.description).toContain('alt="Test"')
    })

    it('should append video HTML to description', () => {
      registerTour(
        createTestTour({
          id: 'media-video-tour',
          steps: [
            {
              element: '[data-onboarding="test-element"]',
              title: 'Video Step',
              description: 'Has video',
              media: { type: 'video', src: '/onboarding/test.mp4' },
            },
          ],
        }),
      )

      const { startTour } = useOnboarding()
      startTour('media-video-tour')

      const step = capturedConfig.steps[0]
      expect(step.popover.description).toContain('<video')
      expect(step.popover.description).toContain('/onboarding/test.mp4')
    })

    it('should append audio HTML to description', () => {
      registerTour(
        createTestTour({
          id: 'media-audio-tour',
          steps: [
            {
              element: '[data-onboarding="test-element"]',
              title: 'Audio Step',
              description: 'Has audio',
              media: { type: 'audio', src: '/onboarding/test.mp3' },
            },
          ],
        }),
      )

      const { startTour } = useOnboarding()
      startTour('media-audio-tour')

      const step = capturedConfig.steps[0]
      expect(step.popover.description).toContain('<audio')
      expect(step.popover.description).toContain('/onboarding/test.mp3')
    })
  })
})
