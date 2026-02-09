describe('Authentication Flow', () => {
  const timestamp = Date.now()
  const testEmail = `testuser${timestamp}@example.com`
  const testPassword = 'TestPass123!'

  beforeEach(() => {
    // Clear auth state before each test
    cy.clearLocalStorage()
  })

  describe('Registration', () => {
    beforeEach(() => {
      cy.visit('/register')
    })

    it('should display registration form', () => {
      cy.contains('Register for ABUVI').should('be.visible')
      cy.get('#email').should('be.visible')
      cy.get('#password').should('be.visible')
      cy.get('#firstName').should('be.visible')
      cy.get('#lastName').should('be.visible')
      cy.get('#phone').should('be.visible')
      cy.contains('button', 'Register').should('be.visible')
    })

    it('should register a new user successfully', () => {
      const uniqueEmail = `newuser${Date.now()}@example.com`

      cy.get('#email').type(uniqueEmail)
      cy.get('#password').type(testPassword)
      cy.get('#firstName').type('Test')
      cy.get('#lastName').type('User')
      cy.get('#phone').type('+34 123 456 789')

      cy.contains('button', 'Register').click()

      // Should show success message
      cy.contains('Registration successful! Redirecting to login...').should('be.visible')

      // Should redirect to login page
      cy.url().should('include', '/login', { timeout: 3000 })
    })

    it('should validate required fields', () => {
      // Try to submit empty form
      cy.contains('button', 'Register').click()

      // Should show validation errors
      cy.contains('Email is required').should('be.visible')
      cy.contains('Password is required').should('be.visible')
      cy.contains('First name is required').should('be.visible')
      cy.contains('Last name is required').should('be.visible')
    })

    it('should validate email format', () => {
      cy.get('#email').type('invalid-email')
      cy.get('#password').type(testPassword)
      cy.get('#firstName').type('Test')
      cy.get('#lastName').type('User')

      cy.contains('button', 'Register').click()

      cy.contains('Invalid email format').should('be.visible')
    })

    it('should validate password requirements', () => {
      cy.get('#email').type(`test${Date.now()}@example.com`)
      cy.get('#firstName').type('Test')
      cy.get('#lastName').type('User')

      // Test too short password
      cy.get('#password').clear().type('Short1')
      cy.contains('button', 'Register').click()
      cy.contains('Password must be at least 8 characters').should('be.visible')

      // Test password without uppercase
      cy.get('#password').clear().type('password123')
      cy.contains('button', 'Register').click()
      cy.contains('Password must contain at least one uppercase letter').should('be.visible')

      // Test password without lowercase
      cy.get('#password').clear().type('PASSWORD123')
      cy.contains('button', 'Register').click()
      cy.contains('Password must contain at least one lowercase letter').should('be.visible')

      // Test password without number
      cy.get('#password').clear().type('PasswordNoNum')
      cy.contains('button', 'Register').click()
      cy.contains('Password must contain at least one number').should('be.visible')
    })

    it('should handle duplicate email error', () => {
      const duplicateEmail = `duplicate${timestamp}@example.com`

      // First registration
      cy.get('#email').type(duplicateEmail)
      cy.get('#password').type(testPassword)
      cy.get('#firstName').type('First')
      cy.get('#lastName').type('User')
      cy.contains('button', 'Register').click()

      // Wait for success and redirect
      cy.url().should('include', '/login', { timeout: 3000 })

      // Try to register again with same email
      cy.visit('/register')
      cy.get('#email').type(duplicateEmail)
      cy.get('#password').type(testPassword)
      cy.get('#firstName').type('Second')
      cy.get('#lastName').type('User')
      cy.contains('button', 'Register').click()

      // Should show error message
      cy.contains(/Email already (registered|exists)/i).should('be.visible')
    })

    it('should navigate to login page from registration', () => {
      cy.contains('Login here').click()
      cy.url().should('include', '/login')
      cy.contains('Login to ABUVI').should('be.visible')
    })
  })

  describe('Login', () => {
    before(() => {
      // Create a test user for login tests
      cy.visit('/register')
      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.get('#firstName').type('Test')
      cy.get('#lastName').type('User')
      cy.contains('button', 'Register').click()
      cy.url().should('include', '/login', { timeout: 3000 })
    })

    beforeEach(() => {
      cy.visit('/login')
    })

    it('should display login form', () => {
      cy.contains('Login to ABUVI').should('be.visible')
      cy.get('#email').should('be.visible')
      cy.get('#password').should('be.visible')
      cy.contains('button', 'Login').should('be.visible')
    })

    it('should login successfully with valid credentials', () => {
      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.contains('button', 'Login').click()

      // Should redirect to users page (or home)
      cy.url().should('not.include', '/login')

      // Should show user info in header
      cy.contains('Test User').should('be.visible')
      cy.contains('button', 'Logout').should('be.visible')
    })

    it('should show error for invalid credentials', () => {
      cy.get('#email').type(testEmail)
      cy.get('#password').type('WrongPassword123!')
      cy.contains('button', 'Login').click()

      cy.contains('Invalid email or password').should('be.visible')
    })

    it('should validate required fields', () => {
      cy.contains('button', 'Login').click()

      cy.contains('Email is required').should('be.visible')
      cy.contains('Password is required').should('be.visible')
    })

    it('should validate email format', () => {
      cy.get('#email').type('invalid-email')
      cy.get('#password').type('password')
      cy.contains('button', 'Login').click()

      cy.contains('Invalid email format').should('be.visible')
    })

    it('should navigate to registration page from login', () => {
      cy.contains('Register here').click()
      cy.url().should('include', '/register')
      cy.contains('Register for ABUVI').should('be.visible')
    })

    it('should redirect to intended page after login', () => {
      // Try to access protected page
      cy.visit('/users')

      // Should redirect to login with redirect query
      cy.url().should('include', '/login')
      cy.url().should('include', 'redirect=%2Fusers')

      // Login
      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.contains('button', 'Login').click()

      // Should redirect back to users page
      cy.url().should('include', '/users')
      cy.contains('User Management').should('be.visible')
    })
  })

  describe('Logout', () => {
    beforeEach(() => {
      // Login before each test
      cy.visit('/login')
      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.contains('button', 'Login').click()
      cy.url().should('not.include', '/login')
    })

    it('should logout successfully', () => {
      // Verify logged in
      cy.contains('Test User').should('be.visible')

      // Click logout
      cy.contains('button', 'Logout').click()

      // Should redirect to login
      cy.url().should('include', '/login')

      // Should not show user info
      cy.contains('Test User').should('not.exist')
      cy.contains('button', 'Logout').should('not.exist')

      // Should show login button
      cy.contains('button', 'Login').should('be.visible')
    })

    it('should clear auth token from localStorage on logout', () => {
      // Verify token exists
      cy.window().then((win) => {
        const token = win.localStorage.getItem('authToken')
        expect(token).to.not.be.null
      })

      // Logout
      cy.contains('button', 'Logout').click()

      // Verify token is cleared
      cy.window().then((win) => {
        const token = win.localStorage.getItem('authToken')
        expect(token).to.be.null
      })
    })
  })

  describe('Authentication Guards', () => {
    it('should redirect to login when accessing protected route', () => {
      cy.visit('/users')

      // Should redirect to login
      cy.url().should('include', '/login')
      cy.contains('Login to ABUVI').should('be.visible')
    })

    it('should allow access to protected route when authenticated', () => {
      // Login first
      cy.visit('/login')
      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.contains('button', 'Login').click()

      // Should be able to access users page
      cy.visit('/users')
      cy.url().should('include', '/users')
      cy.contains('User Management').should('be.visible')
    })

    it('should allow access to public routes without authentication', () => {
      cy.visit('/')
      cy.url().should('eq', Cypress.config().baseUrl + '/')

      cy.visit('/login')
      cy.url().should('include', '/login')

      cy.visit('/register')
      cy.url().should('include', '/register')
    })
  })

  describe('Session Restoration', () => {
    it('should restore session from localStorage on page reload', () => {
      // Login
      cy.visit('/login')
      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.contains('button', 'Login').click()
      cy.url().should('not.include', '/login')

      // Verify logged in
      cy.contains('Test User').should('be.visible')

      // Reload page
      cy.reload()

      // Should still be logged in
      cy.contains('Test User').should('be.visible')
      cy.contains('button', 'Logout').should('be.visible')
    })

    it('should persist auth token in localStorage', () => {
      // Login
      cy.visit('/login')
      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.contains('button', 'Login').click()
      cy.url().should('not.include', '/login')

      // Verify token is stored
      cy.window().then((win) => {
        const token = win.localStorage.getItem('authToken')
        expect(token).to.not.be.null
        expect(token).to.be.a('string')
        expect(token.length).to.be.greaterThan(0)
      })
    })

    it('should maintain authentication across navigation', () => {
      // Login
      cy.visit('/login')
      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.contains('button', 'Login').click()

      // Navigate to home
      cy.contains('Home').click()
      cy.url().should('eq', Cypress.config().baseUrl + '/')

      // Should still show user info
      cy.contains('Test User').should('be.visible')

      // Navigate to users
      cy.contains('Users').click()
      cy.url().should('include', '/users')

      // Should still show user info
      cy.contains('Test User').should('be.visible')
    })
  })

  describe('UI Elements', () => {
    it('should show login button when not authenticated', () => {
      cy.visit('/')

      cy.contains('button', 'Login').should('be.visible')
      cy.contains('button', 'Logout').should('not.exist')
      cy.contains('Users').should('not.exist')
    })

    it('should show user info and logout button when authenticated', () => {
      // Login
      cy.visit('/login')
      cy.get('#email').type(testEmail)
      cy.get('#password').type(testPassword)
      cy.contains('button', 'Login').click()

      // Verify UI elements
      cy.contains('Test User').should('be.visible')
      cy.contains('Member').should('be.visible') // Role
      cy.contains('button', 'Logout').should('be.visible')
      cy.contains('Users').should('be.visible')
      cy.contains('button', 'Login').should('not.exist')
    })

    it('should display PrimeVue icons in auth pages', () => {
      cy.visit('/login')
      cy.contains('button', 'Login').verifyIcon('pi-sign-in')

      cy.visit('/register')
      cy.contains('button', 'Register').should('be.visible')
    })
  })
})
