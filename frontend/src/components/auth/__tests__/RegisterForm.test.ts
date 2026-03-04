import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import { setActivePinia, createPinia } from 'pinia'
import PrimeVue from 'primevue/config'
import RegisterForm from '@/components/auth/RegisterForm.vue'

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => ({
    register: vi.fn().mockResolvedValue({ success: false }),
    loading: false
  })
}))

const router = createRouter({
  history: createMemoryHistory(),
  routes: [
    { path: '/', component: { template: '<div />' } },
    { path: '/legal/privacy', name: 'legal-privacy', component: { template: '<div />' } }
  ]
})

function mountForm() {
  return mount(RegisterForm, {
    global: {
      plugins: [router, PrimeVue, createPinia()]
    }
  })
}

describe('RegisterForm', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  describe('terms and conditions link', () => {
    it('should render a link pointing to /legal/privacy', async () => {
      const wrapper = mountForm()
      await router.isReady()
      const link = wrapper.find('a[href*="legal/privacy"]')
      expect(link.exists()).toBe(true)
    })

    it('should open the terms link in a new tab', async () => {
      const wrapper = mountForm()
      await router.isReady()
      const link = wrapper.find('a[href*="legal/privacy"]')
      expect(link.attributes('target')).toBe('_blank')
    })

    it('should include rel="noopener noreferrer" on the terms link', async () => {
      const wrapper = mountForm()
      await router.isReady()
      const link = wrapper.find('a[href*="legal/privacy"]')
      expect(link.attributes('rel')).toBe('noopener noreferrer')
    })

    it('should display "términos y condiciones" as the link text', async () => {
      const wrapper = mountForm()
      await router.isReady()
      const link = wrapper.find('a[href*="legal/privacy"]')
      expect(link.text()).toContain('términos y condiciones')
    })
  })
})
