import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref, reactive, computed } from 'vue'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import Tooltip from 'primevue/tooltip'
import UsersAdminPanel from '../UsersAdminPanel.vue'
import type { User } from '@/types/user'

const mockToastAdd = vi.fn()
const mockConfirmRequire = vi.fn()
const mockFetchUsers = vi.fn()
const mockCreateUser = vi.fn()
const mockToggleUserActive = vi.fn()
const mockDeleteUser = vi.fn()
const mockClearError = vi.fn()

const mockUsers = ref<User[]>([])
const mockLoading = ref(false)
const mockError = ref<string | null>(null)

const mockRole = ref<string>('Admin')
const mockAuthStore = reactive({
  isAdmin: computed(() => mockRole.value === 'Admin'),
})

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: mockToastAdd }),
}))

vi.mock('primevue/useconfirm', () => ({
  useConfirm: () => ({ require: mockConfirmRequire }),
}))

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => mockAuthStore,
}))

vi.mock('@/composables/useUsers', () => ({
  useUsers: () => ({
    users: mockUsers,
    loading: mockLoading,
    error: mockError,
    fetchUsers: mockFetchUsers,
    createUser: mockCreateUser,
    toggleUserActive: mockToggleUserActive,
    deleteUser: mockDeleteUser,
    clearError: mockClearError,
  }),
}))

const makeUser = (overrides: Partial<User> = {}): User => ({
  id: 'user-1',
  email: 'test@example.com',
  firstName: 'Test',
  lastName: 'User',
  phone: null,
  role: 'Member',
  isActive: true,
  createdAt: '2025-01-01T00:00:00Z',
  updatedAt: '2025-01-01T00:00:00Z',
  ...overrides,
})

function mountPanel() {
  return mount(UsersAdminPanel, {
    global: {
      plugins: [PrimeVue],
      directives: { tooltip: Tooltip },
      stubs: {
        UserForm: true,
        UserRoleCell: true,
        UserRoleDialog: true,
        ConfirmDialog: true,
        Dialog: true,
        ProgressSpinner: true,
        Message: true,
        IconField: true,
        InputIcon: true,
        InputText: true,
      },
    },
  })
}

describe('UsersAdminPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockUsers.value = [makeUser(), makeUser({ id: 'user-2', email: 'other@example.com' })]
    mockLoading.value = false
    mockError.value = null
  })

  describe('Admin role', () => {
    beforeEach(() => {
      mockRole.value = 'Admin'
    })

    it('should show "Crear Usuario" button', () => {
      const wrapper = mountPanel()
      const html = wrapper.html()
      expect(html).toContain('Crear Usuario')
    })

    it('should show Toggle Active buttons in table rows', () => {
      const wrapper = mountPanel()
      const toggleBtns = wrapper.findAll('[data-testid^="toggle-active-"]')
      expect(toggleBtns.length).toBeGreaterThan(0)
    })

    it('should show Delete buttons in table rows', () => {
      const wrapper = mountPanel()
      const deleteBtns = wrapper.findAll('[data-testid^="delete-user-"]')
      expect(deleteBtns.length).toBeGreaterThan(0)
    })
  })

  describe('Board role', () => {
    beforeEach(() => {
      mockRole.value = 'Board'
    })

    it('should NOT show "Crear Usuario" button', () => {
      const wrapper = mountPanel()
      const html = wrapper.html()
      expect(html).not.toContain('Crear Usuario')
    })

    it('should NOT show Toggle Active buttons in table rows', () => {
      const wrapper = mountPanel()
      const toggleBtns = wrapper.findAll('[data-testid^="toggle-active-"]')
      expect(toggleBtns.length).toBe(0)
    })

    it('should NOT show Delete buttons in table rows', () => {
      const wrapper = mountPanel()
      const deleteBtns = wrapper.findAll('[data-testid^="delete-user-"]')
      expect(deleteBtns.length).toBe(0)
    })

    it('should show the users table', () => {
      const wrapper = mountPanel()
      expect(wrapper.find('[data-testid="users-table"]').exists()).toBe(true)
    })
  })
})
