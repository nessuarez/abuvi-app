/**
 * E2E tests: Admin accommodation needs tagging section in registration detail page.
 * All API calls are intercepted with cy.intercept() — no real backend required.
 */

const REG_ID = 'reg-test-1'
const EDITION_ID = 'edition-2026'
const BASE_URL = `/registrations/${REG_ID}`

const mockRegistrationAdmin = {
  id: REG_ID,
  familyUnit: { id: 'fu-1', name: 'García Family', representativeUserId: 'user-1' },
  campEdition: {
    id: EDITION_ID,
    campName: 'Campamento ABUVI',
    year: 2026,
    startDate: '2026-07-01',
    endDate: '2026-07-15',
    location: 'Montaña Norte',
    duration: 14,
  },
  status: 'Confirmed',
  notes: null,
  pricing: { members: [], baseTotalAmount: 0, extras: [], extrasAmount: 0, totalAmount: 0 },
  payments: [],
  amountPaid: 0,
  amountRemaining: 0,
  createdAt: '2026-03-01T00:00:00Z',
  updatedAt: '2026-03-01T00:00:00Z',
  specialNeeds: 'Sin gluten',
  campatesPreference: 'Familia Martínez',
  hasPet: false,
  draftTargetStatus: null,
  hasPendingUserAcknowledgement: false,
  statusHistory: [],
  accommodationInternalNotes: null,
  accommodationNeeds: [],
  friendLinks: [],
}

const mockRegistrationMember = {
  ...mockRegistrationAdmin,
  // Admin-only fields absent for Member
  accommodationInternalNotes: undefined,
  accommodationNeeds: undefined,
  friendLinks: undefined,
}

function stubCommonApis(registrationData = mockRegistrationAdmin) {
  cy.intercept('GET', `/api/registrations/${REG_ID}`, {
    statusCode: 200,
    body: { success: true, data: registrationData },
  }).as('getRegistration')

  cy.intercept('GET', `/api/registrations/${REG_ID}/accommodation-preferences`, {
    statusCode: 200,
    body: { success: true, data: [] },
  })

  cy.intercept('GET', `/api/registrations/${REG_ID}/payments`, {
    statusCode: 200,
    body: { success: true, data: [] },
  })

  cy.intercept('GET', `/api/payment-settings`, {
    statusCode: 200,
    body: { success: true, data: { iban: 'ES00', bankName: 'Test Bank', accountHolder: 'Test' } },
  })

  cy.intercept('GET', '/api/accommodation-features*', {
    statusCode: 200,
    body: { success: true, data: [] },
  }).as('getFeatures')
}

function loginAsAdmin() {
  // Set auth store state via localStorage (matches the auth store implementation)
  window.localStorage.setItem('auth', JSON.stringify({
    token: 'fake-admin-token',
    user: { id: 'user-admin-1', email: 'admin@test.com', firstName: 'Admin', lastName: 'User', role: 'Admin' },
  }))
}

function loginAsMember() {
  window.localStorage.setItem('auth', JSON.stringify({
    token: 'fake-member-token',
    user: { id: 'user-1', email: 'member@test.com', firstName: 'Ana', lastName: 'García', role: 'Member' },
  }))
}

describe('Admin: Accommodation Needs Tagging Section', () => {
  beforeEach(() => {
    cy.clearLocalStorage()
  })

  it('Flow 1 — Admin sees the "Alojamiento (Junta)" section', () => {
    stubCommonApis()
    cy.visit(BASE_URL, {
      onBeforeLoad(win) {
        win.localStorage.setItem('auth', JSON.stringify({
          token: 'fake-admin-token',
          user: { id: 'user-admin-1', email: 'admin@test.com', firstName: 'Admin', lastName: 'User', role: 'Admin' },
        }))
      },
    })
    cy.wait('@getRegistration')

    cy.get('[data-testid="accommodation-needs-section"]').should('be.visible')
    cy.contains('Alojamiento (Junta)').should('be.visible')
    cy.contains('Sin gluten').should('be.visible')
    cy.contains('Familia Martínez').should('be.visible')
  })

  it('Flow 2 — Admin tags accommodation features, saves, and sees chips', () => {
    cy.fixture('accommodation-needs').then((data) => {
      stubCommonApis({
        ...mockRegistrationAdmin,
        accommodationNeeds: [],
      })

      cy.intercept('GET', '/api/accommodation-features*', {
        statusCode: 200,
        body: { success: true, data: data.features },
      }).as('getFeatures')

      cy.intercept('PUT', `/api/registrations/${REG_ID}/accommodation-needs`, {
        statusCode: 200,
        body: {
          success: true,
          data: { registrationId: REG_ID, needs: data.needs.slice(0, 1) },
        },
      }).as('saveNeeds')

      cy.visit(BASE_URL, {
        onBeforeLoad(win) {
          win.localStorage.setItem('auth', JSON.stringify({
            token: 'fake-admin-token',
            user: { id: 'user-admin-1', email: 'admin@test.com', firstName: 'Admin', lastName: 'User', role: 'Admin' },
          }))
        },
      })
      cy.wait('@getRegistration')

      cy.get('[data-testid="edit-tags-btn"]').click()
      cy.get('[data-testid="features-multiselect"]').should('be.visible')
      cy.get('[data-testid="save-tags-btn"]').click()
      cy.wait('@saveNeeds')

      cy.contains('Habitación privada').should('be.visible')
    })
  })

  it('Flow 3 — Admin edits internal notes and saves', () => {
    stubCommonApis()

    cy.intercept('PATCH', `/api/registrations/${REG_ID}/accommodation-notes`, {
      statusCode: 200,
      body: {
        success: true,
        data: {
          registrationId: REG_ID,
          accommodationInternalNotes: 'Familia necesita planta baja',
          updatedAt: '2026-05-01T10:00:00Z',
        },
      },
    }).as('saveNotes')

    cy.visit(BASE_URL, {
      onBeforeLoad(win) {
        win.localStorage.setItem('auth', JSON.stringify({
          token: 'fake-admin-token',
          user: { id: 'user-admin-1', email: 'admin@test.com', firstName: 'Admin', lastName: 'User', role: 'Admin' },
        }))
      },
    })
    cy.wait('@getRegistration')

    cy.get('[data-testid="edit-notes-btn"]').click()
    cy.get('[data-testid="notes-textarea"]').type('Familia necesita planta baja')
    cy.get('[data-testid="save-notes-btn"]').click()
    cy.wait('@saveNotes')

    cy.contains('Familia necesita planta baja').should('be.visible')
  })

  it('Flow 4 — Admin links a friend family and sees it listed', () => {
    cy.fixture('friend-links').then((data) => {
      stubCommonApis()

      cy.intercept('GET', `/api/camp-editions/${EDITION_ID}/registrations*`, {
        statusCode: 200,
        body: { success: true, data: data.editionRegistrations },
      }).as('getEditionRegs')

      cy.intercept('PUT', `/api/registrations/${REG_ID}/friend-links`, {
        statusCode: 200,
        body: {
          success: true,
          data: { registrationId: REG_ID, friendLinks: data.friendLinks },
        },
      }).as('saveFriendLinks')

      cy.visit(BASE_URL, {
        onBeforeLoad(win) {
          win.localStorage.setItem('auth', JSON.stringify({
            token: 'fake-admin-token',
            user: { id: 'user-admin-1', email: 'admin@test.com', firstName: 'Admin', lastName: 'User', role: 'Admin' },
          }))
        },
      })
      cy.wait('@getRegistration')

      cy.get('[data-testid="edit-friend-links-btn"]').click()
      cy.wait('@getEditionRegs')
      cy.get('[data-testid="friend-links-multiselect"]').should('be.visible')
      cy.get('[data-testid="save-friend-links-btn"]').click()
      cy.wait('@saveFriendLinks')

      cy.contains('Martínez Family').should('be.visible')
    })
  })

  it('Flow 5 — Member does NOT see the admin tagging section', () => {
    cy.intercept('GET', `/api/registrations/${REG_ID}`, {
      statusCode: 200,
      body: { success: true, data: mockRegistrationMember },
    }).as('getRegistrationMember')

    cy.intercept('GET', `/api/registrations/${REG_ID}/accommodation-preferences`, {
      statusCode: 200,
      body: { success: true, data: [] },
    })
    cy.intercept('GET', `/api/registrations/${REG_ID}/payments`, {
      statusCode: 200,
      body: { success: true, data: [] },
    })
    cy.intercept('GET', '/api/payment-settings', {
      statusCode: 200,
      body: { success: true, data: { iban: 'ES00', bankName: 'Test Bank', accountHolder: 'Test' } },
    })

    cy.visit(BASE_URL, {
      onBeforeLoad(win) {
        win.localStorage.setItem('auth', JSON.stringify({
          token: 'fake-member-token',
          user: { id: 'user-1', email: 'member@test.com', firstName: 'Ana', lastName: 'García', role: 'Member' },
        }))
      },
    })
    cy.wait('@getRegistrationMember')

    cy.get('[data-testid="accommodation-needs-section"]').should('not.exist')
    cy.get('[data-testid="friend-links-section"]').should('not.exist')
  })

  it('Flow 6 — API 400 error on save shows error toast', () => {
    stubCommonApis()

    cy.intercept('PUT', `/api/registrations/${REG_ID}/accommodation-needs`, {
      statusCode: 400,
      body: {
        success: false,
        error: { code: 'VALIDATION_ERROR', message: 'IDs de características no encontrados' },
      },
    }).as('saveNeedsError')

    cy.visit(BASE_URL, {
      onBeforeLoad(win) {
        win.localStorage.setItem('auth', JSON.stringify({
          token: 'fake-admin-token',
          user: { id: 'user-admin-1', email: 'admin@test.com', firstName: 'Admin', lastName: 'User', role: 'Admin' },
        }))
      },
    })
    cy.wait('@getRegistration')

    cy.get('[data-testid="edit-tags-btn"]').click()
    cy.get('[data-testid="save-tags-btn"]').click()
    cy.wait('@saveNeedsError')

    // PrimeVue toast appears
    cy.get('.p-toast').should('be.visible')
    cy.get('.p-toast').should('contain.text', 'Error')
  })
})
