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

// Declare custom commands for TypeScript
declare global {
  namespace Cypress {
    interface Chainable {
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
