import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ref, reactive, computed, nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import Tooltip from 'primevue/tooltip'
import UsersAdminPanel from '../UsersAdminPanel.vue'
import type { User } from '@/types/user'

const mockToastAdd = vi.fn()
const mockConfirmRequire = vi.fn()
const mockFetchUsers = vi.fn()
const mockCreateUser = vi.fn()
const mockUpdateUser = vi.fn()
const mockToggleUserActive = vi.fn()
const mockDeleteUser = vi.fn()
const mockClearError = vi.fn()

const mockUsers = ref<User[]>([])
const mockLoading = ref(false)
const mockError = ref<string | null>(null)

const mockRole = ref<string>('Admin')
const mockAuthStore = reactive({
  isAdmin: computed(() => mockRole.value === 'Admin'),
  isBoard: computed(() => mockRole.value === 'Board'),
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
    updateUser: mockUpdateUser,
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
  emailVerified: false,
  createdAt: '2025-01-01T00:00:00Z',
  updatedAt: '2025-01-01T00:00:00Z',
  ...overrides,
})

const dialogStub = {
  name: 'Dialog',
  template: '<div v-if="visible"><span>{{ header }}</span><slot /></div>',
  props: ['visible', 'modal', 'header'],
}

const userFormStub = {
  name: 'UserForm',
  template: '<div />',
  props: ['mode', 'user', 'loading'],
  emits: ['submit', 'cancel'],
}

function mountPanel() {
  return mount(UsersAdminPanel, {
    global: {
      plugins: [PrimeVue],
      directives: { tooltip: Tooltip },
      stubs: {
        UserForm: userFormStub,
        UserRoleCell: true,
        UserRoleDialog: true,
        ConfirmDialog: true,
        Dialog: dialogStub,
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

  describe('Edit User Dialog', () => {
    it('renders edit button for each user row (Admin)', () => {
      mockRole.value = 'Admin'
      const wrapper = mountPanel()
      const editBtns = wrapper.findAll('[data-testid^="edit-user-"]')
      expect(editBtns.length).toBe(2)
    })

    it('renders edit button for each user row (Board)', () => {
      mockRole.value = 'Board'
      const wrapper = mountPanel()
      const editBtns = wrapper.findAll('[data-testid^="edit-user-"]')
      expect(editBtns.length).toBe(2)
    })

    it('opens edit dialog when edit button is clicked', async () => {
      mockRole.value = 'Admin'
      const wrapper = mountPanel()
      const editBtn = wrapper.find('[data-testid="edit-user-user-1"]')
      await editBtn.trigger('click')
      await nextTick()
      expect(wrapper.html()).toContain('Editar Perfil de Usuario')
    })

    it('calls updateUser on form submit and shows success toast', async () => {
      mockRole.value = 'Admin'
      const updatedUser = makeUser({ firstName: 'Updated', lastName: 'Name' })
      mockUpdateUser.mockResolvedValue(updatedUser)

      const wrapper = mountPanel()
      const editBtn = wrapper.find('[data-testid="edit-user-user-1"]')
      await editBtn.trigger('click')
      await nextTick()

      const userForm = wrapper.findComponent({ name: 'UserForm' })
      await userForm.vm.$emit('submit', { firstName: 'Updated', lastName: 'Name', phone: null, isActive: true })
      await nextTick()

      expect(mockUpdateUser).toHaveBeenCalledWith('user-1', expect.objectContaining({ firstName: 'Updated' }))
      expect(mockToastAdd).toHaveBeenCalledWith(expect.objectContaining({ severity: 'success' }))
    })

    it('keeps dialog open when update fails (no updated user returned)', async () => {
      mockRole.value = 'Admin'
      mockUpdateUser.mockResolvedValue(null)

      const wrapper = mountPanel()
      const editBtn = wrapper.find('[data-testid="edit-user-user-1"]')
      await editBtn.trigger('click')
      await nextTick()

      const userForm = wrapper.findComponent({ name: 'UserForm' })
      await userForm.vm.$emit('submit', { firstName: 'Bad', lastName: 'Request', phone: null, isActive: true })
      await nextTick()

      expect(wrapper.html()).toContain('Editar Perfil de Usuario')
      expect(mockToastAdd).not.toHaveBeenCalled()
    })
  })
})
