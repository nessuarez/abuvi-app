import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import RegistrationDeleteDialog from '@/components/registrations/RegistrationDeleteDialog.vue'

const mountDialog = (props: { visible: boolean; loading: boolean }) =>
  mount(RegistrationDeleteDialog, {
    props,
    global: {
      plugins: [PrimeVue],
      stubs: {
        Dialog: {
          template: '<div v-if="visible"><slot /><slot name="footer" /></div>',
          props: ['visible', 'modal', 'closable', 'header']
        },
        Button: {
          template: '<button :data-testid="$attrs[\'data-testid\']" :disabled="disabled || loading" @click="$emit(\'click\')"><slot /></button>',
          props: ['label', 'severity', 'icon', 'loading', 'disabled'],
          emits: ['click']
        }
      }
    }
  })

describe('RegistrationDeleteDialog', () => {
  it('renders dialog content when visible is true', () => {
    const wrapper = mountDialog({ visible: true, loading: false })

    expect(wrapper.text()).toContain('Are you sure you want to delete this registration?')
    expect(wrapper.text()).toContain('You will be able to register again for this camp edition.')
  })

  it('does not render content when visible is false', () => {
    const wrapper = mountDialog({ visible: false, loading: false })

    expect(wrapper.text()).not.toContain('Are you sure you want to delete this registration?')
  })

  it('emits update:visible with false when Cancel clicked', async () => {
    const wrapper = mountDialog({ visible: true, loading: false })

    const closeBtn = wrapper.find('[data-testid="delete-dialog-close-btn"]')
    expect(closeBtn.exists()).toBe(true)
    await closeBtn.trigger('click')

    expect(wrapper.emitted('update:visible')).toBeTruthy()
    expect(wrapper.emitted('update:visible')![0]).toEqual([false])
  })

  it('emits confirm when Delete button clicked', async () => {
    const wrapper = mountDialog({ visible: true, loading: false })

    const confirmBtn = wrapper.find('[data-testid="delete-confirm-btn"]')
    expect(confirmBtn.exists()).toBe(true)
    await confirmBtn.trigger('click')

    expect(wrapper.emitted('confirm')).toBeTruthy()
  })

  it('disables Cancel button when loading', () => {
    const wrapper = mountDialog({ visible: true, loading: true })

    const cancelBtn = wrapper.find('[data-testid="delete-dialog-close-btn"]')
    expect(cancelBtn.attributes('disabled')).toBeDefined()
  })
})
