describe('Forgot Password Flow', () => {
  beforeEach(() => {
    cy.clearLocalStorage()
  })

  describe('Navigation from Login', () => {
    it('should navigate to /forgot-password from login form', () => {
      cy.visit('/')
      cy.contains('¿Olvidaste tu contraseña?').click()
      cy.url().should('include', '/forgot-password')
    })
  })

  describe('Forgot Password Page', () => {
    beforeEach(() => {
      cy.visit('/forgot-password')
    })

    it('should display the email input and submit button', () => {
      cy.get('[data-testid="email-input"]').should('be.visible')
      cy.get('[data-testid="submit-button"]').should('be.visible')
      cy.contains('Enviar enlace de recuperación').should('be.visible')
    })

    it('should show error when email is empty on submit', () => {
      cy.get('[data-testid="submit-button"]').click()
      cy.contains('El correo electrónico es obligatorio').should('be.visible')
    })

    it('should show error for invalid email format', () => {
      cy.get('[data-testid="email-input"]').type('not-an-email')
      cy.get('[data-testid="submit-button"]').click()
      cy.contains('Formato de correo electrónico inválido').should('be.visible')
    })

    it('should show success message after valid email submission', () => {
      cy.intercept('POST', '/api/auth/forgot-password', {
        statusCode: 200,
        body: { success: true, data: { message: 'ok' }, error: null }
      }).as('forgotPassword')

      cy.get('[data-testid="email-input"]').type('user@example.com')
      cy.get('[data-testid="submit-button"]').click()

      cy.wait('@forgotPassword')
      cy.contains('Si tu correo está registrado').should('be.visible')
    })

    it('should show "Volver al inicio de sesión" link after success', () => {
      cy.intercept('POST', '/api/auth/forgot-password', {
        statusCode: 200,
        body: { success: true, data: {}, error: null }
      }).as('forgotPassword')

      cy.get('[data-testid="email-input"]').type('user@example.com')
      cy.get('[data-testid="submit-button"]').click()
      cy.wait('@forgotPassword')

      cy.contains('Volver al inicio de sesión').should('be.visible').click()
      cy.url().should('eq', Cypress.config().baseUrl + '/')
    })
  })

  describe('Reset Password Page — missing token', () => {
    it('should show error and redirect when token is absent', () => {
      cy.visit('/reset-password')
      cy.contains('El enlace de recuperación no es válido').should('be.visible')
      // Wait for auto-redirect (3 seconds)
      cy.url({ timeout: 4000 }).should('eq', Cypress.config().baseUrl + '/')
    })
  })

  describe('Reset Password Page — with token', () => {
    beforeEach(() => {
      cy.visit('/reset-password?token=test-token-abc')
    })

    it('should display password fields and submit button', () => {
      cy.get('[data-testid="new-password-input"]').should('be.visible')
      cy.get('[data-testid="confirm-password-input"]').should('be.visible')
      cy.get('[data-testid="submit-button"]').should('be.visible')
      cy.contains('Restablecer Contraseña').should('be.visible')
    })

    it('should show error when passwords are empty', () => {
      cy.get('[data-testid="submit-button"]').click()
      cy.contains('La nueva contraseña es obligatoria').should('be.visible')
      cy.contains('Debes confirmar la contraseña').should('be.visible')
    })

    it('should show error when password is too short', () => {
      cy.get('[data-testid="new-password-input"]').find('input').type('short')
      cy.get('[data-testid="confirm-password-input"]').find('input').type('short')
      cy.get('[data-testid="submit-button"]').click()
      cy.contains('La contraseña debe tener al menos 8 caracteres').should('be.visible')
    })

    it('should show error when passwords do not match', () => {
      cy.get('[data-testid="new-password-input"]').find('input').type('ValidPass123')
      cy.get('[data-testid="confirm-password-input"]').find('input').type('DifferentPass123')
      cy.get('[data-testid="submit-button"]').click()
      cy.contains('Las contraseñas no coinciden').should('be.visible')
    })

    it('should show success message on valid submission', () => {
      cy.intercept('POST', '/api/auth/reset-password', {
        statusCode: 200,
        body: { success: true, data: { message: 'ok' }, error: null }
      }).as('resetPassword')

      cy.get('[data-testid="new-password-input"]').find('input').type('NewPassword123')
      cy.get('[data-testid="confirm-password-input"]').find('input').type('NewPassword123')
      cy.get('[data-testid="submit-button"]').click()

      cy.wait('@resetPassword')
      cy.contains('Tu contraseña ha sido restablecida exitosamente.').should('be.visible')
      cy.get('[data-testid="login-link"]').should('be.visible')
    })

    it('should show error message on invalid/expired token (400)', () => {
      cy.intercept('POST', '/api/auth/reset-password', {
        statusCode: 400,
        body: {
          success: false,
          data: null,
          error: {
            message: 'El enlace de recuperación es inválido o ha expirado.',
            code: 'INVALID_OR_EXPIRED_TOKEN'
          }
        }
      }).as('resetPassword')

      cy.get('[data-testid="new-password-input"]').find('input').type('NewPassword123')
      cy.get('[data-testid="confirm-password-input"]').find('input').type('NewPassword123')
      cy.get('[data-testid="submit-button"]').click()

      cy.wait('@resetPassword')
      cy.get('[data-testid="error-message"]').should(
        'contain.text',
        'El enlace de recuperación es inválido o ha expirado.'
      )
    })
  })
})
