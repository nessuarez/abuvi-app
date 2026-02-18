describe('Camp Editions — Member view (/camp)', () => {
  beforeEach(() => {
    cy.login('member@abuvi.org', 'password123')
    cy.intercept('GET', '/api/camps/editions/active', { fixture: 'active-edition.json' }).as('getActive')
    cy.visit('/camp')
    cy.wait('@getActive')
  })

  it('should display active edition card when an open edition exists', () => {
    cy.get('[data-testid="active-edition-card"]').should('be.visible')
    cy.get('[data-testid="active-edition-card"]').should('contain.text', 'Campamento 2026')
  })

  it('should show status badge on active edition card', () => {
    cy.get('[data-testid="active-edition-card"]').within(() => {
      cy.get('[data-testid="status-badge"]').should('be.visible').and('contain.text', 'Abierto')
    })
  })

  it('should show empty state when no active edition exists', () => {
    cy.intercept('GET', '/api/camps/editions/active', {
      body: { success: true, data: null, error: null }
    }).as('getActiveNull')
    cy.visit('/camp')
    cy.wait('@getActiveNull')
    cy.contains('No hay ningún campamento abierto para este año.').should('be.visible')
  })

  it('should show loading state while fetching', () => {
    cy.intercept('GET', '/api/camps/editions/active', (req) => { req.reply({ delay: 2000 }) })
    cy.visit('/camp')
    cy.get('[data-testid="camp-loading"]').should('be.visible')
  })
})

describe('Camp Editions — Board management (/camps/editions)', () => {
  beforeEach(() => {
    cy.login('board@abuvi.org', 'password123')
    cy.intercept('GET', '/api/camps/editions*', { fixture: 'editions-list.json' }).as('getEditions')
    cy.intercept('GET', '/api/camps', { body: { success: true, data: [], error: null } }).as('getCamps')
    cy.visit('/camps/editions')
    cy.wait('@getEditions')
  })

  it('should display editions list with status badges', () => {
    cy.get('[data-testid="editions-table"]').should('be.visible')
    cy.get('[data-testid="status-badge"]').should('have.length.greaterThan', 0)
  })

  it('should show change-status button disabled for Completed editions', () => {
    cy.get('[data-testid="change-status-btn"]').then(($btns) => {
      const completedIndex = 1
      cy.wrap($btns.eq(completedIndex)).should('be.disabled')
    })
  })

  it('should open status dialog when clicking change status', () => {
    cy.get('[data-testid="change-status-btn"]').first().click()
    cy.get('[data-testid="status-dialog"]').should('be.visible')
  })

  it('should change status and refresh list on confirm', () => {
    cy.intercept('PATCH', '/api/camps/editions/*/status', { fixture: 'edition-updated.json' }).as('patchStatus')
    cy.get('[data-testid="change-status-btn"]').first().click()
    cy.get('[data-testid="confirm-status-btn"]').click()
    cy.wait('@patchStatus')
    cy.contains('Estado actualizado correctamente').should('be.visible')
  })

  it('should open edit dialog when clicking edit', () => {
    cy.get('[data-testid="edit-edition-btn"]').first().click()
    cy.get('[data-testid="edition-dialog"]').should('be.visible')
  })

  it('should save changes and show success toast after editing', () => {
    cy.intercept('PUT', '/api/camps/editions/*', { fixture: 'edition-updated.json' }).as('putEdition')
    cy.get('[data-testid="edit-edition-btn"]').first().click()
    cy.get('[data-testid="edition-dialog"]').should('be.visible')
    cy.get('[data-testid="save-edition-btn"]').click()
    cy.wait('@putEdition')
    cy.contains('Edición actualizada correctamente').should('be.visible')
  })

  it('should redirect Member to /home when visiting /camps/editions', () => {
    cy.login('member@abuvi.org', 'password123')
    cy.visit('/camps/editions')
    cy.url().should('include', '/home')
  })
})
