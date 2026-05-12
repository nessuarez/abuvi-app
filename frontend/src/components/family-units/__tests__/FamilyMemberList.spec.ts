import { describe, it, expect, afterEach } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import Tooltip from 'primevue/tooltip'
import FamilyMemberList from '../FamilyMemberList.vue'
import { FamilyRelationship, type FamilyMemberResponse } from '@/types/family-unit'

const mockMember: FamilyMemberResponse = {
  id: 'member-1',
  familyUnitId: 'unit-1',
  firstName: 'Ana',
  lastName: 'García',
  dateOfBirth: '1990-05-15',
  relationship: FamilyRelationship.Spouse,
  documentNumber: null,
  email: null,
  phone: null,
  hasMedicalNotes: false,
  hasAllergies: false,
  profilePhotoUrl: null,
  userId: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

const completeMember: FamilyMemberResponse = {
  id: 'member-2',
  familyUnitId: 'unit-1',
  firstName: 'Carlos',
  lastName: 'López',
  dateOfBirth: '1985-03-20',
  relationship: FamilyRelationship.Parent,
  documentNumber: '12345678A',
  email: 'carlos@example.com',
  phone: '+34612345678',
  hasMedicalNotes: false,
  hasAllergies: false,
  profilePhotoUrl: null,
  userId: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

const minorMember: FamilyMemberResponse = {
  id: 'member-3',
  familyUnitId: 'unit-1',
  firstName: 'Lucía',
  lastName: 'García',
  dateOfBirth: '2015-08-10',
  relationship: FamilyRelationship.Child,
  documentNumber: null,
  email: null,
  phone: null,
  hasMedicalNotes: false,
  hasAllergies: false,
  profilePhotoUrl: null,
  userId: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

const globalConfig = {
  plugins: [[PrimeVue, { unstyled: true }]] as [unknown],
  directives: { tooltip: Tooltip },
}

const openDrawerForMember = async (wrapper: ReturnType<typeof mount>, memberId: string) => {
  const card = wrapper.find(`[data-testid="member-card-${memberId}"]`)
  await card.trigger('click')
  await wrapper.vm.$nextTick()
}

// Drawer content is teleported to <body> by PrimeVue — mount with attachTo and unmount after each test
describe('FamilyMemberList — manageMembership', () => {
  let wrapper: ReturnType<typeof mount>
  let div: HTMLDivElement

  afterEach(() => {
    wrapper.unmount()
    if (div.parentNode) document.body.removeChild(div)
  })

  const mountAttached = (props: object) => {
    div = document.createElement('div')
    document.body.appendChild(div)
    wrapper = mount(FamilyMemberList, {
      props: { members: [mockMember], loading: false, ...props },
      global: globalConfig,
      attachTo: div,
    })
    return wrapper
  }

  it('renders manageMembership button in drawer when canManageMemberships is true', async () => {
    const wrapper = mountAttached({ canManageMemberships: true })
    await openDrawerForMember(wrapper, 'member-1')
    expect(document.querySelector('[data-testid="manage-membership-btn-member-1"]')).not.toBeNull()
  })

  it('does not render manageMembership button in drawer when canManageMemberships is false', async () => {
    const wrapper = mountAttached({ canManageMemberships: false })
    await openDrawerForMember(wrapper, 'member-1')
    expect(document.querySelector('[data-testid="manage-membership-btn-member-1"]')).toBeNull()
  })

  it('does not render manageMembership button when canManageMemberships is omitted', async () => {
    const wrapper = mountAttached({})
    await openDrawerForMember(wrapper, 'member-1')
    expect(document.querySelector('[data-testid="manage-membership-btn-member-1"]')).toBeNull()
  })

  it('emits manageMembership with the correct member when button is clicked', async () => {
    const wrapper = mountAttached({ canManageMemberships: true })
    await openDrawerForMember(wrapper, 'member-1')
    const btn = document.querySelector('[data-testid="manage-membership-btn-member-1"]') as HTMLElement
    btn.click()
    await wrapper.vm.$nextTick()
    expect(wrapper.emitted('manageMembership')).toHaveLength(1)
    expect(wrapper.emitted('manageMembership')![0][0]).toMatchObject({
      id: 'member-1',
      firstName: 'Ana',
      lastName: 'García',
    })
  })
})

describe('FamilyMemberList — data completeness warnings', () => {
  it('shows warning icon on card for adult member missing DNI and email', () => {
    const wrapper = mount(FamilyMemberList, {
      props: { members: [mockMember], loading: false },
      global: globalConfig,
    })
    expect(wrapper.find('[data-testid="member-warning-icon"]').exists()).toBe(true)
  })

  it('does not show warning icon for adult member with complete data', () => {
    const wrapper = mount(FamilyMemberList, {
      props: { members: [completeMember], loading: false },
      global: globalConfig,
    })
    expect(wrapper.find('[data-testid="member-warning-icon"]').exists()).toBe(false)
  })

  it('does not show warning icon for minor member missing DNI and email', () => {
    const wrapper = mount(FamilyMemberList, {
      props: { members: [minorMember], loading: false },
      global: globalConfig,
    })
    expect(wrapper.find('[data-testid="member-warning-icon"]').exists()).toBe(false)
  })

  it('shows warning banner when any member has incomplete data', () => {
    const wrapper = mount(FamilyMemberList, {
      props: { members: [mockMember], loading: false },
      global: globalConfig,
    })
    expect(wrapper.find('[data-testid="member-warnings-banner"]').exists()).toBe(true)
  })

  it('does not show warning banner when all members have complete data', () => {
    const wrapper = mount(FamilyMemberList, {
      props: { members: [completeMember], loading: false },
      global: globalConfig,
    })
    expect(wrapper.find('[data-testid="member-warnings-banner"]').exists()).toBe(false)
  })

  it('does not show warning banner in readOnly mode', () => {
    const wrapper = mount(FamilyMemberList, {
      props: { members: [mockMember], loading: false, readOnly: true },
      global: globalConfig,
    })
    expect(wrapper.find('[data-testid="member-warnings-banner"]').exists()).toBe(false)
  })
})
