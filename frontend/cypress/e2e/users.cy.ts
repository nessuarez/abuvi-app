describe('User Management', () => {
  beforeEach(() => {
    cy.visit('/users')
  })

  describe('Visual Elements & Icons', () => {
    it('should load PrimeIcons stylesheet', () => {
      cy.verifyPrimeIconsLoaded()
    })

    it('should display icon in "Create User" button', () => {
      cy.contains('button', 'Create User').verifyIcon('pi-plus')
    })

    it('should display eye icons in "View Details" buttons', () => {
      // Wait for table to load
      cy.get('[data-testid="users-table"]').should('be.visible')
      cy.wait(1000)

      // Verify first view button has the eye icon properly styled
      cy.verifyButtonWithIcon('[data-testid="view-user-button"]', 'pi-eye')

      // Verify all view buttons have icons
      cy.get('[data-testid="view-user-button"]').each(($btn) => {
        cy.wrap($btn).verifyIcon('pi-eye')
      })
    })

    it('should display icons with proper font-face', () => {
      // Verify that all pi icons use the correct font
      cy.get('i.pi').each(($icon) => {
        cy.wrap($icon).should(($el) => {
          const fontFamily = $el.css('font-family')
          expect(fontFamily, 'Icon should use primeicons font').to.include('primeicons')
        })
      })
    })

    it('should render action buttons as visible and clickable', () => {
      cy.get('[data-testid="users-table"]').should('be.visible')
      cy.wait(1000)

      // Verify view button is visible and clickable
      cy.get('[data-testid="view-user-button"]')
        .first()
        .should('be.visible')
        .and('not.have.css', 'display', 'none')
        .and('not.have.css', 'visibility', 'hidden')
        .click({ force: true })

      // Verify navigation worked
      cy.url().should('include', '/users/')
    })
  })

  it('should display users list', () => {
    cy.get('[data-testid="users-table"]').should('exist')
    cy.contains('User Management').should('be.visible')
  })

  it('should open create user dialog', () => {
    cy.contains('button', 'Create User').click()
    cy.contains('Create New User').should('be.visible')
    // Wait for dialog animation to complete
    cy.wait(300)
    cy.get('#email').should('be.visible')
    cy.get('#password').should('be.visible')
  })

  it('should create a new user successfully', () => {
    // Generate unique email using timestamp
    const timestamp = Date.now()
    const uniqueEmail = `testuser${timestamp}@example.com`

    cy.contains('button', 'Create User').click()
    cy.contains('Create New User').should('be.visible')
    // Wait for dialog animation to complete
    cy.wait(300)

    // Fill form
    cy.get('#email').type(uniqueEmail)
    cy.get('#password').type('Password123!')
    cy.get('#firstName').type('New')
    cy.get('#lastName').type('User')
    cy.get('#phone').type('+34 111 222 333')

    // Submit using form button (not the page button)
    cy.get('form').within(() => {
      cy.contains('button', 'Create User').click()
    })

    // Wait for dialog to close
    cy.contains('Create New User').should('not.exist')
    cy.wait(500)

    // Verify success - user appears in table
    cy.contains(uniqueEmail).should('be.visible')
  })

  it('should disable submit when form is invalid', () => {
    // Ensure we're on the users page
    cy.url().should('include', '/users')
    cy.get('[data-testid="users-table"]').should('be.visible')

    cy.contains('button', 'Create User').click()
    cy.contains('Create New User').should('be.visible')
    // Wait for dialog animation to complete
    cy.wait(500)

    // Initially button should be disabled with empty form
    cy.get('form').within(() => {
      cy.contains('button', 'Create User').should('be.disabled')
    })

    // Enter invalid email (without @) and short password
    cy.get('#email').clear().type('invalidemail')
    cy.get('#password').clear().type('short')
    cy.get('#firstName').clear().type('Test')
    cy.get('#lastName').clear().type('User')

    // Wait a moment for computed property to update
    cy.wait(200)

    // Button should still be disabled due to validation errors
    cy.get('form').within(() => {
      cy.contains('button', 'Create User').should('be.disabled')
    })

    // Fix the errors - enter valid email and longer password
    cy.get('#email').clear().type('valid@example.com')
    cy.get('#password').clear().type('ValidPass123!')

    // Now button should be enabled
    cy.get('form').within(() => {
      cy.contains('button', 'Create User').should('not.be.disabled')
    })
  })

  it('should navigate to user detail page', () => {
    // Wait for table to load
    cy.get('[data-testid="users-table"]').should('be.visible')
    cy.wait(1000)

    // Click view button on first user (force click due to PrimeVue DataTable rendering)
    cy.get('[aria-label="View Details"]').first().click({ force: true })

    // Verify detail page
    cy.url().should('include', '/users/')
    cy.contains('User Details').should('be.visible')
  })

  it('should edit user successfully', () => {
    // Wait for table to load
    cy.get('[data-testid="users-table"]').should('be.visible')
    cy.wait(1000)

    // Navigate to detail page (force click due to PrimeVue DataTable rendering)
    cy.get('[aria-label="View Details"]').first().click({ force: true })
    cy.url().should('include', '/users/')

    // Click edit button
    cy.contains('button', 'Edit').click()
    cy.wait(300)

    // Modify first name
    cy.get('#firstName').clear().type('Updated')

    // Submit
    cy.contains('button', 'Update User').click()

    // Wait for form submission
    cy.wait(500)

    // Verify update
    cy.contains('Updated').should('be.visible')
    cy.contains('button', 'Edit').should('be.visible')
  })

  it('should cancel edit and return to view mode', () => {
    // Wait for table to load
    cy.get('[data-testid="users-table"]').should('be.visible')
    cy.wait(1000)

    cy.get('[aria-label="View Details"]').first().click({ force: true })
    cy.url().should('include', '/users/')

    cy.contains('button', 'Edit').click()
    cy.wait(300)

    // Cancel edit
    cy.contains('button', 'Cancel').click()

    // Should be back in view mode
    cy.contains('button', 'Edit').should('be.visible')
  })

  it('should navigate back to users list', () => {
    // Wait for table to load
    cy.get('[data-testid="users-table"]').should('be.visible')
    cy.wait(1000)

    cy.get('[aria-label="View Details"]').first().click({ force: true })
    cy.url().should('include', '/users/')

    cy.contains('button', 'Back to Users').click()

    cy.url().should('equal', Cypress.config().baseUrl + '/users')
  })
})
