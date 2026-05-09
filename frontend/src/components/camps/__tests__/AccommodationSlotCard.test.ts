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
  availableFeatures: [],
  quantity: 1,
  unitIndex: null,
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
  hasSpecialNeeds: false,
  requiredFeatures: [],
  friendlyFamilyUnitIds: [],
  ...overrides
})

describe('AccommodationSlotCard', () => {
  it('renders_accommodationNameAndCapacity', () => {
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation({ name: 'Cabaña Norte', capacity: 20 }),
        assignedFamilies: [],
        selectedFamily: null,
        hasFriendlyFamilyInZone: false
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
        selectedFamily: family,
        hasFriendlyFamilyInZone: false
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
        selectedFamily: family,
        hasFriendlyFamilyInZone: false
      }
    })
    expect(wrapper.find('div').classes().join(' ')).toContain('border-amber-400')
  })

  it('showsRedBorder_whenFamilyDoesNotFit', () => {
    const families = [
      makeFamily({ registrationId: 'reg-1', memberCount: 6 }),
      makeFamily({ registrationId: 'reg-2', memberCount: 6 })
    ]
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation({ capacity: 10, countByFamily: false }),
        assignedFamilies: families,
        selectedFamily: makeFamily({ accommodationPreferences: [] }),
        hasFriendlyFamilyInZone: false
      }
    })
    expect(wrapper.find('div').classes().join(' ')).toContain('border-red-500')
  })

  it('emitsAssign_onClickWhenFamilySelected', async () => {
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation(),
        assignedFamilies: [],
        selectedFamily: makeFamily(),
        hasFriendlyFamilyInZone: false
      }
    })
    await wrapper.find('div').trigger('click')
    expect(wrapper.emitted('assign')).toBeTruthy()
    expect(wrapper.emitted('assign')![0]).toEqual(['acc-1', null])
  })

  it('emitsUnassign_onChipClose', async () => {
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation(),
        assignedFamilies: [makeFamily()],
        selectedFamily: null,
        hasFriendlyFamilyInZone: false
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
        selectedFamily: null,
        hasFriendlyFamilyInZone: false
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
        selectedFamily: null,
        hasFriendlyFamilyInZone: false
      }
    })
    // 5 + 3 = 8 persons
    expect(wrapper.text()).toContain('8 / 20')
  })

  it('showsGreenBadge_whenAllFeaturesMatch', () => {
    const family = makeFamily({ requiredFeatures: ['feat-1'] })
    const accommodation = makeAccommodation({ availableFeatures: ['feat-1'] })
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation,
        assignedFamilies: [],
        selectedFamily: family,
        hasFriendlyFamilyInZone: false
      }
    })
    expect(wrapper.text()).toContain('Cumple todas las preferencias')
  })

  it('showsGreenBadge_whenFriendlyFamilyIsAlreadyAssignedHere', () => {
    const family = makeFamily({ friendlyFamilyUnitIds: ['fu-friend'] })
    const assignedFriend = makeFamily({ registrationId: 'reg-friend', familyUnitId: 'fu-friend' })
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation(),
        assignedFamilies: [assignedFriend],
        selectedFamily: family,
        hasFriendlyFamilyInZone: false
      }
    })
    expect(wrapper.text()).toContain('Familia amiga ya aquí')
  })

  it('showsBlueBadge_whenFriendlyFamilyInZone_hasFriendlyFamilyInZone_prop_true', () => {
    const family = makeFamily({ friendlyFamilyUnitIds: ['fu-other'] })
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation(),
        assignedFamilies: [],
        selectedFamily: family,
        hasFriendlyFamilyInZone: true
      }
    })
    expect(wrapper.text()).toContain('Familia amiga en misma zona')
  })

  it('showsAmberBadge_whenSomeRequiredFeaturesAreMissing', () => {
    const family = makeFamily({ requiredFeatures: ['feat-1', 'feat-2'] })
    const accommodation = makeAccommodation({ availableFeatures: ['feat-1'] })
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation,
        assignedFamilies: [],
        selectedFamily: family,
        hasFriendlyFamilyInZone: false
      }
    })
    expect(wrapper.text()).toContain('Preferencia no cubierta: feat-2')
  })

  it('showsMissingFeaturesList_inAmberBadge', () => {
    const family = makeFamily({ requiredFeatures: ['feat-1', 'feat-2'] })
    const accommodation = makeAccommodation({ availableFeatures: ['feat-1'] })
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation,
        assignedFamilies: [],
        selectedFamily: family,
        hasFriendlyFamilyInZone: false
      }
    })
    const badge = wrapper.find('[title^="Faltan:"]')
    expect(badge.exists()).toBe(true)
    expect(badge.attributes('title')).toContain('feat-2')
  })

  it('showsImprovedCapacityMessage_whenFamilyDoesNotFit', () => {
    const family = makeFamily({ memberCount: 3 })
    const assigned = [
      makeFamily({ registrationId: 'reg-x', memberCount: 1 }),
      makeFamily({ registrationId: 'reg-y', memberCount: 1 })
    ]
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation({ capacity: 2, countByFamily: false }),
        assignedFamilies: assigned,
        selectedFamily: family,
        hasFriendlyFamilyInZone: false
      }
    })
    expect(wrapper.text()).toContain('Necesitan 3 pers., quedan 0')
  })

  it('signalClass_isGreen_whenAllFeaturesMatchEvenWithNoPreference', () => {
    const family = makeFamily({ requiredFeatures: ['feat-1'], accommodationPreferences: [] })
    const accommodation = makeAccommodation({ availableFeatures: ['feat-1'] })
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation,
        assignedFamilies: [],
        selectedFamily: family,
        hasFriendlyFamilyInZone: false
      }
    })
    expect(wrapper.find('div').classes().join(' ')).toContain('border-green-400')
  })

  it('signalClass_priority_redBeatsGreen', () => {
    // family has 1st preference but capacity is full → red
    const family = makeFamily({
      memberCount: 4,
      accommodationPreferences: [{ accommodationId: 'acc-1', preferenceOrder: 1 }]
    })
    const assigned = [
      makeFamily({ registrationId: 'reg-x', memberCount: 5 }),
      makeFamily({ registrationId: 'reg-y', memberCount: 6 })
    ]
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation({ capacity: 10, countByFamily: false }),
        assignedFamilies: assigned,
        selectedFamily: family,
        hasFriendlyFamilyInZone: false
      }
    })
    // occupied=11, remaining=-1, needed=4 → red
    expect(wrapper.find('div').classes().join(' ')).toContain('border-red-500')
    expect(wrapper.find('div').classes().join(' ')).not.toContain('border-green-400')
  })

  it('signalClass_isBlue_whenHasFriendlyFamilyInZone', () => {
    // no preference, no required features, friendly family in zone but not here
    const family = makeFamily({ friendlyFamilyUnitIds: ['fu-other'] })
    const wrapper = mount(AccommodationSlotCard, {
      props: {
        accommodation: makeAccommodation(),
        assignedFamilies: [],
        selectedFamily: family,
        hasFriendlyFamilyInZone: true
      }
    })
    expect(wrapper.find('div').classes().join(' ')).toContain('border-blue-400')
  })
})
