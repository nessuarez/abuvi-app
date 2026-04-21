import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import App from '@/App.vue'

// ── Auth store mock ───────────────────────────────────────────────────────────
const authMock = vi.hoisted(() => ({
  isAuthenticated: false,
  user: null as { email: string; firstName: string; lastName: string } | null,
  restoreSession: vi.fn(),
}))

vi.mock('@/stores/auth', () => ({ useAuthStore: () => authMock }))

// ── Child component stubs ─────────────────────────────────────────────────────
vi.mock('@/layouts/AuthenticatedLayout.vue', () => ({
  default: { name: 'AuthenticatedLayout', template: '<div><slot /></div>' },
}))
vi.mock('primevue/toast', () => ({
  default: { name: 'Toast', template: '<div />' },
}))

const router = createRouter({
  history: createMemoryHistory(),
  routes: [
    { path: '/', component: { template: '<div />' } },
    { path: '/home', component: { template: '<div />' } },
  ],
})

function mountApp() {
  return mount(App, { global: { plugins: [createPinia(), router] } })
}

describe('App.vue — Userback identity watcher', () => {
  let originalUserback: unknown

  beforeEach(() => {
    originalUserback = (window as any).Userback
    authMock.isAuthenticated = false
    authMock.user = null
    authMock.restoreSession.mockReset()
  })

  afterEach(() => {
    ;(window as any).Userback = originalUserback
    vi.restoreAllMocks()
  })

  it('should not call identify when user is not authenticated', async () => {
    const identify = vi.fn()
    ;(window as any).Userback = { identify }

    mountApp()
    await flushPromises()

    expect(identify).not.toHaveBeenCalled()
  })

  it('should not throw when Userback script has not loaded yet (window.Userback is {})', async () => {
    ;(window as any).Userback = {}
    authMock.isAuthenticated = true
    authMock.user = { email: 'u@test.com', firstName: 'A', lastName: 'B' }

    expect(() => mountApp()).not.toThrow()
    await flushPromises()
  })

  it('should call identify with email and full name when authenticated and Userback is ready', async () => {
    const identify = vi.fn()
    ;(window as any).Userback = { identify }
    authMock.isAuthenticated = true
    authMock.user = { email: 'u@test.com', firstName: 'Ana', lastName: 'García' }

    mountApp()
    await flushPromises()

    expect(identify).toHaveBeenCalledWith('u@test.com', {
      name: 'Ana García',
      email: 'u@test.com',
    })
  })

  it('should not propagate errors if Userback.identify throws', async () => {
    const consoleWarn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    ;(window as any).Userback = {
      identify: () => {
        throw new Error('Userback internal error')
      },
    }
    authMock.isAuthenticated = true
    authMock.user = { email: 'u@test.com', firstName: 'A', lastName: 'B' }

    expect(() => mountApp()).not.toThrow()
    await flushPromises()
    expect(consoleWarn).toHaveBeenCalledWith(
      '[Userback] Failed to identify user:',
      expect.any(Error),
    )
  })
})
