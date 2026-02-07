/// <reference types="cypress" />

describe('Home Page', () => {
  it('should display welcome message', () => {
    cy.visit('/')
    cy.contains('Welcome to ABUVI').should('be.visible')
  })

  it('should show backend health status', () => {
    cy.visit('/')
    cy.contains('Backend:').should('be.visible')
  })
})
