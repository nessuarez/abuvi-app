import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import RegistrationForm from '../RegistrationForm.vue'
import InputText from 'primevue/inputtext'
import Password from 'primevue/password'
import Checkbox from 'primevue/checkbox'
import Button from 'primevue/button'
import ProgressBar from 'primevue/progressbar'

describe('RegistrationForm', () => {
  const globalComponents = {
    components: {
      InputText,
      Password,
      Checkbox,
      Button,
      ProgressBar
    }
  }

  it('should render all form fields', () => {
    const wrapper = mount(RegistrationForm, {
      global: globalComponents
    })

    expect(wrapper.find('#email').exists()).toBe(true)
    expect(wrapper.find('#password').exists()).toBe(true)
    expect(wrapper.find('#firstName').exists()).toBe(true)
    expect(wrapper.find('#lastName').exists()).toBe(true)
    expect(wrapper.find('#documentNumber').exists()).toBe(true)
    expect(wrapper.find('#phone').exists()).toBe(true)
    expect(wrapper.find('#acceptedTerms').exists()).toBe(true)
  })

  it('should validate required fields', async () => {
    const wrapper = mount(RegistrationForm, {
      global: globalComponents
    })

    // Submit empty form
    await wrapper.find('form').trigger('submit.prevent')

    // Check that validation errors are shown
    expect(wrapper.text()).toContain('Email is required')
    expect(wrapper.text()).toContain('Password is required')
    expect(wrapper.text()).toContain('First name is required')
    expect(wrapper.text()).toContain('Last name is required')
    expect(wrapper.text()).toContain('You must accept the terms')
  })

  it('should validate email format', async () => {
    const wrapper = mount(RegistrationForm, {
      global: globalComponents
    })

    const emailInput = wrapper.find('#email')
    await emailInput.setValue('invalid-email')
    await wrapper.find('form').trigger('submit.prevent')

    expect(wrapper.text()).toContain('Please enter a valid email address')
  })

  it('should validate password strength', async () => {
    const wrapper = mount(RegistrationForm, {
      global: globalComponents
    })

    const passwordInput = wrapper.find('#password input')
    await passwordInput.setValue('weak')
    await wrapper.find('form').trigger('submit.prevent')

    expect(wrapper.text()).toContain('Password must meet all strength requirements')
  })

  it('should validate document number format (uppercase alphanumeric)', async () => {
    const wrapper = mount(RegistrationForm, {
      global: globalComponents
    })

    const docInput = wrapper.find('#documentNumber')
    await docInput.setValue('abc123-invalid')
    await wrapper.find('form').trigger('submit.prevent')

    expect(wrapper.text()).toContain(
      'Document number must contain only uppercase letters and numbers'
    )
  })

  it('should validate phone format (E.164)', async () => {
    const wrapper = mount(RegistrationForm, {
      global: globalComponents
    })

    const phoneInput = wrapper.find('#phone')
    await phoneInput.setValue('invalid-phone')
    await wrapper.find('form').trigger('submit.prevent')

    expect(wrapper.text()).toContain('Please enter a valid phone number')
  })

  it('should require terms acceptance', async () => {
    const wrapper = mount(RegistrationForm, {
      global: globalComponents
    })

    await wrapper.find('form').trigger('submit.prevent')

    expect(wrapper.text()).toContain('You must accept the terms')
  })

  it('should emit submit event with form data when valid', async () => {
    const wrapper = mount(RegistrationForm, {
      global: globalComponents
    })

    // Fill form with valid data
    await wrapper.find('#email').setValue('test@example.com')
    await wrapper.find('#password input').setValue('Test123!@#')
    await wrapper.find('#firstName').setValue('John')
    await wrapper.find('#lastName').setValue('Doe')

    const checkbox = wrapper.findComponent({ name: 'Checkbox' })
    await checkbox.setValue(true)

    await wrapper.find('form').trigger('submit.prevent')

    expect(wrapper.emitted('submit')).toBeTruthy()
    expect(wrapper.emitted('submit')![0][0]).toMatchObject({
      email: 'test@example.com',
      password: 'Test123!@#',
      firstName: 'John',
      lastName: 'Doe',
      acceptedTerms: true
    })
  })

  it('should not submit when form is invalid', async () => {
    const wrapper = mount(RegistrationForm, {
      global: globalComponents
    })

    // Fill form with invalid data (weak password)
    await wrapper.find('#email').setValue('test@example.com')
    await wrapper.find('#password input').setValue('weak')
    await wrapper.find('#firstName').setValue('John')
    await wrapper.find('#lastName').setValue('Doe')

    await wrapper.find('form').trigger('submit.prevent')

    expect(wrapper.emitted('submit')).toBeFalsy()
  })

  it('should emit cancel event when cancel button clicked', async () => {
    const wrapper = mount(RegistrationForm, {
      global: globalComponents
    })

    const buttons = wrapper.findAllComponents(Button)
    const cancelButton = buttons.find((btn) => btn.text() === 'Cancel')

    await cancelButton!.trigger('click')

    expect(wrapper.emitted('cancel')).toBeTruthy()
  })

  it('should disable submit button when loading', () => {
    const wrapper = mount(RegistrationForm, {
      props: { loading: true },
      global: globalComponents
    })

    const buttons = wrapper.findAllComponents(Button)
    const submitButton = buttons.find((btn) => btn.text() === 'Register')

    expect(submitButton!.props('disabled')).toBe(true)
  })
})
