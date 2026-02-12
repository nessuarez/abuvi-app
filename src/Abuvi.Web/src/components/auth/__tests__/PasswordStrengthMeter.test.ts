import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PasswordStrengthMeter from '../PasswordStrengthMeter.vue'
import ProgressBar from 'primevue/progressbar'

describe('PasswordStrengthMeter', () => {
  it('should show "Weak" for password with only lowercase letters', async () => {
    const wrapper = mount(PasswordStrengthMeter, {
      props: { password: 'abcdefgh' },
      global: {
        components: { ProgressBar }
      }
    })

    expect(wrapper.text()).toContain('Weak')
    expect(wrapper.text()).toContain('One uppercase letter')
    expect(wrapper.text()).toContain('One digit')
    expect(wrapper.text()).toContain('One special character')
  })

  it('should show "Good" for password meeting most criteria', async () => {
    const wrapper = mount(PasswordStrengthMeter, {
      props: { password: 'Password123' },
      global: {
        components: { ProgressBar }
      }
    })

    expect(wrapper.text()).toContain('Good')
    expect(wrapper.text()).toContain('One special character')
  })

  it('should show "Strong" for password meeting all criteria', async () => {
    const wrapper = mount(PasswordStrengthMeter, {
      props: { password: 'Password123!@#' },
      global: {
        components: { ProgressBar }
      }
    })

    expect(wrapper.text()).toContain('Strong')
    expect(wrapper.text()).not.toContain('Missing requirements')
  })

  it('should show missing criteria feedback', async () => {
    const wrapper = mount(PasswordStrengthMeter, {
      props: { password: 'short' },
      global: {
        components: { ProgressBar }
      }
    })

    expect(wrapper.text()).toContain('At least 8 characters')
    expect(wrapper.text()).toContain('One uppercase letter')
    expect(wrapper.text()).toContain('One digit')
    expect(wrapper.text()).toContain('One special character')
  })

  it('should update strength in real-time as password prop changes', async () => {
    const wrapper = mount(PasswordStrengthMeter, {
      props: { password: 'weak' },
      global: {
        components: { ProgressBar }
      }
    })

    expect(wrapper.text()).toContain('Weak')

    await wrapper.setProps({ password: 'Password123!@#' })

    expect(wrapper.text()).toContain('Strong')
  })

  it('should not render when password is empty', async () => {
    const wrapper = mount(PasswordStrengthMeter, {
      props: { password: '' },
      global: {
        components: { ProgressBar }
      }
    })

    expect(wrapper.text()).toBe('')
  })
})
