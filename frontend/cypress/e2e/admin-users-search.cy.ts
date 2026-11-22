describe('Admin Users - server-side search (/admin/users)', () => {
  // Search by the board user's own email (always present in the seeded data)
  const searchTerm = () => String(Cypress.env('BOARD_EMAIL')).split('@')[0]

  beforeEach(() => {
    cy.login('board')
    cy.intercept('GET', '/api/users*').as('getUsers')
    cy.visit('/admin/users')
    cy.wait('@getUsers')
  })

  it('sends the search term to the server and resets to page 1', () => {
    cy.get('[data-testid="users-search-input"]').type(searchTerm())

    cy.wait('@getUsers').then(({ request }) => {
      expect(request.query.search).to.equal(searchTerm())
      expect(request.query.page).to.equal('1')
    })
    cy.get('[data-testid="users-table"]').should('contain.text', searchTerm())
  })

  it('omits the search param after clearing the search box', () => {
    cy.get('[data-testid="users-search-input"]').type(searchTerm())
    cy.wait('@getUsers')

    cy.get('[data-testid="users-search-input"]').clear()

    cy.wait('@getUsers').then(({ request }) => {
      expect(request.query).to.not.have.property('search')
    })
    cy.get('[data-testid="users-total"]').should('contain.text', 'usuarios')
  })
})
