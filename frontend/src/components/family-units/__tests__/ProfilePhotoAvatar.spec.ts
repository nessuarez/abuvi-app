import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import ProfilePhotoAvatar from '../ProfilePhotoAvatar.vue'

const globalConfig = {
  plugins: [[PrimeVue, { unstyled: true }]] as [unknown],
}

describe('ProfilePhotoAvatar', () => {
  it('renders initials when photoUrl is null', () => {
    const wrapper = mount(ProfilePhotoAvatar, {
      props: { photoUrl: null, initials: 'AB' },
      global: globalConfig,
    })
    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.text()).toContain('AB')
  })

  it('renders image when photoUrl is provided', () => {
    const wrapper = mount(ProfilePhotoAvatar, {
      props: { photoUrl: 'https://example.com/photo.jpg', initials: 'AB' },
      global: globalConfig,
    })
    const img = wrapper.find('img')
    expect(img.exists()).toBe(true)
    expect(img.attributes('src')).toBe('https://example.com/photo.jpg')
  })

  it('falls back to initials on image load error', async () => {
    const wrapper = mount(ProfilePhotoAvatar, {
      props: { photoUrl: 'https://example.com/broken.jpg', initials: 'AB' },
      global: globalConfig,
    })
    expect(wrapper.find('img').exists()).toBe(true)

    await wrapper.find('img').trigger('error')

    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.text()).toContain('AB')
  })

  it('applies correct size classes for sm', () => {
    const wrapper = mount(ProfilePhotoAvatar, {
      props: { photoUrl: null, initials: 'AB', size: 'sm' as const },
      global: globalConfig,
    })
    const avatar = wrapper.find('.rounded-full')
    expect(avatar.classes()).toContain('h-10')
    expect(avatar.classes()).toContain('w-10')
  })

  it('applies correct size classes for lg', () => {
    const wrapper = mount(ProfilePhotoAvatar, {
      props: { photoUrl: null, initials: 'AB', size: 'lg' as const },
      global: globalConfig,
    })
    const avatar = wrapper.find('.rounded-full')
    expect(avatar.classes()).toContain('h-20')
    expect(avatar.classes()).toContain('w-20')
  })

  it('shows edit overlay when editable', () => {
    const wrapper = mount(ProfilePhotoAvatar, {
      props: { photoUrl: null, initials: 'AB', editable: true },
      global: globalConfig,
    })
    expect(wrapper.find('[aria-label="Subir foto"]').exists()).toBe(true)
  })

  it('hides edit overlay when not editable', () => {
    const wrapper = mount(ProfilePhotoAvatar, {
      props: { photoUrl: null, initials: 'AB', editable: false },
      global: globalConfig,
    })
    expect(wrapper.find('[aria-label="Subir foto"]').exists()).toBe(false)
  })

  it('hides remove button when no existing photo', () => {
    const wrapper = mount(ProfilePhotoAvatar, {
      props: { photoUrl: null, initials: 'AB', editable: true },
      global: globalConfig,
    })
    expect(wrapper.find('[aria-label="Eliminar foto"]').exists()).toBe(false)
  })

  it('shows remove button when photo exists and editable', () => {
    const wrapper = mount(ProfilePhotoAvatar, {
      props: { photoUrl: 'https://example.com/photo.jpg', initials: 'AB', editable: true },
      global: globalConfig,
    })
    expect(wrapper.find('[aria-label="Eliminar foto"]').exists()).toBe(true)
  })

  it('emits remove event when trash button clicked', async () => {
    const wrapper = mount(ProfilePhotoAvatar, {
      props: { photoUrl: 'https://example.com/photo.jpg', initials: 'AB', editable: true },
      global: globalConfig,
    })
    await wrapper.find('[aria-label="Eliminar foto"]').trigger('click')
    expect(wrapper.emitted('remove')).toHaveLength(1)
  })

  it('shows loading spinner when loading prop is true', () => {
    const wrapper = mount(ProfilePhotoAvatar, {
      props: { photoUrl: null, initials: 'AB', loading: true },
      global: globalConfig,
    })
    expect(wrapper.find('.pi-spinner').exists()).toBe(true)
  })

  it('hides edit overlay when loading', () => {
    const wrapper = mount(ProfilePhotoAvatar, {
      props: { photoUrl: null, initials: 'AB', editable: true, loading: true },
      global: globalConfig,
    })
    expect(wrapper.find('[aria-label="Subir foto"]').exists()).toBe(false)
  })
})
