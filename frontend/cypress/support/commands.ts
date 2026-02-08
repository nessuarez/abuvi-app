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

// Example custom command:
// Cypress.Commands.add('login', (email, password) => { ... })

// Declare custom commands for TypeScript
declare global {
  namespace Cypress {
    interface Chainable {
      // Add your custom commands here
      // Example: login(email: string, password: string): Chainable<void>
    }
  }
}

export {}
