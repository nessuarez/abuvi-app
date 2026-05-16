import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createPinia, setActivePinia } from 'pinia'
import { ref } from 'vue'
import AdminSidebar from '@/components/admin/AdminSidebar.vue'
import { useAuthStore } from '@/stores/auth'

const mockFetchCurrentCampEdition = vi.fn().mockResolvedValue(undefined)
const mockCurrentCampEdition = ref<{ id: string } | null>(null)

vi.mock('@/stores/camp-editions', () => ({
  useCampEditionsStore: () => ({
    get currentCampEdition() { return mockCurrentCampEdition.value },
    fetchCurrentCampEdition: mockFetchCurrentCampEdition,
  }),
}))

const router = createRouter({
  history: createMemoryHistory(),
  routes: [
    { path: '/admin/camps', name: 'admin-camps', component: { template: '<div />' } },
    { path: '/admin/registrations', name: 'admin-registrations', component: { template: '<div />' } },
    { path: '/admin/users', name: 'admin-users', component: { template: '<div />' } },
    { path: '/admin/family-units', name: 'admin-family-units', component: { template: '<div />' } },
    { path: '/admin/media-review', name: 'admin-media-review', component: { template: '<div />' } },
    { path: '/admin/payments', name: 'admin-payments', component: { template: '<div />' } },
    { path: '/admin/storage', name: 'admin-storage', component: { template: '<div />' } },
    { path: '/admin/settings', name: 'admin-settings', component: { template: '<div />' } },
    { path: '/camps/editions/:id', name: 'camp-edition-detail', component: { template: '<div />' } },
    { path: '/camps/editions/:id/assignment', name: 'accommodation-assignment', component: { template: '<div />' } },
  ],
})

const mountComponent = (boardUser = true) => {
  const pinia = createPinia()
  setActivePinia(pinia)

  if (boardUser) {
    const auth = useAuthStore()
    auth.user = { role: 'Board', firstName: 'Test', lastName: 'User', email: 'board@test.com', id: '1' }
    auth.token = 'mock-token'
  }

  return mount(AdminSidebar, {
    global: { plugins: [router, pinia] },
  })
}

describe('AdminSidebar', () => {
  beforeEach(async () => {
    mockCurrentCampEdition.value = null
    mockFetchCurrentCampEdition.mockClear()
    await router.push('/admin/camps')
    await router.isReady()
  })

  describe('Gestión section — static items', () => {
    it('should always render Campamentos and Inscripciones', () => {
      const wrapper = mountComponent()

      expect(wrapper.find('[data-testid="sidebar-camps"]').exists()).toBe(true)
      expect(wrapper.find('[data-testid="sidebar-registrations"]').exists()).toBe(true)
    })
  })

  describe('Gestión section — dynamic items (Campamento Actual / Asignación)', () => {
    it('should not render Campamento Actual when no current edition', () => {
      mockCurrentCampEdition.value = null
      const wrapper = mountComponent()

      expect(wrapper.find('[data-testid="sidebar-current-edition"]').exists()).toBe(false)
    })

    it('should not render Asignación de Habitaciones when no current edition', () => {
      mockCurrentCampEdition.value = null
      const wrapper = mountComponent()

      expect(wrapper.find('[data-testid="sidebar-room-assignment"]').exists()).toBe(false)
    })

    it('should render Campamento Actual when a current edition exists', async () => {
      mockCurrentCampEdition.value = { id: 'edition-xyz' }
      const wrapper = mountComponent()
      await wrapper.vm.$nextTick()

      expect(wrapper.find('[data-testid="sidebar-current-edition"]').exists()).toBe(true)
    })

    it('should render Asignación de Habitaciones for Board users with a current edition', async () => {
      mockCurrentCampEdition.value = { id: 'edition-xyz' }
      const wrapper = mountComponent(true)
      await wrapper.vm.$nextTick()

      expect(wrapper.find('[data-testid="sidebar-room-assignment"]').exists()).toBe(true)
    })

    it('should link Campamento Actual to the edition detail route', async () => {
      mockCurrentCampEdition.value = { id: 'edition-xyz' }
      const wrapper = mountComponent()
      await wrapper.vm.$nextTick()

      const link = wrapper.find('[data-testid="sidebar-current-edition"]')
      expect(link.attributes('href')).toBe('/camps/editions/edition-xyz')
    })

    it('should link Asignación de Habitaciones to the assignment route', async () => {
      mockCurrentCampEdition.value = { id: 'edition-xyz' }
      const wrapper = mountComponent(true)
      await wrapper.vm.$nextTick()

      const link = wrapper.find('[data-testid="sidebar-room-assignment"]')
      expect(link.attributes('href')).toBe('/camps/editions/edition-xyz/assignment')
    })
  })

  describe('Personas section', () => {
    it('should render Usuarios under Personas', () => {
      const wrapper = mountComponent()

      expect(wrapper.find('[data-testid="sidebar-users"]').exists()).toBe(true)
    })

    it('should render Unidades Familiares under Personas (not Gestión)', () => {
      const wrapper = mountComponent()

      const familyUnitsLink = wrapper.find('[data-testid="sidebar-family-units"]')
      expect(familyUnitsLink.exists()).toBe(true)

      // Confirm it comes after Usuarios in the DOM (same group)
      const allLinks = wrapper.findAll('a')
      const usersIndex = allLinks.findIndex(l => l.attributes('data-testid') === 'sidebar-users')
      const familyIndex = allLinks.findIndex(l => l.attributes('data-testid') === 'sidebar-family-units')

      expect(usersIndex).toBeGreaterThan(-1)
      expect(familyIndex).toBeGreaterThan(usersIndex)
    })

    it('should render Unidades Familiares after Campamentos in the full list', () => {
      const wrapper = mountComponent()

      const allLinks = wrapper.findAll('a')
      const campsIndex = allLinks.findIndex(l => l.attributes('data-testid') === 'sidebar-camps')
      const familyIndex = allLinks.findIndex(l => l.attributes('data-testid') === 'sidebar-family-units')

      expect(familyIndex).toBeGreaterThan(campsIndex)
    })
  })

  describe('Active state', () => {
    it('should mark Campamento Actual as active when on edition sub-routes', async () => {
      mockCurrentCampEdition.value = { id: 'edition-xyz' }
      await router.push('/camps/editions/edition-xyz/assignment')
      await router.isReady()

      const wrapper = mountComponent()
      await wrapper.vm.$nextTick()

      const link = wrapper.find('[data-testid="sidebar-current-edition"]')
      expect(link.classes()).toContain('bg-red-50')
    })
  })
})
