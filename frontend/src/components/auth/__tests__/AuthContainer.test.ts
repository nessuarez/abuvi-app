import { describe, it, expect, beforeEach, vi } from 'vitest'

// PrimeVue TabList requires ResizeObserver
class MockResizeObserver {
  observe = vi.fn()
  unobserve = vi.fn()
  disconnect = vi.fn()
}
global.ResizeObserver = MockResizeObserver as any
import { mount } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import { setActivePinia, createPinia } from 'pinia'
import PrimeVue from 'primevue/config'
import AuthContainer from '@/components/auth/AuthContainer.vue'

const router = createRouter({
  history: createMemoryHistory(),
  routes: [
    { path: '/', component: { template: '<div />' } },
    { path: '/legal/privacy', name: 'legal-privacy', component: { template: '<div />' } }
  ]
})

function mountContainer() {
  return mount(AuthContainer, {
    global: {
      plugins: [router, PrimeVue, createPinia()]
    }
  })
}

describe('AuthContainer', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('should render the ABUVI logo', async () => {
    const wrapper = mountContainer()
    await router.isReady()

    const logo = wrapper.find('img[alt="ABUVI"]')
    expect(logo.exists()).toBe(true)
  })

  it('should display welcome title', async () => {
    const wrapper = mountContainer()
    await router.isReady()

    expect(wrapper.text()).toContain('Te damos la bienvenida a ABUVI')
  })

  it('should display disclaimer text for members only', async () => {
    const wrapper = mountContainer()
    await router.isReady()

    expect(wrapper.text()).toContain('Plataforma exclusiva para socios/as')
  })

  it('should have login and register tabs', async () => {
    const wrapper = mountContainer()
    await router.isReady()

    expect(wrapper.text()).toContain('Iniciar Sesión')
    expect(wrapper.text()).toContain('Registrarse')
  })
})
