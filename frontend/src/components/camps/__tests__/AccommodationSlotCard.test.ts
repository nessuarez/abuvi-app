import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import AccommodationSlotCard from '../AccommodationSlotCard.vue'
import type { AssignmentAccommodationResponse, AssignmentFamilyResponse } from '@/types/accommodation-assignment'

vi.mock('primevue/progressbar', () => ({
  default: { name: 'ProgressBar', props: ['value', 'pt'], template: '<div class="progress-bar" />' }
}))

const makeAccommodation = (overrides: Partial<AssignmentAccommodationResponse> = {}): AssignmentAccommodationResponse => ({
  id: 'acc-1',
  name: 'Cabaña 1',
  type: 'Lodge',
  capacity: 10,
  countByFamily: false,
  zoneId: null,
  zoneName: null,
  sortOrder: 1,
  ...overrides
})

const makeFamily = (overrides: Partial<AssignmentFamilyResponse> = {}): AssignmentFamilyResponse => ({
  registrationId: 'reg-1',
  familyUnitId: 'fam-1',
  familyName: 'García',
  representativeName: 'Ana García',
  memberCount: 4,
  adultCount: 2,
  childCount: 2,
  hasPet: false,
  specialNeeds: null,
  campatesPreference: null,
  accommodationPreferences: [],
  ...overrides
})

describe('AccommodationSlotCard', () => {
  it('renders_accommodationNameAndCapacity', () => {
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation({ name: 'Cabaña Norte', capacity: 20 }),
        assignedFamilies: [],
        selectedFamily: null
      }
    })
    expect(wrapper.text()).toContain('Cabaña Norte')
    expect(wrapper.text()).toContain('20')
  })

  it('showsGreenBorder_whenSelectedFamilyHasFirstPreference', () => {
    const family = makeFamily({
      accommodationPreferences: [{ accommodationId: 'acc-1', preferenceOrder: 1 }]
    })
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation(),
        assignedFamilies: [],
        selectedFamily: family
      }
    })
    expect(wrapper.find('div').classes().join(' ')).toContain('border-green-400')
  })

  it('showsAmberBorder_whenSelectedFamilyHasSecondPreference', () => {
    const family = makeFamily({
      accommodationPreferences: [{ accommodationId: 'acc-1', preferenceOrder: 2 }]
    })
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation(),
        assignedFamilies: [],
        selectedFamily: family
      }
    })
    expect(wrapper.find('div').classes().join(' ')).toContain('border-amber-400')
  })

  it('showsRedBorder_whenOverCapacity', () => {
    const families = [
      makeFamily({ registrationId: 'reg-1', memberCount: 6 }),
      makeFamily({ registrationId: 'reg-2', memberCount: 6 })
    ]
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation({ capacity: 10, countByFamily: false }),
        assignedFamilies: families,
        selectedFamily: makeFamily({ accommodationPreferences: [] })
      }
    })
    expect(wrapper.find('div').classes().join(' ')).toContain('border-red-400')
  })

  it('emitsAssign_onClickWhenFamilySelected', async () => {
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation(),
        assignedFamilies: [],
        selectedFamily: makeFamily()
      }
    })
    await wrapper.find('div').trigger('click')
    expect(wrapper.emitted('assign')).toBeTruthy()
    expect(wrapper.emitted('assign')![0]).toEqual(['acc-1'])
  })

  it('emitsUnassign_onChipClose', async () => {
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation(),
        assignedFamilies: [makeFamily()],
        selectedFamily: null
      }
    })
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('unassign')).toBeTruthy()
    expect(wrapper.emitted('unassign')![0]).toEqual(['reg-1'])
  })

  it('countsOccupancyByFamily_forCaravan', () => {
    const families = [
      makeFamily({ registrationId: 'reg-1', memberCount: 5 }),
      makeFamily({ registrationId: 'reg-2', memberCount: 3 })
    ]
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation({ type: 'Caravan', capacity: 4, countByFamily: true }),
        assignedFamilies: families,
        selectedFamily: null
      }
    })
    // 2 families, not 8 persons
    expect(wrapper.text()).toContain('2 / 4')
  })

  it('countsOccupancyByPerson_forLodge', () => {
    const families = [
      makeFamily({ registrationId: 'reg-1', memberCount: 5 }),
      makeFamily({ registrationId: 'reg-2', memberCount: 3 })
    ]
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation({ type: 'Lodge', capacity: 20, countByFamily: false }),
        assignedFamilies: families,
        selectedFamily: null
      }
    })
    // 5 + 3 = 8 persons
    expect(wrapper.text()).toContain('8 / 20')
  })
})
