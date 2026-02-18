import { describe, it, expect, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import PrimeVue from 'primevue/config'
import UserRoleDialog from '@/components/users/UserRoleDialog.vue'
import type { User } from '@/types/user'
import { ref } from 'vue'

vi.mock('@/composables/useUsers', () => ({
  useUsers: () => ({
    updateUserRole: vi.fn(),
    loading: ref(false),
    error: ref(null),
    clearError: vi.fn()
  })
}))

const mockUser: User = {
  id: 'user-1',
  email: 'test@test.com',
  firstName: 'John',
  lastName: 'Doe',
  role: 'Member',
  isActive: true,
  phone: null,
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: '2024-01-01T00:00:00Z'
}

const mountComponent = (props: Record<string, unknown> = {}) => {
  return mount(UserRoleDialog, {
    props: {
      visible: true,
      user: mockUser,
      ...props
    },
    global: {
      plugins: [PrimeVue],
      stubs: {
        // Stub Dialog to render content inline for testability
        Dialog: {
          props: ['header', 'visible', 'modal', 'closable'],
          template:
            '<div v-if="visible" data-testid="role-dialog"><div class="p-dialog-header">{{ header }}</div><div class="p-dialog-content"><slot /></div></div>'
        }
      }
    }
  })
}

describe('UserRoleDialog', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('should display Spanish dialog header', () => {
    const wrapper = mountComponent()
    expect(wrapper.text()).toContain('Actualizar rol:')
  })

  it('should display "Rol actual" label for current role', () => {
    const wrapper = mountComponent()
    expect(wrapper.text()).toContain('Rol actual')
  })

  it('should display translated role label in current role badge', () => {
    const wrapper = mountComponent({ user: { ...mockUser, role: 'Member' } })
    expect(wrapper.text()).toContain('Socio')
  })

  it('should display "Nuevo rol *" label for new role dropdown', () => {
    const wrapper = mountComponent()
    expect(wrapper.text()).toContain('Nuevo rol')
  })

  it('should display "Motivo (opcional)" label for reason field', () => {
    const wrapper = mountComponent()
    expect(wrapper.text()).toContain('Motivo (opcional)')
  })

  it('should display "Cancelar" for Cancel button', () => {
    const wrapper = mountComponent()
    expect(wrapper.text()).toContain('Cancelar')
  })

  it('should display "Actualizar rol" for Update Role button', () => {
    const wrapper = mountComponent()
    expect(wrapper.find('[data-testid="submit-button"]').text()).toBe('Actualizar rol')
  })

  it('should display "caracteres" in character count text', () => {
    const wrapper = mountComponent()
    expect(wrapper.text()).toContain('caracteres')
  })
})
