import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import { setActivePinia, createPinia } from 'pinia'
import PrimeVue from 'primevue/config'
import RegisterForm from '@/components/auth/RegisterForm.vue'

const mockRegister = vi.fn().mockResolvedValue({ success: false })

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => ({
    register: mockRegister,
    loading: false
  })
}))

vi.mock('@/utils/api', () => ({
  api: {
    post: vi.fn().mockResolvedValue({ data: { success: true } })
  }
}))

const router = createRouter({
  history: createMemoryHistory(),
  routes: [
    { path: '/', component: { template: '<div />' } },
    { path: '/home', component: { template: '<div />' } },
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

async function fillAndSubmitForm(wrapper: ReturnType<typeof mount>) {
  await wrapper.find('#firstName').setValue('John')
  await wrapper.find('#lastName').setValue('Doe')
  await wrapper.find('#registerEmail').setValue('john@example.com')

  const passwordInputs = wrapper.findAll('input[type="password"]')
  await passwordInputs[0].setValue('Password1!')
  await passwordInputs[1].setValue('Password1!')

  const checkbox = wrapper.findComponent({ name: 'Checkbox' })
  await checkbox.vm.$emit('update:modelValue', true)

  await wrapper.find('form').trigger('submit')
  await vi.dynamicImportSettled()
}

describe('RegisterForm', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    mockRegister.mockReset()
    vi.clearAllMocks()
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

    it('should have font-semibold and underline classes on the terms link', async () => {
      const wrapper = mountForm()
      await router.isReady()
      const link = wrapper.find('a[href*="legal/privacy"]')
      expect(link.classes()).toContain('font-semibold')
      expect(link.classes()).toContain('underline')
    })
  })

  describe('password validation', () => {
    it('should require minimum 8 characters', async () => {
      const wrapper = mountForm()
      await router.isReady()

      await wrapper.find('#firstName').setValue('John')
      await wrapper.find('#lastName').setValue('Doe')
      await wrapper.find('#registerEmail').setValue('john@example.com')

      const passwordInputs = wrapper.findAll('input[type="password"]')
      await passwordInputs[0].setValue('Short1!')
      await passwordInputs[1].setValue('Short1!')

      const checkbox = wrapper.findComponent({ name: 'Checkbox' })
      await checkbox.vm.$emit('update:modelValue', true)

      await wrapper.find('form').trigger('submit')

      expect(wrapper.text()).toContain('al menos 8 caracteres')
    })

    it('should require password complexity', async () => {
      const wrapper = mountForm()
      await router.isReady()

      await wrapper.find('#firstName').setValue('John')
      await wrapper.find('#lastName').setValue('Doe')
      await wrapper.find('#registerEmail').setValue('john@example.com')

      const passwordInputs = wrapper.findAll('input[type="password"]')
      await passwordInputs[0].setValue('simplepassword')
      await passwordInputs[1].setValue('simplepassword')

      const checkbox = wrapper.findComponent({ name: 'Checkbox' })
      await checkbox.vm.$emit('update:modelValue', true)

      await wrapper.find('form').trigger('submit')

      expect(wrapper.text()).toContain('mayúscula, minúscula, número y carácter especial')
    })
  })

  describe('successful registration', () => {
    it('should show success message after successful registration', async () => {
      mockRegister.mockResolvedValue({ success: true, email: 'john@example.com' })
      const wrapper = mountForm()
      await router.isReady()

      await fillAndSubmitForm(wrapper)
      await vi.dynamicImportSettled()

      expect(wrapper.text()).toContain('¡Registro completo!')
      expect(wrapper.text()).toContain('email de verificación')
    })

    it('should display registered email in success message', async () => {
      mockRegister.mockResolvedValue({ success: true, email: 'john@example.com' })
      const wrapper = mountForm()
      await router.isReady()

      await fillAndSubmitForm(wrapper)
      await vi.dynamicImportSettled()

      expect(wrapper.text()).toContain('john@example.com')
    })

    it('should not show form when registration is complete', async () => {
      mockRegister.mockResolvedValue({ success: true, email: 'john@example.com' })
      const wrapper = mountForm()
      await router.isReady()

      await fillAndSubmitForm(wrapper)
      await vi.dynamicImportSettled()

      expect(wrapper.find('form').exists()).toBe(false)
    })

    it('should not redirect to /home after registration', async () => {
      mockRegister.mockResolvedValue({ success: true, email: 'john@example.com' })
      const pushSpy = vi.spyOn(router, 'push')
      const wrapper = mountForm()
      await router.isReady()

      await fillAndSubmitForm(wrapper)
      await vi.dynamicImportSettled()

      expect(pushSpy).not.toHaveBeenCalledWith('/home')
    })

    it('should show go-to-login button in success state', async () => {
      mockRegister.mockResolvedValue({ success: true, email: 'john@example.com' })
      const wrapper = mountForm()
      await router.isReady()

      await fillAndSubmitForm(wrapper)
      await vi.dynamicImportSettled()

      expect(wrapper.text()).toContain('Ir al inicio de sesión')
    })

    it('should show resend email link in success state', async () => {
      mockRegister.mockResolvedValue({ success: true, email: 'john@example.com' })
      const wrapper = mountForm()
      await router.isReady()

      await fillAndSubmitForm(wrapper)
      await vi.dynamicImportSettled()

      expect(wrapper.text()).toContain('¿No recibiste el email? Reenviar')
    })

    it('should emit go-to-login when clicking login button', async () => {
      mockRegister.mockResolvedValue({ success: true, email: 'john@example.com' })
      const wrapper = mountForm()
      await router.isReady()

      await fillAndSubmitForm(wrapper)
      await vi.dynamicImportSettled()

      await wrapper.findComponent({ name: 'Button' }).trigger('click')
      expect(wrapper.emitted('go-to-login')).toBeTruthy()
    })
  })

  describe('error handling', () => {
    it('should show error message for duplicate email', async () => {
      mockRegister.mockResolvedValue({
        success: false,
        error: 'Este correo electrónico ya está registrado.'
      })
      const wrapper = mountForm()
      await router.isReady()

      await fillAndSubmitForm(wrapper)
      await vi.dynamicImportSettled()

      expect(wrapper.text()).toContain('Este correo electrónico ya está registrado.')
    })

    it('should keep showing the form on error', async () => {
      mockRegister.mockResolvedValue({ success: false, error: 'Error' })
      const wrapper = mountForm()
      await router.isReady()

      await fillAndSubmitForm(wrapper)
      await vi.dynamicImportSettled()

      expect(wrapper.find('form').exists()).toBe(true)
    })
  })
})
