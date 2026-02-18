describe('Profile Page - Family Unit Section', () => {
  it('shows create button when user has no family unit', () => {
    cy.intercept('GET', '/api/family-units/me', { statusCode: 404 })
    cy.login('member@abuvi.org', 'password123')
    cy.visit('/profile')
    cy.get('[data-testid="create-family-unit-btn"]').should('be.visible')
  })

  it('shows manage button when user has a family unit', () => {
    cy.intercept('GET', '/api/family-units/me', { fixture: 'family-unit.json' })
    cy.login('member@abuvi.org', 'password123')
    cy.visit('/profile')
    cy.get('[data-testid="manage-family-unit-btn"]').should('be.visible')
  })

  it('clicking Gestionar navigates to /family-unit', () => {
    cy.intercept('GET', '/api/family-units/me', { fixture: 'family-unit.json' })
    cy.login('member@abuvi.org', 'password123')
    cy.visit('/profile')
    cy.get('[data-testid="manage-family-unit-btn"]').click()
    cy.url().should('include', '/family-unit')
  })
})
