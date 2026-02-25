import { describe, it, expect, beforeAll } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import Tooltip from 'primevue/tooltip'
import FamilyMemberList from '../FamilyMemberList.vue'
import { FamilyRelationship, type FamilyMemberResponse } from '@/types/family-unit'

// jsdom does not have ResizeObserver or IntersectionObserver — mock them for PrimeVue DataTable
beforeAll(() => {
  global.ResizeObserver = class ResizeObserver {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
  global.IntersectionObserver = class IntersectionObserver {
    observe() {}
    unobserve() {}
    disconnect() {}
    takeRecords() {
      return []
    }
  } as unknown as typeof IntersectionObserver
})

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
  userId: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
}

const globalConfig = {
  plugins: [[PrimeVue, { unstyled: true }]] as [unknown],
  directives: { tooltip: Tooltip },
}

describe('FamilyMemberList — manageMembership', () => {
  it('renders manageMembership button when canManageMemberships is true', () => {
    const wrapper = mount(FamilyMemberList, {
      props: { members: [mockMember], loading: false, canManageMemberships: true },
      global: globalConfig,
    })
    expect(wrapper.find('[data-testid="manage-membership-btn-member-1"]').exists()).toBe(true)
  })

  it('does not render manageMembership button when canManageMemberships is false', () => {
    const wrapper = mount(FamilyMemberList, {
      props: { members: [mockMember], loading: false, canManageMemberships: false },
      global: globalConfig,
    })
    expect(wrapper.find('[data-testid="manage-membership-btn-member-1"]').exists()).toBe(false)
  })

  it('does not render manageMembership button when canManageMemberships is omitted', () => {
    const wrapper = mount(FamilyMemberList, {
      props: { members: [mockMember], loading: false },
      global: globalConfig,
    })
    expect(wrapper.find('[data-testid="manage-membership-btn-member-1"]').exists()).toBe(false)
  })

  it('emits manageMembership with the correct member when button is clicked', async () => {
    const wrapper = mount(FamilyMemberList, {
      props: { members: [mockMember], loading: false, canManageMemberships: true },
      global: globalConfig,
    })
    const btn = wrapper.find('[data-testid="manage-membership-btn-member-1"]')
    await btn.trigger('click')
    expect(wrapper.emitted('manageMembership')).toHaveLength(1)
    expect(wrapper.emitted('manageMembership')![0][0]).toMatchObject({
      id: 'member-1',
      firstName: 'Ana',
      lastName: 'García',
    })
  })
})
