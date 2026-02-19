describe('Camp Extended Information', () => {
  beforeEach(() => {
    cy.login('board@example.com', 'password') // Use existing login command
  })

  context('Camp detail page — with Google Places data', () => {
    it('displays contact information section for Google-enriched camp', () => {
      cy.visit('/camp-locations')
      cy.get('[data-testid="view-camp-detail-btn"]').first().click()

      cy.contains('Información de contacto').should('be.visible')
    })

    it('renders phone as clickable tel: link', () => {
      cy.visit('/camp-locations')
      cy.get('[data-testid="view-camp-detail-btn"]').first().click()

      cy.get('a[href^="tel:"]').should('exist')
    })

    it('renders website as external link with _blank target', () => {
      cy.visit('/camp-locations')
      cy.get('[data-testid="view-camp-detail-btn"]').first().click()

      cy.get('a[target="_blank"]').should('exist')
    })

    it('renders Google rating with star icon', () => {
      cy.visit('/camp-locations')
      cy.get('[data-testid="view-camp-detail-btn"]').first().click()

      cy.get('.pi-star-fill').should('exist')
      cy.contains('valoraciones en Google').should('be.visible')
    })

    it('renders photo gallery when camp has photos', () => {
      cy.visit('/camp-locations')
      cy.get('[data-testid="view-camp-detail-btn"]').first().click()

      cy.contains('Fotos').should('be.visible')
      cy.get('img[src*="places/photo"]').should('have.length.greaterThan', 0)
    })

    it('shows Google Maps attribution in photo gallery', () => {
      cy.visit('/camp-locations')
      cy.get('[data-testid="view-camp-detail-btn"]').first().click()

      cy.contains('Google Maps').should('be.visible')
    })
  })

  context('Camp detail page — without Google Places data (graceful degradation)', () => {
    it('does not show contact info section for manually-created camps', () => {
      cy.visit('/camp-locations')
      cy.contains('Nuevo Campamento').click()
      cy.get('[data-testid="camp-name-input"]').type('Manual Test Camp')
      cy.contains('Guardar').click()

      cy.contains('Manual Test Camp').click()

      cy.contains('Información de contacto').should('not.exist')
      cy.contains('Fotos').should('not.exist')
    })
  })

  context('Camp cards list — rating badge', () => {
    it('shows rating badge on cards for Google-enriched camps', () => {
      cy.visit('/camp-locations')
      cy.get('button[aria-label="Vista de tarjetas"]').click()

      cy.get('.pi-star-fill').should('exist')
    })
  })
})
