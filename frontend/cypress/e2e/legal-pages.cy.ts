// E2E tests for legal pages — all pages are public (no auth required)
describe('Legal Pages', () => {
  describe('Aviso Legal (/legal/notice)', () => {
    beforeEach(() => {
      cy.visit('/legal/notice')
    })

    it('should load the page and display the title', () => {
      cy.get('[data-testid="legal-page-title"]').should('contain.text', 'Aviso Legal')
    })

    it('should display the last updated date', () => {
      cy.get('[data-testid="legal-last-updated"]').should('be.visible')
    })

    it('should show the "Volver al inicio" back link', () => {
      cy.get('[data-testid="legal-back-link"]').should('be.visible')
      cy.get('[data-testid="legal-back-link"]').should('contain.text', 'Volver al inicio')
    })

    it('should display the table of contents', () => {
      cy.get('[data-testid="legal-toc"]').should('be.visible')
    })

    it('should display the print button', () => {
      cy.get('[data-testid="print-button"]').should('be.visible')
    })

    it('should show all required sections', () => {
      cy.get('#identificacion').should('exist')
      cy.get('#objeto').should('exist')
      cy.get('#propiedad-intelectual').should('exist')
      cy.get('#responsabilidad').should('exist')
      cy.get('#legislacion').should('exist')
    })

    it('should navigate back to landing page when clicking back link', () => {
      cy.get('[data-testid="legal-back-link"]').click()
      cy.url().should('eq', Cypress.config().baseUrl + '/')
    })

    it('should be accessible without authentication', () => {
      // Verify we did not get redirected to landing or blocked
      cy.url().should('include', '/legal/notice')
      cy.get('[data-testid="legal-page-title"]').should('exist')
    })
  })

  describe('Política de Privacidad (/legal/privacy)', () => {
    beforeEach(() => {
      cy.visit('/legal/privacy')
    })

    it('should load and display the title', () => {
      cy.get('[data-testid="legal-page-title"]').should('contain.text', 'Política de Privacidad')
    })

    it('should display the last updated date', () => {
      cy.get('[data-testid="legal-last-updated"]').should('be.visible')
    })

    it('should show the "Volver al inicio" back link', () => {
      cy.get('[data-testid="legal-back-link"]').should('contain.text', 'Volver al inicio')
    })

    it('should display the table of contents', () => {
      cy.get('[data-testid="legal-toc"]').should('be.visible')
    })

    it('should show all GDPR required sections', () => {
      cy.get('#responsable').should('exist')
      cy.get('#datos-recopilados').should('exist')
      cy.get('#base-legal').should('exist')
      cy.get('#conservacion').should('exist')
      cy.get('#destinatarios').should('exist')
      cy.get('#derechos').should('exist')
      cy.get('#seguridad').should('exist')
      cy.get('#cookies').should('exist')
      cy.get('#contacto').should('exist')
    })

    it('should mention AEPD in contact section', () => {
      cy.get('#contacto').should('contain.text', 'Agencia Española de Protección de Datos')
    })

    it('should be accessible without authentication', () => {
      cy.url().should('include', '/legal/privacy')
      cy.get('[data-testid="legal-page-title"]').should('exist')
    })
  })

  describe('Estatutos (/legal/bylaws)', () => {
    beforeEach(() => {
      cy.visit('/legal/bylaws')
    })

    it('should load and display the title', () => {
      cy.get('[data-testid="legal-page-title"]').should('contain.text', 'Estatutos')
    })

    it('should show the "Volver al inicio" back link', () => {
      cy.get('[data-testid="legal-back-link"]').should('contain.text', 'Volver al inicio')
    })

    it('should display placeholder message', () => {
      cy.contains('Próximamente publicaremos aquí la documentación oficial de la asociación').should('be.visible')
    })

    it('should be accessible without authentication', () => {
      cy.url().should('include', '/legal/bylaws')
      cy.get('[data-testid="legal-page-title"]').should('exist')
    })
  })

  describe('TOC anchor navigation', () => {
    it('should scroll to section when clicking TOC link on Aviso Legal', () => {
      cy.visit('/legal/notice')
      cy.get('[data-testid="legal-toc"] a').first().click()
      // After clicking, the hash should be updated
      cy.url().should('include', '#identificacion')
    })

    it('should scroll to section when clicking TOC link on Privacy page', () => {
      cy.visit('/legal/privacy')
      cy.get('[data-testid="legal-toc"] a').first().click()
      cy.url().should('include', '#responsable')
    })
  })
})
