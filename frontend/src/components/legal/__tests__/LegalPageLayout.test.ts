import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createRouter, createMemoryHistory } from 'vue-router'
import LegalPageLayout from '@/components/legal/LegalPageLayout.vue'

// Mock PrimeVue Button
vi.mock('primevue/button', () => ({
  default: {
    name: 'Button',
    props: ['label', 'icon', 'outlined', 'size', 'ariaLabel'],
    emits: ['click'],
    template: `<button :data-testid="$attrs['data-testid']" @click="$emit('click')">{{ label }}</button>`
  }
}))

const router = createRouter({
  history: createMemoryHistory(),
  routes: [{ path: '/', component: { template: '<div />' } }]
})

const defaultProps = {
  title: 'Aviso Legal',
  lastUpdated: 'Febrero 2026'
}

const tocEntries = [
  { id: 'section-one', label: '1. Section One', level: 1 as const },
  { id: 'section-two', label: '2. Section Two', level: 1 as const }
]

function mountLayout(props = {}, slots = {}) {
  return mount(LegalPageLayout, {
    props: { ...defaultProps, ...props },
    slots,
    global: { plugins: [router] }
  })
}

describe('LegalPageLayout', () => {
  it('should render the page title in h1', () => {
    const wrapper = mountLayout()
    const h1 = wrapper.find('h1')
    expect(h1.exists()).toBe(true)
    expect(h1.text()).toBe('Aviso Legal')
  })

  it('should render the last updated date', () => {
    const wrapper = mountLayout()
    const el = wrapper.find('[data-testid="legal-last-updated"]')
    expect(el.exists()).toBe(true)
    expect(el.text()).toContain('Febrero 2026')
  })

  it('should render the "Volver al inicio" back link', () => {
    const wrapper = mountLayout()
    const backLink = wrapper.find('[data-testid="legal-back-link"]')
    expect(backLink.exists()).toBe(true)
    expect(backLink.text()).toContain('Volver al inicio')
  })

  it('should render the print button when showPrintButton is true (default)', () => {
    const wrapper = mountLayout()
    const printBtn = wrapper.find('[data-testid="print-button"]')
    expect(printBtn.exists()).toBe(true)
  })

  it('should not render the print button when showPrintButton is false', () => {
    const wrapper = mountLayout({ showPrintButton: false })
    const printBtn = wrapper.find('[data-testid="print-button"]')
    expect(printBtn.exists()).toBe(false)
  })

  it('should render slot content', () => {
    const wrapper = mountLayout({}, { default: '<p>Legal content here</p>' })
    expect(wrapper.html()).toContain('Legal content here')
  })

  it('should show TOC sidebar when showToc is true and tocEntries provided', () => {
    const wrapper = mountLayout({ showToc: true, tocEntries })
    const toc = wrapper.find('[data-testid="legal-toc"]')
    expect(toc.exists()).toBe(true)
  })

  it('should hide TOC sidebar when showToc is false', () => {
    const wrapper = mountLayout({ showToc: false, tocEntries })
    const toc = wrapper.find('[data-testid="legal-toc"]')
    expect(toc.exists()).toBe(false)
  })

  it('should hide TOC when tocEntries is empty', () => {
    const wrapper = mountLayout({ showToc: true, tocEntries: [] })
    const toc = wrapper.find('[data-testid="legal-toc"]')
    expect(toc.exists()).toBe(false)
  })

  it('should call window.print when print button is clicked', async () => {
    const printMock = vi.spyOn(window, 'print').mockImplementation(() => {})
    const wrapper = mountLayout()
    await wrapper.find('[data-testid="print-button"]').trigger('click')
    expect(printMock).toHaveBeenCalledOnce()
    printMock.mockRestore()
  })
})
