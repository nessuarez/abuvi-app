import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createPinia, setActivePinia } from 'pinia'
import AppHeader from '@/components/layout/AppHeader.vue'

vi.mock('primevue/button', () => ({
  default: {
    name: 'Button',
    props: ['label', 'icon', 'text', 'rounded'],
    emits: ['click'],
    template: '<button @click="$emit(\'click\')">{{ label }}</button>'
  }
}))

vi.mock('@/components/layout/UserMenu.vue', () => ({
  default: {
    name: 'UserMenu',
    template: '<div data-testid="user-menu">UserMenu</div>'
  }
}))

vi.mock('@/components/ui/Container.vue', () => ({
  default: {
    name: 'Container',
    template: '<div><slot /></div>'
  }
}))

const router = createRouter({
  history: createMemoryHistory(),
  routes: [
    { path: '/', component: { template: '<div />' } },
    { path: '/home', component: { template: '<div />' } },
    { path: '/camp', component: { template: '<div />' } },
    { path: '/anniversary', component: { template: '<div />' } },
    { path: '/profile', component: { template: '<div />' } },
    { path: '/admin', component: { template: '<div />' } }
  ]
})

const mountComponent = () => {
  const pinia = createPinia()
  setActivePinia(pinia)

  return mount(AppHeader, {
    global: {
      plugins: [router, pinia]
    }
  })
}

describe('AppHeader', () => {
  beforeEach(async () => {
    await router.push('/home')
    await router.isReady()
  })

  describe('Logo rendering', () => {
    it('should render desktop logo with hidden lg:block classes', () => {
      const wrapper = mountComponent()
      const images = wrapper.findAll('img')
      const desktopLogo = images.find(img => img.classes().includes('hidden') && img.classes().includes('lg:block'))

      expect(desktopLogo).toBeTruthy()
      expect(desktopLogo!.attributes('alt')).toBe('ABUVI')
    })

    it('should render mobile logo with lg:hidden class', () => {
      const wrapper = mountComponent()
      const images = wrapper.findAll('img')
      const mobileLogo = images.find(img =>
        img.classes().includes('lg:hidden') && img.classes().includes('w-10')
      )

      expect(mobileLogo).toBeTruthy()
      expect(mobileLogo!.attributes('alt')).toBe('ABUVI Logo')
    })

    it('should render ABUVI text only on mobile', () => {
      const wrapper = mountComponent()
      const textSpan = wrapper.find('span.text-xl')

      expect(textSpan.exists()).toBe(true)
      expect(textSpan.text()).toBe('ABUVI')
      expect(textSpan.classes()).toContain('lg:hidden')
    })

    it('should wrap logo in a router-link to /home', () => {
      const wrapper = mountComponent()
      const logoLink = wrapper.find('a[href="/home"]')

      expect(logoLink.exists()).toBe(true)
    })
  })

  describe('Hamburger menu button', () => {
    it('should wrap hamburger button in a div with lg:hidden', () => {
      const wrapper = mountComponent()
      const hamburgerContainer = wrapper.find('div.lg\\:hidden')

      expect(hamburgerContainer.exists()).toBe(true)
      expect(hamburgerContainer.find('button').exists()).toBe(true)
    })

    it('should toggle mobile navigation when hamburger is clicked', async () => {
      const wrapper = mountComponent()
      const mobileNav = () => wrapper.find('nav.border-t')

      expect(mobileNav().exists()).toBe(false)

      const hamburgerButton = wrapper.find('div.lg\\:hidden button')
      await hamburgerButton.trigger('click')

      expect(mobileNav().exists()).toBe(true)
    })

    it('should close mobile menu when clicking again', async () => {
      const wrapper = mountComponent()
      const hamburgerButton = wrapper.find('div.lg\\:hidden button')

      await hamburgerButton.trigger('click')
      expect(wrapper.find('nav.border-t').exists()).toBe(true)

      await hamburgerButton.trigger('click')
      expect(wrapper.find('nav.border-t').exists()).toBe(false)
    })
  })

  describe('Navigation links', () => {
    it('should render all 4 navigation links on desktop', () => {
      const wrapper = mountComponent()
      const desktopNav = wrapper.find('nav.hidden')
      const links = desktopNav.findAll('a')

      expect(links).toHaveLength(4)
      expect(links[0].text()).toBe('Inicio')
      expect(links[1].text()).toBe('Campamento')
      expect(links[2].text()).toBe('Aniversario')
      expect(links[3].text()).toBe('Mi Perfil')
    })

    it('should close mobile menu when a navigation link is clicked', async () => {
      const wrapper = mountComponent()

      const hamburgerButton = wrapper.find('div.lg\\:hidden button')
      await hamburgerButton.trigger('click')

      const mobileNav = wrapper.find('nav.border-t')
      const mobileLinks = mobileNav.findAll('a')
      await mobileLinks[0].trigger('click')

      expect(wrapper.find('nav.border-t').exists()).toBe(false)
    })
  })
})
