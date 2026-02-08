import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import UserForm from '@/components/users/UserForm.vue'
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

describe('UserForm', () => {
  const mountComponent = (props: any) => {
    return mount(UserForm, {
      props,
      global: {
        plugins: [PrimeVue]
      }
    })
  }

  it('should render password field in create mode', () => {
    const wrapper = mountComponent({ mode: 'create' })

    expect(wrapper.find('#password').exists()).toBe(true)
  })

  it('should not render password field in edit mode', () => {
    const wrapper = mountComponent({ mode: 'edit', user: mockUser })

    expect(wrapper.find('#password').exists()).toBe(false)
  })

  it('should emit cancel event when cancel button clicked', async () => {
    const wrapper = mountComponent({ mode: 'create' })

    const cancelButton = wrapper.findAll('button').find((b) => b.text() === 'Cancel')
    await cancelButton?.trigger('click')

    expect(wrapper.emitted('cancel')).toHaveLength(1)
  })

  it('should disable submit button when form is invalid', () => {
    const wrapper = mountComponent({ mode: 'create' })

    const submitButton = wrapper.findAll('button').find((b) => b.text().includes('Create User'))
    expect(submitButton?.attributes('disabled')).toBeDefined()
  })
})
