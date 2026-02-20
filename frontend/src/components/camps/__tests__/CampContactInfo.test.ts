import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import CampContactInfo from '../CampContactInfo.vue'
import type { CampDetailResponse } from '@/types/camp'

vi.mock('primevue/button', () => ({
  default: {
    name: 'Button',
    props: ['label', 'icon', 'outlined', 'size', 'as', 'href', 'target', 'rel'],
    template: '<a :href="href" :target="target" :rel="rel">{{ label }}</a>'
  }
}))

const makeCamp = (overrides: Partial<CampDetailResponse> = {}): CampDetailResponse => ({
  id: '1',
  name: 'Test Camp',
  description: null,
  location: null,
  latitude: 42.0,
  longitude: 2.7,
  googlePlaceId: 'ChIJ123',
  formattedAddress: 'Crta Pujarnol, km 5, Girona',
  streetAddress: 'Crta Pujarnol, km 5',
  locality: 'Pujarnol',
  administrativeArea: 'Girona',
  postalCode: '17834',
  country: 'España',
  phoneNumber: '+34 972 59 05 07',
  nationalPhoneNumber: '972 59 05 07',
  websiteUrl: 'http://www.example.com/',
  googleMapsUrl: 'https://maps.google.com/?cid=123',
  googleRating: 4.2,
  googleRatingCount: 113,
  businessStatus: 'OPERATIONAL',
  placeTypes: '["campground"]',
  lastGoogleSyncAt: '2026-01-15T00:00:00Z',
  pricePerAdult: 100,
  pricePerChild: 80,
  pricePerBaby: 0,
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  photos: [],
  ...overrides
})

describe('CampContactInfo', () => {
  it('renders nothing when all contact fields are null', () => {
    const wrapper = mount(CampContactInfo, {
      props: {
        camp: makeCamp({
          formattedAddress: null,
          phoneNumber: null,
          websiteUrl: null,
          googleMapsUrl: null,
          googleRating: null
        })
      }
    })
    expect(wrapper.find('div').exists()).toBe(false)
  })

  it('renders formatted address when present', () => {
    const wrapper = mount(CampContactInfo, { props: { camp: makeCamp() } })
    expect(wrapper.text()).toContain('Crta Pujarnol, km 5, Girona')
  })

  it('renders phone as tel: link using national number', () => {
    const wrapper = mount(CampContactInfo, { props: { camp: makeCamp() } })
    const link = wrapper.find('a[href="tel:+34 972 59 05 07"]')
    expect(link.exists()).toBe(true)
    expect(link.text()).toContain('972 59 05 07')
  })

  it('renders website as external link', () => {
    const wrapper = mount(CampContactInfo, { props: { camp: makeCamp() } })
    const link = wrapper.find('a[href="http://www.example.com/"]')
    expect(link.exists()).toBe(true)
    expect(link.attributes('target')).toBe('_blank')
    expect(link.attributes('rel')).toContain('noopener')
  })

  it('renders Google rating with review count', () => {
    const wrapper = mount(CampContactInfo, { props: { camp: makeCamp() } })
    expect(wrapper.text()).toContain('4.2')
    expect(wrapper.text()).toContain('113')
  })

  it('renders OPERATIONAL status as "Operativo"', () => {
    const wrapper = mount(CampContactInfo, { props: { camp: makeCamp() } })
    expect(wrapper.text()).toContain('Operativo')
  })

  it('renders CLOSED_TEMPORARILY as "Cerrado temporalmente"', () => {
    const wrapper = mount(CampContactInfo, {
      props: { camp: makeCamp({ businessStatus: 'CLOSED_TEMPORARILY' }) }
    })
    expect(wrapper.text()).toContain('Cerrado temporalmente')
  })

  it('renders Google Maps link when address present', () => {
    const wrapper = mount(CampContactInfo, { props: { camp: makeCamp() } })
    expect(wrapper.find('a[href="https://maps.google.com/?cid=123"]').exists()).toBe(true)
  })

  it('renders last sync date when present', () => {
    const wrapper = mount(CampContactInfo, { props: { camp: makeCamp() } })
    expect(wrapper.text()).toContain('15') // day from lastGoogleSyncAt
  })

  it('renders correctly with only phone number (partial data)', () => {
    const wrapper = mount(CampContactInfo, {
      props: {
        camp: makeCamp({
          formattedAddress: null,
          websiteUrl: null,
          googleMapsUrl: null,
          googleRating: null
        })
      }
    })
    expect(wrapper.exists()).toBe(true)
    expect(wrapper.text()).toContain('972 59 05 07')
  })
})
