import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { ref, reactive, computed, nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import DataTable from 'primevue/datatable'
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
const mockRefresh = vi.fn()

const mockUsers = ref<User[]>([])
const mockLoading = ref(false)
const mockError = ref<string | null>(null)
const mockPagination = ref({ page: 1, pageSize: 20, totalCount: 2, totalPages: 1 })

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
    pagination: mockPagination,
    refresh: mockRefresh,
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

const searchInputStub = {
  name: 'InputText',
  props: ['modelValue'],
  emits: ['update:modelValue'],
  template:
    `<input :value="modelValue" @input="$emit('update:modelValue', $event.target.value)" />`,
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
        IconField: { template: '<div><slot /></div>' },
        InputIcon: true,
        InputText: searchInputStub,
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
    mockPagination.value = { page: 1, pageSize: 20, totalCount: 2, totalPages: 1 }
  })

  afterEach(() => {
    vi.useRealTimers()
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
  describe('Server-side search, paging and sort', () => {
    beforeEach(() => {
      mockRole.value = 'Admin'
    })

    it('fetches first page on mount', () => {
      mountPanel()
      expect(mockFetchUsers).toHaveBeenCalledWith({
        page: 1, pageSize: 20, search: '', sortBy: 'createdAt', sortOrder: 'desc',
      })
    })

    it('debounces search input and resets to page 1', async () => {
      vi.useFakeTimers()
      const wrapper = mountPanel()
      mockFetchUsers.mockClear()

      await wrapper.find('[data-testid="users-search-input"]').setValue('ana')
      vi.advanceTimersByTime(299)
      expect(mockFetchUsers).not.toHaveBeenCalled()
      vi.advanceTimersByTime(1)

      expect(mockFetchUsers).toHaveBeenCalledTimes(1)
      expect(mockFetchUsers).toHaveBeenCalledWith(
        expect.objectContaining({ search: 'ana', page: 1 })
      )
    })

    it('does not filter rows client-side', async () => {
      const wrapper = mountPanel()
      await wrapper.find('[data-testid="users-search-input"]').setValue('zzz')
      await nextTick()
      expect(wrapper.findAll('[data-testid^="edit-user-"]').length).toBe(2)
    })

    it('requests the next page on paginator page event', async () => {
      const wrapper = mountPanel()
      mockFetchUsers.mockClear()

      wrapper.findComponent(DataTable).vm.$emit('page', { page: 1, rows: 20, first: 20 })

      expect(mockFetchUsers).toHaveBeenCalledWith(
        expect.objectContaining({ page: 2, pageSize: 20 })
      )
    })

    it('maps column sort to server sortBy/sortOrder', async () => {
      const wrapper = mountPanel()
      mockFetchUsers.mockClear()

      wrapper.findComponent(DataTable).vm.$emit('sort', { sortField: 'email', sortOrder: 1 })

      expect(mockFetchUsers).toHaveBeenCalledWith(
        expect.objectContaining({ sortBy: 'email', sortOrder: 'asc', page: 1 })
      )
    })

    it('resets to createdAt desc when the sort is cleared', async () => {
      const wrapper = mountPanel()
      mockFetchUsers.mockClear()

      wrapper.findComponent(DataTable).vm.$emit('sort', { sortField: 'email', sortOrder: 0 })

      expect(mockFetchUsers).toHaveBeenCalledWith(
        expect.objectContaining({ sortBy: 'createdAt', sortOrder: 'desc', page: 1 })
      )
    })

    it('shows total count', () => {
      mockPagination.value = { page: 1, pageSize: 20, totalCount: 137, totalPages: 7 }
      const wrapper = mountPanel()
      expect(wrapper.find('[data-testid="users-total"]').text()).toContain('137 usuarios')
    })

    it('shows search-specific empty message', async () => {
      const wrapper = mountPanel()
      await wrapper.find('[data-testid="users-search-input"]').setValue('nadie')
      mockUsers.value = []
      await nextTick()
      expect(wrapper.html()).toContain('No se encontraron usuarios para')
      expect(wrapper.html()).toContain('nadie')
    })

    it('shows plain empty message without a search term', async () => {
      mockUsers.value = []
      const wrapper = mountPanel()
      await nextTick()
      expect(wrapper.html()).toContain('No hay usuarios.')
    })

    it('refreshes list after role update', async () => {
      const wrapper = mountPanel()
      wrapper
        .findComponent({ name: 'UserRoleDialog' })
        .vm.$emit('role-updated', makeUser({ role: 'Board' }))
      await nextTick()
      expect(mockRefresh).toHaveBeenCalled()
    })
  })
})
