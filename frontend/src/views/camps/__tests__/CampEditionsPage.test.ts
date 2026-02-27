import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref, defineComponent, h } from 'vue'
import { createRouter, createMemoryHistory } from 'vue-router'
import CampEditionsPage from '@/views/camps/CampEditionsPage.vue'

const editionData = {
  id: 'edition-1',
  campId: 'camp-1',
  year: 2026,
  startDate: '2026-08-15',
  endDate: '2026-08-22',
  location: 'Test Location',
  pricePerAdult: 100,
  pricePerChild: 50,
  pricePerBaby: 0,
  maxCapacity: 50,
  status: 'Open',
  isArchived: false,
  createdAt: '2026-01-01',
  updatedAt: '2026-01-01',
  camp: { id: 'camp-1', name: 'Summer Camp' },
}

vi.mock('@/composables/useCampEditions', () => ({
  useCampEditions: () => ({
    allEditions: ref([editionData]),
    loading: ref(false),
    error: ref(null),
    fetchAllEditions: vi.fn(),
    changeStatus: vi.fn(),
    promoteEdition: vi.fn(),
  }),
}))

vi.mock('@/composables/useCamps', () => ({
  useCamps: () => ({
    camps: ref([]),
    fetchCamps: vi.fn(),
  }),
}))

vi.mock('primevue/usetoast', () => ({
  useToast: () => ({ add: vi.fn() }),
}))

vi.mock('@vueuse/core', () => ({
  useDebounceFn: (fn: Function) => fn,
}))

// DataTable stub that iterates `value` and invokes each Column's #body slot
const DataTableStub = defineComponent({
  name: 'DataTable',
  props: { value: Array, stripedRows: Boolean, paginator: Boolean, rows: Number, loading: Boolean },
  setup(props, { slots }) {
    return () => {
      const defaultSlotContent = slots.default?.() ?? []
      const rows = (props.value ?? []) as any[]
      return h('table', { 'data-testid': 'editions-table' },
        rows.map((item) =>
          h('tr', { key: item.id },
            defaultSlotContent.map((vnode: any) => {
              const bodySlot = vnode.children?.body
              if (typeof bodySlot === 'function') {
                return h('td', bodySlot({ data: item }))
              }
              const field = vnode.props?.field
              return h('td', field ? String(item[field] ?? '') : '')
            })
          )
        )
      )
    }
  },
})

const router = createRouter({
  history: createMemoryHistory(),
  routes: [
    { path: '/camps/editions', name: 'camp-editions', component: { template: '<div />' } },
    { path: '/camps/editions/:id', name: 'camp-edition-detail', component: { template: '<div />' } },
  ],
})

describe('CampEditionsPage — clickable edition names', () => {
  beforeEach(async () => {
    await router.push('/camps/editions')
    await router.isReady()
  })

  const mountPage = () =>
    mount(CampEditionsPage, {
      global: {
        plugins: [router],
        stubs: {
          Container: { template: '<div><slot /></div>' },
          CampEditionStatusBadge: true,
          CampEditionStatusDialog: true,
          CampEditionUpdateDialog: true,
          Toast: true,
          ProgressSpinner: true,
          Message: true,
          InputNumber: true,
          Select: true,
          DataTable: DataTableStub,
          Column: { template: '<div />' },
          Button: {
            props: ['label', 'icon', 'text', 'rounded', 'size', 'severity', 'disabled', 'outlined', 'iconPos'],
            template: '<button>{{ label }}</button>',
          },
        },
        directives: {
          tooltip: {},
        },
      },
    })

  it('renders camp name as a router-link to edition detail', () => {
    const wrapper = mountPage()
    const links = wrapper.findAll('a')
    const editionLink = links.find((l) => l.attributes('href') === '/camps/editions/edition-1')
    expect(editionLink).toBeDefined()
    expect(editionLink!.text()).toBe('Summer Camp')
  })

  it('router-link has correct styling classes', () => {
    const wrapper = mountPage()
    const links = wrapper.findAll('a')
    const editionLink = links.find((l) => l.attributes('href') === '/camps/editions/edition-1')
    expect(editionLink).toBeDefined()
    expect(editionLink!.classes()).toContain('font-medium')
    expect(editionLink!.classes()).toContain('text-primary')
  })
})
