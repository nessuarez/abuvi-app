import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import UserCard from '@/components/users/UserCard.vue'
import type { User } from '@/types/user'

const mockUser: User = {
  id: '1',
  email: 'john@example.com',
  firstName: 'John',
  lastName: 'Doe',
  phone: '+34 123 456 789',
  role: 'Member',
  isActive: true,
  createdAt: '2026-02-08T10:00:00Z',
  updatedAt: '2026-02-08T10:00:00Z'
}

describe('UserCard', () => {
  const mountComponent = (props: any) => {
    return mount(UserCard, {
      props,
      global: {
        plugins: [PrimeVue]
      }
    })
  }

  it('should render user information', () => {
    const wrapper = mountComponent({ user: mockUser })

    expect(wrapper.text()).toContain('John Doe')
    expect(wrapper.text()).toContain('john@example.com')
    expect(wrapper.text()).toContain('+34 123 456 789')
    expect(wrapper.text()).toContain('Socio')
  })

  it('should display "Activo" when user is active', () => {
    const wrapper = mountComponent({ user: mockUser })
    expect(wrapper.text()).toContain('Activo')
  })

  it('should display "Inactivo" when user is inactive', () => {
    const wrapper = mountComponent({ user: { ...mockUser, isActive: false } })
    expect(wrapper.text()).toContain('Inactivo')
  })

  it('should emit select event when clicked', async () => {
    const wrapper = mountComponent({ user: mockUser })

    await wrapper.trigger('click')

    expect(wrapper.emitted('select')).toHaveLength(1)
    expect(wrapper.emitted('select')![0]).toEqual([mockUser])
  })

  it('should apply selected styles when selected prop is true', () => {
    const wrapper = mountComponent({ user: mockUser, selected: true })

    expect(wrapper.classes()).toContain('ring-2')
  })

  it('should render phone as dash when null', () => {
    const userWithoutPhone = { ...mockUser, phone: null }
    const wrapper = mountComponent({ user: userWithoutPhone })

    expect(wrapper.text()).not.toContain('+34')
  })
})
