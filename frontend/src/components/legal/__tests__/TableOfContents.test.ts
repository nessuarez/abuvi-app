import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import TableOfContents from '@/components/legal/TableOfContents.vue'

const sampleEntries = [
  { id: 'section-one', label: '1. Section One', level: 1 as const },
  { id: 'section-two', label: '2. Section Two', level: 1 as const },
  { id: 'subsection', label: '2.1 Sub Section', level: 2 as const }
]

describe('TableOfContents', () => {
  it('should render nav with aria-label "Tabla de contenidos"', () => {
    const wrapper = mount(TableOfContents, { props: { entries: sampleEntries } })
    const nav = wrapper.find('nav')
    expect(nav.exists()).toBe(true)
    expect(nav.attributes('aria-label')).toBe('Tabla de contenidos')
  })

  it('should render one link per entry', () => {
    const wrapper = mount(TableOfContents, { props: { entries: sampleEntries } })
    const links = wrapper.findAll('a')
    expect(links).toHaveLength(sampleEntries.length)
  })

  it('should set href to #entry.id for each entry', () => {
    const wrapper = mount(TableOfContents, { props: { entries: sampleEntries } })
    const links = wrapper.findAll('a')
    expect(links[0].attributes('href')).toBe('#section-one')
    expect(links[1].attributes('href')).toBe('#section-two')
    expect(links[2].attributes('href')).toBe('#subsection')
  })

  it('should display entry label text', () => {
    const wrapper = mount(TableOfContents, { props: { entries: sampleEntries } })
    const links = wrapper.findAll('a')
    expect(links[0].text()).toBe('1. Section One')
    expect(links[1].text()).toBe('2. Section Two')
    expect(links[2].text()).toBe('2.1 Sub Section')
  })

  it('should apply pl-4 class to level 2 entries', () => {
    const wrapper = mount(TableOfContents, { props: { entries: sampleEntries } })
    const listItems = wrapper.findAll('li')
    expect(listItems[0].classes()).not.toContain('pl-4')
    expect(listItems[1].classes()).not.toContain('pl-4')
    expect(listItems[2].classes()).toContain('pl-4')
  })

  it('should render empty list when entries is empty', () => {
    const wrapper = mount(TableOfContents, { props: { entries: [] } })
    const links = wrapper.findAll('a')
    expect(links).toHaveLength(0)
    expect(wrapper.find('nav').exists()).toBe(true)
  })

  it('should have data-testid attribute for E2E testing', () => {
    const wrapper = mount(TableOfContents, { props: { entries: sampleEntries } })
    expect(wrapper.find('[data-testid="legal-toc"]').exists()).toBe(true)
  })
})
