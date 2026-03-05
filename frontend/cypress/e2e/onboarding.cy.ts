describe('Onboarding Tours', () => {
  const STORAGE_KEY = 'abuvi:onboarding:completed'

  beforeEach(() => {
    cy.clearLocalStorage()
  })

  describe('Welcome Tour Auto-Start', () => {
    it('should auto-start the welcome tour on first visit to /home', () => {
      // Login first (set auth token in localStorage)
      cy.visit('/login')
      cy.get('#email').type('admin@abuvi.es')
      cy.get('#password').type('Admin123!')
      cy.contains('button', 'Iniciar Sesión').click()

      // Should navigate to /home
      cy.url().should('include', '/home')

      // Driver.js overlay should appear
      cy.get('.driver-popover', { timeout: 10000 }).should('be.visible')
      cy.get('.driver-popover').should('contain', 'Bienvenido a ABUVI')
    })

    it('should progress through tour steps with Next button', () => {
      cy.visit('/login')
      cy.get('#email').type('admin@abuvi.es')
      cy.get('#password').type('Admin123!')
      cy.contains('button', 'Iniciar Sesión').click()

      cy.url().should('include', '/home')

      // Step 1: Bienvenido
      cy.get('.driver-popover', { timeout: 10000 }).should('contain', 'Bienvenido a ABUVI')
      cy.get('.driver-popover button').contains('Next').click()

      // Step 2: Acceso Rápido
      cy.get('.driver-popover').should('contain', 'Acceso Rápido')
      cy.get('.driver-popover button').contains('Next').click()

      // Step 3: Menú de Navegación
      cy.get('.driver-popover').should('contain', 'Menú de Navegación')
      cy.get('.driver-popover button').contains('Next').click()

      // Step 4: Tu Perfil (last step)
      cy.get('.driver-popover').should('contain', 'Tu Perfil')
      cy.get('.driver-popover button').contains('Done').click()

      // Overlay should disappear
      cy.get('.driver-popover').should('not.exist')
    })

    it('should not auto-start tour again after completion', () => {
      // Pre-set completed tours
      cy.window().then((win) => {
        win.localStorage.setItem(STORAGE_KEY, JSON.stringify(['welcome']))
      })

      cy.visit('/login')
      cy.get('#email').type('admin@abuvi.es')
      cy.get('#password').type('Admin123!')
      cy.contains('button', 'Iniciar Sesión').click()

      cy.url().should('include', '/home')

      // Tour should NOT auto-start
      cy.wait(2000)
      cy.get('.driver-popover').should('not.exist')
    })
  })

  describe('OnboardingButton', () => {
    it('should show help button on authenticated pages', () => {
      cy.visit('/login')
      cy.get('#email').type('admin@abuvi.es')
      cy.get('#password').type('Admin123!')
      cy.contains('button', 'Iniciar Sesión').click()

      cy.url().should('include', '/home')

      // Close auto-started tour first
      cy.get('.driver-popover', { timeout: 10000 }).should('be.visible')
      cy.get('body').type('{esc}')

      // Help button should be visible
      cy.get('[aria-label="Help & Tours"]').should('be.visible')
    })

    it('should restart tour from help button', () => {
      // Pre-set completed tours so auto-trigger does not fire
      cy.window().then((win) => {
        win.localStorage.setItem(STORAGE_KEY, JSON.stringify(['welcome']))
      })

      cy.visit('/login')
      cy.get('#email').type('admin@abuvi.es')
      cy.get('#password').type('Admin123!')
      cy.contains('button', 'Iniciar Sesión').click()

      cy.url().should('include', '/home')

      // Click help button
      cy.get('[aria-label="Help & Tours"]').click()

      // Menu should show available tours
      cy.contains('Tour de Bienvenida').should('be.visible')

      // Click on Tour de Bienvenida
      cy.contains('Tour de Bienvenida').click()

      // Tour should restart
      cy.get('.driver-popover', { timeout: 10000 }).should('be.visible')
      cy.get('.driver-popover').should('contain', 'Bienvenido a ABUVI')
    })
  })
})
