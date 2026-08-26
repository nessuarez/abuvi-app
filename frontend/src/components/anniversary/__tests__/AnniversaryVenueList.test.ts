import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import AnniversaryVenueList from '@/components/anniversary/AnniversaryVenueList.vue'
import type { CampHistoryVenue } from '@/types/camp-history'

const espinosa: CampHistoryVenue = {
  campId: 'camp-1',
  campName: 'Espinosa de los Monteros',
  location: 'Burgos',
  latitude: 43.077348,
  longitude: -3.552172,
  years: [1983, 1993, 2003, 2015],
  totalEditionsAtVenue: 4,
  photoCount: 12
}

const miraflores: CampHistoryVenue = {
  campId: 'camp-2',
  campName: 'Miraflores de la Sierra',
  location: 'Madrid',
  latitude: 40.81,
  longitude: -3.77,
  years: [1976],
  totalEditionsAtVenue: 1,
  photoCount: 0
}

interface ListProps {
  venues: CampHistoryVenue[]
  selectedYear: number | null
  selectedCampId: string | null
}

const mountList = (props: Partial<ListProps> = {}) =>
  mount(AnniversaryVenueList, {
    props: {
      venues: [espinosa, miraflores],
      selectedYear: null,
      selectedCampId: null,
      ...props
    }
  })

describe('AnniversaryVenueList', () => {
  it('renders one chip per edition year', () => {
    const wrapper = mountList()

    const chips = wrapper.findAll('button[aria-label^="Edición de"]')
    expect(chips).toHaveLength(5)
    expect(chips[0].text()).toBe('1983')
  })

  it('emits the year when a chip is clicked', async () => {
    const wrapper = mountList()

    await wrapper.findAll('button[aria-label^="Edición de"]')[2].trigger('click')

    expect(wrapper.emitted('selectYear')?.[0]).toEqual([2003])
  })

  it('emits the venue when the row is clicked', async () => {
    const wrapper = mountList()

    await wrapper.findAll('li')[1].find('button').trigger('click')

    expect(wrapper.emitted('selectVenue')?.[0]).toEqual(['camp-2'])
  })

  it('marks the selected year as current', () => {
    const wrapper = mountList({ selectedYear: 2015, selectedCampId: 'camp-1' })

    const current = wrapper.findAll('[aria-current="true"]')
    expect(current).toHaveLength(1)
    expect(current[0].text()).toBe('2015')
  })

  it('shows the edition tally only where the association came back', () => {
    const wrapper = mountList()

    const badges = wrapper.findAll('li').map((li) => li.text())
    expect(badges[0]).toContain('4 ediciones')
    expect(badges[1]).not.toContain('ediciones')
  })

  it('names the venue in each chip accessible label', () => {
    const wrapper = mountList()

    expect(wrapper.findAll('button[aria-label^="Edición de"]')[0].attributes('aria-label')).toBe(
      'Edición de 1983 en Espinosa de los Monteros'
    )
  })

  it('scrolls the selected venue into view without yanking the list', async () => {
    const wrapper = mountList()
    const scrollIntoView = vi.fn()
    wrapper.findAll('li').forEach((li) => {
      ;(li.element as HTMLElement).scrollIntoView = scrollIntoView
    })

    await wrapper.setProps({ selectedCampId: 'camp-2' })

    expect(scrollIntoView).toHaveBeenCalledWith(expect.objectContaining({ block: 'nearest' }))
  })
})
