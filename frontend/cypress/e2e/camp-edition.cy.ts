describe('Camp Edition Page (/camp)', () => {
  beforeEach(() => { cy.login('member@abuvi.org', 'password123') })

  it('shows loading state while fetching', () => {
    cy.intercept('GET', '/api/camps/current', (req) => { req.reply({ delay: 2000 }) })
    cy.visit('/camp')
    cy.get('[data-testid="camp-loading"]').should('be.visible')
  })

  it('displays current year open camp edition with title and status', () => {
    cy.intercept('GET', '/api/camps/current', { fixture: 'camp-edition-open.json' }).as('getCurrent')
    cy.visit('/camp')
    cy.wait('@getCurrent')
    cy.get('h1').should('contain.text', 'Campamento 2026')
  })

  it('shows warning when displaying previous year camp', () => {
    cy.intercept('GET', '/api/camps/current', { fixture: 'camp-edition-2025.json' })
    cy.visit('/camp')
    cy.contains('Mostrando información del campamento de 2025').should('be.visible')
  })

  it('shows info message when no camp edition exists (404)', () => {
    cy.intercept('GET', '/api/camps/current', {
      statusCode: 404,
      body: { success: false, data: null, error: { message: 'Not found', code: 'NOT_FOUND' } }
    })
    cy.visit('/camp')
    cy.get('[data-testid="camp-empty"]').should('be.visible')
    cy.contains('No hay información de campamento disponible').should('be.visible')
  })

  it('shows registration CTA when status is Open', () => {
    cy.intercept('GET', '/api/camps/current', { fixture: 'camp-edition-open.json' })
    cy.visit('/camp')
    cy.contains('Inscripciones Abiertas').should('be.visible')
  })
})
