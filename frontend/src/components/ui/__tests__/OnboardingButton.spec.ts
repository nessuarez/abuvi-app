import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref } from 'vue'
import OnboardingButton from '@/components/ui/OnboardingButton.vue'
import type { OnboardingTour } from '@/types/onboarding'

const mockStartTour = vi.fn()
const mockResetAllTours = vi.fn()
const mockGetAvailableTours = vi.fn<() => OnboardingTour[]>()

vi.mock('@/composables/useOnboarding', () => ({
  useOnboarding: () => ({
    startTour: mockStartTour,
    resetAllTours: mockResetAllTours,
    getAvailableTours: mockGetAvailableTours,
    isActive: ref(false),
    hasCompletedTour: () => false,
    resetTour: vi.fn(),
    autoTrigger: vi.fn(),
  }),
}))

vi.mock('vue-router', () => ({
  useRoute: () => ref({ path: '/home' }),
}))

vi.mock('primevue/button', () => ({
  default: {
    name: 'Button',
    props: ['icon', 'rounded', 'severity', 'ariaLabel'],
    emits: ['click'],
    template: '<button @click="$emit(\'click\', $event)"><slot /></button>',
  },
}))

vi.mock('primevue/menu', () => ({
  default: {
    name: 'Menu',
    props: ['model', 'popup'],
    template: '<div class="mock-menu"><div v-for="(item, i) in model" :key="i"><button v-if="item.label" @click="item.command?.()">{{ item.label }}</button></div></div>',
    methods: { toggle() {} },
  },
}))

const testTours: OnboardingTour[] = [
  {
    id: 'welcome',
    name: 'Welcome Tour',
    description: 'Get to know the platform',
    routes: ['/home'],
    steps: [
      { element: '[data-onboarding="test"]', title: 'Step 1', description: 'Desc' },
    ],
  },
]

describe('OnboardingButton', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('should render when tours are available', () => {
    mockGetAvailableTours.mockReturnValue(testTours)
    const wrapper = mount(OnboardingButton)
    expect(wrapper.find('button').exists()).toBe(true)
  })

  it('should be hidden when no tours are available', () => {
    mockGetAvailableTours.mockReturnValue([])
    const wrapper = mount(OnboardingButton)
    expect(wrapper.find('button').exists()).toBe(false)
  })

  it('should display tour names in menu', () => {
    mockGetAvailableTours.mockReturnValue(testTours)
    const wrapper = mount(OnboardingButton)
    expect(wrapper.text()).toContain('Welcome Tour')
  })

  it('should call startTour when clicking a tour item', async () => {
    mockGetAvailableTours.mockReturnValue(testTours)
    const wrapper = mount(OnboardingButton)

    const tourButton = wrapper.findAll('button').find((b) => b.text() === 'Welcome Tour')
    await tourButton?.trigger('click')

    expect(mockStartTour).toHaveBeenCalledWith('welcome')
  })

  it('should call resetAllTours when clicking reset option', async () => {
    mockGetAvailableTours.mockReturnValue(testTours)
    const wrapper = mount(OnboardingButton)

    const resetButton = wrapper.findAll('button').find((b) => b.text() === 'Reset all tours')
    await resetButton?.trigger('click')

    expect(mockResetAllTours).toHaveBeenCalled()
  })
})
