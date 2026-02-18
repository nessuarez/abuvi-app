import { describe, it, expect, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import PrimeVue from 'primevue/config'
import UserRoleCell from '@/components/users/UserRoleCell.vue'
import type { User } from '@/types/user'

const mockUser: User = {
  id: 'other-user-id',
  email: 'john@example.com',
  firstName: 'John',
  lastName: 'Doe',
  phone: null,
  role: 'Member',
  isActive: true,
  createdAt: '2026-02-08T10:00:00Z',
  updatedAt: '2026-02-08T10:00:00Z'
}

describe('UserRoleCell', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  const mountComponent = (user: User) => {
    return mount(UserRoleCell, {
      props: { user },
      global: {
        plugins: [PrimeVue]
      }
    })
  }

  it('should display "Socio" for Member role', () => {
    const wrapper = mountComponent({ ...mockUser, role: 'Member' })
    expect(wrapper.find('[data-testid="role-badge"]').text()).toBe('Socio')
  })

  it('should display "Junta Directiva" for Board role', () => {
    const wrapper = mountComponent({ ...mockUser, role: 'Board' })
    expect(wrapper.find('[data-testid="role-badge"]').text()).toBe('Junta Directiva')
  })

  it('should display "Administrador" for Admin role', () => {
    const wrapper = mountComponent({ ...mockUser, role: 'Admin' })
    expect(wrapper.find('[data-testid="role-badge"]').text()).toBe('Administrador')
  })
})
