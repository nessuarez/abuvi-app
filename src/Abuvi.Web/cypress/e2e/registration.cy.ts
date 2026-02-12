describe('User Registration', () => {
  beforeEach(() => {
    cy.visit('/register')
  })

  it('should complete registration successfully', () => {
    cy.intercept('POST', '/api/auth/register-user', {
      statusCode: 200,
      body: {
        success: true,
        data: {
          id: '123',
          email: 'test@example.com',
          firstName: 'John',
          lastName: 'Doe',
          role: 'Member',
          isActive: false,
          emailVerified: false,
          createdAt: '2026-02-12T00:00:00Z',
          updatedAt: '2026-02-12T00:00:00Z'
        }
      }
    }).as('register')

    cy.get('[data-testid="email-input"]').type('test@example.com')
    cy.get('[data-testid="password-input"] input').type('Test123!@#')
    cy.get('[data-testid="firstName-input"]').type('John')
    cy.get('[data-testid="lastName-input"]').type('Doe')
    cy.get('[data-testid="terms-checkbox"]').check({ force: true })
    cy.get('[data-testid="submit-button"]').click()

    cy.wait('@register')
    cy.get('[data-testid="success-message"]').should('be.visible')
    cy.get('[data-testid="success-message"]').should('contain', 'check your email')
  })

  it('should show validation errors for invalid input', () => {
    cy.get('[data-testid="submit-button"]').click()

    cy.get('[data-testid="email-error"]').should('be.visible')
    cy.get('[data-testid="password-error"]').should('be.visible')
    cy.get('[data-testid="firstName-error"]').should('be.visible')
    cy.get('[data-testid="lastName-error"]').should('be.visible')
    cy.get('[data-testid="terms-error"]').should('be.visible')
  })

  it('should validate email format', () => {
    cy.get('[data-testid="email-input"]').type('invalid-email')
    cy.get('[data-testid="password-input"] input').type('Test123!@#')
    cy.get('[data-testid="firstName-input"]').type('John')
    cy.get('[data-testid="lastName-input"]').type('Doe')
    cy.get('[data-testid="terms-checkbox"]').check({ force: true })
    cy.get('[data-testid="submit-button"]').click()

    cy.get('[data-testid="email-error"]').should('contain', 'valid email address')
  })

  it('should validate password strength with visual feedback', () => {
    cy.get('[data-testid="password-input"] input').type('weak')

    cy.contains('Password Strength').should('be.visible')
    cy.contains('Weak').should('be.visible')
    cy.contains('At least 8 characters').should('be.visible')
  })

  it('should handle EMAIL_EXISTS error', () => {
    cy.intercept('POST', '/api/auth/register-user', {
      statusCode: 400,
      body: {
        success: false,
        error: {
          code: 'EMAIL_EXISTS',
          message: 'An account with this email already exists'
        }
      }
    }).as('registerError')

    cy.get('[data-testid="email-input"]').type('existing@example.com')
    cy.get('[data-testid="password-input"] input').type('Test123!@#')
    cy.get('[data-testid="firstName-input"]').type('John')
    cy.get('[data-testid="lastName-input"]').type('Doe')
    cy.get('[data-testid="terms-checkbox"]').check({ force: true })
    cy.get('[data-testid="submit-button"]').click()

    cy.wait('@registerError')
    cy.get('[data-testid="error-message"]').should('contain', 'email already exists')
  })

  it('should handle DOCUMENT_EXISTS error', () => {
    cy.intercept('POST', '/api/auth/register-user', {
      statusCode: 400,
      body: {
        success: false,
        error: {
          code: 'DOCUMENT_EXISTS',
          message: 'An account with this document number already exists'
        }
      }
    }).as('registerError')

    cy.get('[data-testid="email-input"]').type('test@example.com')
    cy.get('[data-testid="password-input"] input').type('Test123!@#')
    cy.get('[data-testid="firstName-input"]').type('John')
    cy.get('[data-testid="lastName-input"]').type('Doe')
    cy.get('[data-testid="documentNumber-input"]').type('12345678A')
    cy.get('[data-testid="terms-checkbox"]').check({ force: true })
    cy.get('[data-testid="submit-button"]').click()

    cy.wait('@registerError')
    cy.get('[data-testid="error-message"]').should('contain', 'document number already exists')
  })

  it('should validate document number format', () => {
    cy.get('[data-testid="documentNumber-input"]').type('lowercase123')
    cy.get('[data-testid="email-input"]').type('test@example.com')
    cy.get('[data-testid="password-input"] input').type('Test123!@#')
    cy.get('[data-testid="firstName-input"]').type('John')
    cy.get('[data-testid="lastName-input"]').type('Doe')
    cy.get('[data-testid="terms-checkbox"]').check({ force: true })
    cy.get('[data-testid="submit-button"]').click()

    cy.get('[data-testid="documentNumber-error"]').should('contain', 'uppercase letters and numbers')
  })

  it('should validate phone format', () => {
    cy.get('[data-testid="phone-input"]').type('invalid-phone')
    cy.get('[data-testid="email-input"]').type('test@example.com')
    cy.get('[data-testid="password-input"] input').type('Test123!@#')
    cy.get('[data-testid="firstName-input"]').type('John')
    cy.get('[data-testid="lastName-input"]').type('Doe')
    cy.get('[data-testid="terms-checkbox"]').check({ force: true })
    cy.get('[data-testid="submit-button"]').click()

    cy.get('[data-testid="phone-error"]').should('contain', 'valid phone number')
  })

  it('should disable submit button while loading', () => {
    cy.intercept('POST', '/api/auth/register-user', (req) => {
      req.reply((res) => {
        res.delay(1000).send({
          success: true,
          data: {
            id: '123',
            email: 'test@example.com',
            firstName: 'John',
            lastName: 'Doe',
            role: 'Member',
            isActive: false,
            emailVerified: false,
            createdAt: '2026-02-12T00:00:00Z',
            updatedAt: '2026-02-12T00:00:00Z'
          }
        })
      })
    })

    cy.get('[data-testid="email-input"]').type('test@example.com')
    cy.get('[data-testid="password-input"] input').type('Test123!@#')
    cy.get('[data-testid="firstName-input"]').type('John')
    cy.get('[data-testid="lastName-input"]').type('Doe')
    cy.get('[data-testid="terms-checkbox"]').check({ force: true })
    cy.get('[data-testid="submit-button"]').click()

    cy.get('[data-testid="submit-button"]').should('be.disabled')
  })
})

describe('Email Verification', () => {
  it('should verify email successfully with valid token', () => {
    cy.intercept('POST', '/api/auth/verify-email', {
      statusCode: 200,
      body: {
        success: true,
        data: { message: 'Email verified successfully' }
      }
    }).as('verifyEmail')

    cy.visit('/verify-email?token=valid-token')

    cy.wait('@verifyEmail')
    cy.contains('Email verified successfully').should('be.visible')
    cy.contains('Go to Login').should('be.visible')
  })

  it('should show error for invalid token', () => {
    cy.intercept('POST', '/api/auth/verify-email', {
      statusCode: 404,
      body: {
        success: false,
        error: {
          code: 'NOT_FOUND',
          message: 'User not found'
        }
      }
    }).as('verifyEmailError')

    cy.visit('/verify-email?token=invalid-token')

    cy.wait('@verifyEmailError')
    cy.contains('Verification failed').should('be.visible')
    cy.contains('Invalid verification token').should('be.visible')
  })

  it('should show error for expired token', () => {
    cy.intercept('POST', '/api/auth/verify-email', {
      statusCode: 400,
      body: {
        success: false,
        error: {
          code: 'VERIFICATION_FAILED',
          message: 'Token expired'
        }
      }
    }).as('verifyEmailError')

    cy.visit('/verify-email?token=expired-token')

    cy.wait('@verifyEmailError')
    cy.contains('Verification failed').should('be.visible')
    cy.contains('expired').should('be.visible')
  })

  it('should show error when no token provided', () => {
    cy.visit('/verify-email')

    cy.contains('Verification failed').should('be.visible')
    cy.contains('Invalid verification link').should('be.visible')
  })
})

describe('Resend Verification', () => {
  beforeEach(() => {
    cy.visit('/resend-verification')
  })

  it('should resend verification email successfully', () => {
    cy.intercept('POST', '/api/auth/resend-verification', {
      statusCode: 200,
      body: {
        success: true,
        data: { message: 'Verification email sent' }
      }
    }).as('resendVerification')

    cy.get('#email').type('test@example.com')
    cy.get('button[type="submit"]').click()

    cy.wait('@resendVerification')
    cy.contains('Verification email sent').should('be.visible')
  })

  it('should validate email format', () => {
    cy.get('#email').type('invalid-email')
    cy.get('button[type="submit"]').click()

    cy.contains('valid email address').should('be.visible')
  })

  it('should handle email not found error', () => {
    cy.intercept('POST', '/api/auth/resend-verification', {
      statusCode: 404,
      body: {
        success: false,
        error: {
          code: 'NOT_FOUND',
          message: 'User not found'
        }
      }
    }).as('resendError')

    cy.get('#email').type('notfound@example.com')
    cy.get('button[type="submit"]').click()

    cy.wait('@resendError')
    cy.contains('Invalid verification token').should('be.visible')
  })

  it('should handle already verified error', () => {
    cy.intercept('POST', '/api/auth/resend-verification', {
      statusCode: 400,
      body: {
        success: false,
        error: {
          code: 'RESEND_FAILED',
          message: 'Email already verified'
        }
      }
    }).as('resendError')

    cy.get('#email').type('verified@example.com')
    cy.get('button[type="submit"]').click()

    cy.wait('@resendError')
    cy.contains('already verified').should('be.visible')
  })

  it('should disable submit button when email is empty', () => {
    cy.get('button[type="submit"]').should('be.disabled')
  })
})
