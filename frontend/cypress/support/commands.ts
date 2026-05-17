/// <reference types="cypress" />

// ***********************************************
// This example commands.ts shows you how to
// create various custom commands and overwrite
// existing commands.
//
// For more comprehensive examples of custom
// commands please read more here:
// https://on.cypress.io/custom-commands
// ***********************************************

// Custom command to verify PrimeIcons library is loaded
Cypress.Commands.add('verifyPrimeIconsLoaded', () => {
  cy.document().then((doc) => {
    const stylesheets = Array.from(doc.styleSheets)
    const primeIconsLoaded = stylesheets.some((sheet) => {
      try {
        return sheet.href && sheet.href.includes('primeicons')
      } catch {
        return false
      }
    })
    expect(primeIconsLoaded, 'PrimeIcons stylesheet should be loaded').to.be.true
  })
})

// Custom command to verify an icon is visible and properly styled
Cypress.Commands.add('verifyIcon', { prevSubject: 'element' }, (subject, iconClass: string) => {
  cy.wrap(subject)
    .find(`i.pi.${iconClass}`)
    .should('exist')
    .and('be.visible')
    .and('have.css', 'font-family')
    .and('include', 'primeicons')
})

// Custom command to verify a button with icon is visible
Cypress.Commands.add('verifyButtonWithIcon', (buttonSelector: string, iconClass: string) => {
  cy.get(buttonSelector)
    .should('be.visible')
    .and('not.have.css', 'display', 'none')
    .and('not.have.css', 'visibility', 'hidden')
    .find(`i.pi.${iconClass}`)
    .should('exist')
    .and('be.visible')
})

Cypress.Commands.add('login', (role: 'admin' | 'board' | 'member' = 'member') => {
  const credentials = {
    admin:  { email: Cypress.env('ADMIN_EMAIL'),  password: Cypress.env('ADMIN_PASSWORD') },
    board:  { email: Cypress.env('BOARD_EMAIL'),  password: Cypress.env('BOARD_PASSWORD') },
    member: { email: Cypress.env('MEMBER_EMAIL'), password: Cypress.env('MEMBER_PASSWORD') },
  }
  const { email, password } = credentials[role]
  cy.request({
    method: 'POST',
    url: `${Cypress.env('API_URL')}/auth/login`,
    body: { email, password },
  }).then((response) => {
    const { token, user } = response.body.data
    localStorage.setItem('abuvi_auth_token', token)
    localStorage.setItem('abuvi_user', JSON.stringify(user))
  })
})

// Declare custom commands for TypeScript
declare global {
  namespace Cypress {
    interface Chainable {
      /**
       * Authenticates programmatically via the E2E API. Sets both auth localStorage keys.
       * Requires cypress.env.json with credentials and API_URL.
       * @param role - 'admin' | 'board' | 'member' (default: 'member')
       * @example cy.login('admin')
       */
      login(role?: 'admin' | 'board' | 'member'): Chainable<void>

      /**
       * Verifies that the PrimeIcons stylesheet is loaded
       * @example cy.verifyPrimeIconsLoaded()
       */
      verifyPrimeIconsLoaded(): Chainable<void>

      /**
       * Verifies that an icon element is visible and properly styled within a subject element
       * @param iconClass - The icon class name (e.g., 'pi-eye', 'pi-plus')
       * @example cy.get('button').verifyIcon('pi-eye')
       */
      verifyIcon(iconClass: string): Chainable<JQuery<HTMLElement>>

      /**
       * Verifies that a button contains a visible icon
       * @param buttonSelector - The CSS selector for the button
       * @param iconClass - The icon class name (e.g., 'pi-eye', 'pi-plus')
       * @example cy.verifyButtonWithIcon('[data-testid="view-user-button"]', 'pi-eye')
       */
      verifyButtonWithIcon(buttonSelector: string, iconClass: string): Chainable<void>
    }
  }
}

export {}
