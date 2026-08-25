import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import AnniversaryYearStrip from '@/components/anniversary/AnniversaryYearStrip.vue'
import type { CampHistoryEntry } from '@/types/camp-history'

const makeEntry = (year: number, photoCount = 0): CampHistoryEntry => ({
  year,
  campId: `camp-${year}`,
  campName: `Sede ${year}`,
  location: 'Burgos',
  latitude: 43,
  longitude: -3,
  editionNumber: 1,
  totalEditionsAtVenue: 1,
  photoCount,
  previewPhotos: []
})

const fiftyYears = Array.from({ length: 50 }, (_, i) => makeEntry(1976 + i))

const mountStrip = (entries: CampHistoryEntry[], selectedYear: number | null = null) =>
  mount(AnniversaryYearStrip, { props: { entries, selectedYear } })

describe('AnniversaryYearStrip', () => {
  it('renders one button per edition year', () => {
    const wrapper = mountStrip(fiftyYears)

    expect(wrapper.findAll('button')).toHaveLength(50)
  })

  it('emits the year when a chip is clicked', async () => {
    const wrapper = mountStrip([makeEntry(1976), makeEntry(1983)])

    await wrapper.findAll('button')[1].trigger('click')

    expect(wrapper.emitted('selectYear')?.[0]).toEqual([1983])
  })

  it('marks the selected year as current', () => {
    const wrapper = mountStrip([makeEntry(1976), makeEntry(1983)], 1983)

    const current = wrapper.findAll('[aria-current="true"]')
    expect(current).toHaveLength(1)
    expect(current[0].text()).toBe('1983')
  })

  it('distinguishes years with memories from years without', () => {
    const wrapper = mountStrip([makeEntry(1987, 0), makeEntry(2003, 25)])

    const dots = wrapper.findAll('[data-has-photos]')
    expect(dots[0].attributes('data-has-photos')).toBe('false')
    expect(dots[1].attributes('data-has-photos')).toBe('true')
  })

  it('says in the accessible label whether a year has memories', () => {
    const wrapper = mountStrip([makeEntry(1987, 0), makeEntry(2003, 25)])

    const buttons = wrapper.findAll('button')
    expect(buttons[0].attributes('aria-label')).toContain('sin recuerdos')
    expect(buttons[1].attributes('aria-label')).toContain('25 recuerdos')
  })

  it('keeps the selected year in view as the tour advances', async () => {
    const wrapper = mountStrip([makeEntry(1976), makeEntry(1983)])
    const scrollIntoView = vi.fn()
    wrapper.findAll('button').forEach((button) => {
      ;(button.element as HTMLElement).scrollIntoView = scrollIntoView
    })

    await wrapper.setProps({ selectedYear: 1983 })

    expect(scrollIntoView).toHaveBeenCalledWith(expect.objectContaining({ inline: 'center' }))
  })
})
