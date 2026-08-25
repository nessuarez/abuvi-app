/**
 * Walks the 50th anniversary journey as a member.
 *
 * Assertions stay on the venue list, the year strip and the gallery — never on Leaflet
 * internals, which are slow and flaky to drive. The map is covered by unit tests.
 */
describe('Anniversary journey', () => {
  beforeEach(() => {
    cy.login('member')
    cy.visit('/anniversary')
  })

  it('shows the whole history before anything is selected', () => {
    cy.contains('Cincuenta años de mapa').should('be.visible')
    cy.contains(/\d+ ediciones en \d+ sedes/).should('be.visible')
    cy.get('[aria-label="Sedes de los campamentos"]').should('exist')
    cy.get('[aria-label="Años con campamento"]').should('exist')
    cy.screenshot('journey-initial', { capture: 'viewport' })
  })

  it('lists every venue with its edition years', () => {
    cy.get('[aria-label="Sedes de los campamentos"]').within(() => {
      cy.get('button[aria-label^="Edición de"]').should('have.length.greaterThan', 40)
    })
  })

  it('selecting a year from the list drives the gallery', () => {
    cy.get('button[aria-label^="Edición de 2015"]').first().click()

    cy.contains('h2', 'Recuerdos de 2015').should('be.visible')
    cy.contains('2015').should('be.visible')
    cy.screenshot('journey-year-selected', { capture: 'viewport' })
  })

  it('turns a year with nothing kept into a call to action', () => {
    cy.get('button[aria-label^="Edición de 1987"]').first().click()

    cy.contains('no conservamos nada todavía').should('be.visible')
    cy.contains('button', 'Comparte tu recuerdo').should('be.visible')
  })

  it('reports how many times the association returned to a venue', () => {
    cy.get('button[aria-label^="Edición de 2015 en Espinosa"]').first().click()

    cy.contains('edición 4 de 4 aquí').should('be.visible')
  })

  it('walks the years on its own in presentation mode', () => {
    cy.contains('button', 'Recorrer los 50 años').click()

    cy.get('[aria-label="Años con campamento"] [aria-current="true"]')
      .invoke('text')
      .then((first) => {
        cy.wait(2500)
        cy.get('[aria-label="Años con campamento"] [aria-current="true"]')
          .invoke('text')
          .should('not.eq', first)
      })

    cy.contains('button', 'Pausar recorrido').click()
  })
})
