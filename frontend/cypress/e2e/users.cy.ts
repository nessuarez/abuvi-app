describe('User Management', () => {
  beforeEach(() => {
    cy.visit('/users')
  })

  it('should display users list', () => {
    cy.get('[data-testid="users-table"]').should('exist')
    cy.contains('User Management').should('be.visible')
  })

  it('should open create user dialog', () => {
    cy.contains('button', 'Create User').click()
    cy.contains('Create New User').should('be.visible')
    cy.get('#email').should('be.visible')
    cy.get('#password').should('be.visible')
  })

  it('should create a new user successfully', () => {
    cy.contains('button', 'Create User').click()

    // Fill form
    cy.get('#email').type('newuser@example.com')
    cy.get('#password').type('Password123!')
    cy.get('#firstName').type('New')
    cy.get('#lastName').type('User')
    cy.get('#phone').type('+34 111 222 333')

    // Submit
    cy.contains('button', 'Create User').click()

    // Verify success
    cy.contains('newuser@example.com').should('be.visible')
  })

  it('should show validation errors for invalid data', () => {
    cy.contains('button', 'Create User').click()

    // Try to submit empty form
    cy.contains('button', 'Create User').should('be.disabled')

    // Enter invalid email
    cy.get('#email').type('invalid-email')
    cy.get('#password').type('short')

    // Check validation messages
    cy.contains('Email must be valid').should('be.visible')
    cy.contains('Password must be at least 8 characters').should('be.visible')
  })

  it('should navigate to user detail page', () => {
    // Click view button on first user
    cy.get('[aria-label="View Details"]').first().click()

    // Verify detail page
    cy.url().should('include', '/users/')
    cy.contains('User Details').should('be.visible')
  })

  it('should edit user successfully', () => {
    // Navigate to detail page
    cy.get('[aria-label="View Details"]').first().click()

    // Click edit button
    cy.contains('button', 'Edit').click()

    // Modify first name
    cy.get('#firstName').clear().type('Updated')

    // Submit
    cy.contains('button', 'Update User').click()

    // Verify update
    cy.contains('Updated').should('be.visible')
    cy.contains('button', 'Edit').should('be.visible')
  })

  it('should cancel edit and return to view mode', () => {
    cy.get('[aria-label="View Details"]').first().click()
    cy.contains('button', 'Edit').click()

    // Cancel edit
    cy.contains('button', 'Cancel').click()

    // Should be back in view mode
    cy.contains('button', 'Edit').should('be.visible')
  })

  it('should navigate back to users list', () => {
    cy.get('[aria-label="View Details"]').first().click()

    cy.contains('button', 'Back to Users').click()

    cy.url().should('equal', Cypress.config().baseUrl + '/users')
  })
})
