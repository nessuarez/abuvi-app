const EDITION_ID = 'edition-2026'

const mockCreatedExtra = {
  success: true,
  data: {
    id: 'extra-new',
    campEditionId: EDITION_ID,
    name: 'Seguro de viaje',
    description: 'Seguro opcional',
    price: 30,
    pricingType: 'PerFamily',
    pricingPeriod: 'OneTime',
    isRequired: false,
    maxQuantity: null,
    currentQuantitySold: 0,
    isActive: true,
    createdAt: '2026-02-27T00:00:00Z',
    updatedAt: '2026-02-27T00:00:00Z'
  },
  error: null
}

const mockDeactivatedExtra = {
  success: true,
  data: {
    id: 'extra-1',
    campEditionId: EDITION_ID,
    name: 'Camiseta del campamento',
    description: 'Camiseta oficial con logo',
    price: 15,
    pricingType: 'PerPerson',
    pricingPeriod: 'OneTime',
    isRequired: false,
    maxQuantity: 100,
    currentQuantitySold: 5,
    isActive: false,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-02-27T00:00:00Z'
  },
  error: null
}

describe('Camp Edition Extras — Board management', () => {
  beforeEach(() => {
    cy.login('board@abuvi.org', 'password123')
    cy.intercept('GET', `/api/camps/editions/${EDITION_ID}`, {
      fixture: 'camp-edition-open.json'
    }).as('getEdition')
    cy.intercept('GET', `/api/camps/editions/${EDITION_ID}/extras*`, {
      fixture: 'edition-extras.json'
    }).as('getExtras')
    cy.intercept('GET', `/api/camps/editions/${EDITION_ID}/accommodations*`, {
      body: { success: true, data: [], error: null }
    }).as('getAccommodations')
    cy.visit(`/camps/editions/${EDITION_ID}`)
    cy.wait('@getEdition')
    cy.wait('@getExtras')
  })

  it('should display extras section with extras table', () => {
    cy.get('[data-testid="edition-extras-section"]').should('be.visible')
    cy.get('[data-testid="extras-table"]').should('be.visible')
    cy.contains('Camiseta del campamento').should('be.visible')
    cy.contains('Menú vegetariano').should('be.visible')
  })

  it('should show add extra button for board user', () => {
    cy.get('[data-testid="add-extra-button"]').should('be.visible')
  })

  it('should show action buttons for board user', () => {
    cy.get('[data-testid="edit-extra-button-extra-1"]').should('be.visible')
    cy.get('[data-testid="delete-extra-button-extra-1"]').should('be.visible')
    cy.get('[data-testid="toggle-active-button-extra-1"]').should('be.visible')
  })

  it('should open create dialog when clicking add extra', () => {
    cy.get('[data-testid="add-extra-button"]').click()
    cy.get('[data-testid="extra-form-dialog"]').should('be.visible')
    cy.contains('Nuevo extra').should('be.visible')
  })

  it('should create extra and show success toast', () => {
    cy.intercept('POST', `/api/camps/editions/${EDITION_ID}/extras`, {
      statusCode: 201,
      body: mockCreatedExtra
    }).as('createExtra')
    cy.intercept('GET', `/api/camps/editions/${EDITION_ID}/extras*`, {
      fixture: 'edition-extras.json'
    }).as('refreshExtras')

    cy.get('[data-testid="add-extra-button"]').click()
    cy.get('[data-testid="extra-name-input"]').type('Seguro de viaje')
    cy.get('[data-testid="extra-submit-button"]').click()
    cy.wait('@createExtra')
    cy.contains('Extra creado').should('be.visible')
  })

  it('should open edit dialog with pre-filled values when clicking edit', () => {
    cy.get('[data-testid="edit-extra-button-extra-1"]').click()
    cy.get('[data-testid="extra-form-dialog"]').should('be.visible')
    cy.contains('Editar extra').should('be.visible')
    cy.get('[data-testid="extra-name-input"]').should('have.value', 'Camiseta del campamento')
  })

  it('should show confirmation dialog before deleting', () => {
    cy.get('[data-testid="delete-extra-button-extra-1"]').click()
    cy.contains('Eliminar extra').should('be.visible')
    cy.contains('¿Estás seguro').should('be.visible')
  })

  it('should delete extra and show success toast on confirmation', () => {
    cy.intercept('DELETE', '/api/camps/editions/extras/extra-1', {
      statusCode: 204,
      body: ''
    }).as('deleteExtra')
    cy.intercept('GET', `/api/camps/editions/${EDITION_ID}/extras*`, {
      body: { success: true, data: [], error: null }
    }).as('refreshExtras')

    cy.get('[data-testid="delete-extra-button-extra-1"]').click()
    cy.contains('Eliminar extra').should('be.visible')
    cy.contains('button', 'Eliminar').last().click()
    cy.wait('@deleteExtra')
    cy.contains('Extra eliminado').should('be.visible')
  })

  it('should deactivate extra when clicking toggle active button', () => {
    cy.intercept('PATCH', '/api/camps/editions/extras/extra-1/deactivate', {
      body: mockDeactivatedExtra
    }).as('deactivateExtra')

    cy.get('[data-testid="toggle-active-button-extra-1"]').click()
    cy.wait('@deactivateExtra')
    cy.contains('Extra desactivado').should('be.visible')
  })
})

describe('Camp Edition Extras — Member view', () => {
  beforeEach(() => {
    cy.login('member@abuvi.org', 'password123')
    cy.intercept('GET', `/api/camps/editions/${EDITION_ID}`, {
      fixture: 'camp-edition-open.json'
    }).as('getEdition')
    cy.intercept('GET', `/api/camps/editions/${EDITION_ID}/extras*`, {
      fixture: 'edition-extras.json'
    }).as('getExtras')
    cy.visit(`/camps/editions/${EDITION_ID}`)
    cy.wait('@getEdition')
    cy.wait('@getExtras')
  })

  it('should display extras section', () => {
    cy.get('[data-testid="edition-extras-section"]').should('be.visible')
    cy.contains('Camiseta del campamento').should('be.visible')
  })

  it('should not show add extra button for member user', () => {
    cy.get('[data-testid="add-extra-button"]').should('not.exist')
  })

  it('should not show action buttons for member user', () => {
    cy.get('[data-testid="edit-extra-button-extra-1"]').should('not.exist')
    cy.get('[data-testid="delete-extra-button-extra-1"]').should('not.exist')
    cy.get('[data-testid="toggle-active-button-extra-1"]').should('not.exist')
  })
})

describe('Camp Edition Extras — Empty state', () => {
  beforeEach(() => {
    cy.login('board@abuvi.org', 'password123')
    cy.intercept('GET', `/api/camps/editions/${EDITION_ID}`, {
      fixture: 'camp-edition-open.json'
    }).as('getEdition')
    cy.intercept('GET', `/api/camps/editions/${EDITION_ID}/extras*`, {
      body: { success: true, data: [], error: null }
    }).as('getExtras')
    cy.intercept('GET', `/api/camps/editions/${EDITION_ID}/accommodations*`, {
      body: { success: true, data: [], error: null }
    }).as('getAccommodations')
    cy.visit(`/camps/editions/${EDITION_ID}`)
    cy.wait('@getEdition')
    cy.wait('@getExtras')
  })

  it('should show empty state when no extras exist', () => {
    cy.get('[data-testid="empty-extras-state"]').should('be.visible')
    cy.contains('No hay extras configurados').should('be.visible')
  })

  it('should show add first extra button in empty state', () => {
    cy.contains('Añadir el primero').should('be.visible')
  })
})
