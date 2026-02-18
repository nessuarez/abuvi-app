describe('Google Places Autocomplete for Camp Locations', () => {
  const autocompleteUrl = '/api/places/autocomplete'
  const detailsUrl = '/api/places/details'

  const mockAutocompleteResponse = {
    success: true,
    data: [
      {
        placeId: 'ChIJN1t_tDeuEmsRUsoyG83frY4',
        description: 'Camping El Pinar, Madrid',
        mainText: 'Camping El Pinar',
        secondaryText: 'Madrid, España'
      },
      {
        placeId: 'ChIJ2ndplace123',
        description: 'Camping Los Pinos, Barcelona',
        mainText: 'Camping Los Pinos',
        secondaryText: 'Barcelona, España'
      }
    ],
    error: null
  }

  const mockDetailsResponse = {
    success: true,
    data: {
      placeId: 'ChIJN1t_tDeuEmsRUsoyG83frY4',
      name: 'Camping El Pinar',
      formattedAddress: 'Calle Example, 123, Madrid, España',
      latitude: 40.416775,
      longitude: -3.703790,
      types: ['campground', 'lodging']
    },
    error: null
  }

  beforeEach(() => {
    cy.visit('/camps/locations')
  })

  describe('Autocomplete Search', () => {
    it('should not trigger autocomplete for short input (less than 3 characters)', () => {
      cy.intercept('POST', autocompleteUrl).as('autocomplete')

      cy.get('[data-testid="new-camp-btn"]').click()

      // Type only 2 characters
      cy.get('#name').type('Ca')

      // Assert no API call was made
      cy.wait(500)
      cy.get('@autocomplete.all').should('have.length', 0)

      // Assert dropdown is not visible
      cy.get('.p-autocomplete-list').should('not.exist')
    })

    it('should show suggestions when typing 3 or more characters', () => {
      cy.intercept('POST', autocompleteUrl, mockAutocompleteResponse).as('autocomplete')

      cy.get('[data-testid="new-camp-btn"]').click()

      cy.get('#name').type('Cam')

      cy.wait('@autocomplete')

      // Assert suggestions dropdown is visible
      cy.get('.p-autocomplete-list').should('be.visible')
      cy.contains('Camping El Pinar').should('be.visible')
      cy.contains('Madrid, España').should('be.visible')
    })

    it('should display suggestion items with mainText and secondaryText', () => {
      cy.intercept('POST', autocompleteUrl, mockAutocompleteResponse).as('autocomplete')

      cy.get('[data-testid="new-camp-btn"]').click()

      cy.get('#name').type('Camping')

      cy.wait('@autocomplete')

      cy.get('.p-autocomplete-item').first().within(() => {
        cy.contains('Camping El Pinar').should('be.visible')
        cy.contains('Madrid, España').should('be.visible')
      })
    })
  })

  describe('Place Selection and Auto-fill', () => {
    beforeEach(() => {
      cy.intercept('POST', autocompleteUrl, mockAutocompleteResponse).as('autocomplete')
      cy.intercept('POST', detailsUrl, mockDetailsResponse).as('details')
    })

    it('should auto-fill all fields when selecting a place from suggestions', () => {
      cy.get('[data-testid="new-camp-btn"]').click()

      // Type in autocomplete field
      cy.get('#name').type('Camping El')

      // Wait for autocomplete results
      cy.wait('@autocomplete')

      // Select first suggestion
      cy.get('.p-autocomplete-item').first().click()

      // Wait for details
      cy.wait('@details')

      // Verify auto-filled fields
      cy.get('#location').should('have.value', 'Calle Example, 123, Madrid, España')
      cy.get('#latitude input, #latitude').filter(':visible').first().should('have.value', '40,416775')
      cy.get('#longitude input, #longitude').filter(':visible').first().should('have.value', '-3,703790')
    })

    it('should show auto-fill indicator message after place selection', () => {
      cy.get('[data-testid="new-camp-btn"]').click()

      cy.get('#name').type('Camping')

      cy.wait('@autocomplete')

      cy.get('.p-autocomplete-item').first().click()

      cy.wait('@details')

      cy.contains('Datos cargados desde Google Places').should('be.visible')
    })

    it('should show "Escribir manualmente" button after place selection', () => {
      cy.get('[data-testid="new-camp-btn"]').click()

      cy.get('#name').type('Camping')

      cy.wait('@autocomplete')

      cy.get('.p-autocomplete-item').first().click()

      cy.wait('@details')

      cy.contains('Escribir manualmente').should('be.visible')
    })

    it('should show auto-completed labels on location and coordinate fields', () => {
      cy.get('[data-testid="new-camp-btn"]').click()

      cy.get('#name').type('Camping')

      cy.wait('@autocomplete')

      cy.get('.p-autocomplete-item').first().click()

      cy.wait('@details')

      cy.contains('(Auto-completado)').should('be.visible')
    })
  })

  describe('Manual Override', () => {
    it('should allow manual entry after clearing autocomplete', () => {
      cy.intercept('POST', autocompleteUrl, mockAutocompleteResponse).as('autocomplete')
      cy.intercept('POST', detailsUrl, mockDetailsResponse).as('details')

      cy.get('[data-testid="new-camp-btn"]').click()

      cy.get('#name').type('Camping')
      cy.wait('@autocomplete')
      cy.get('.p-autocomplete-item').first().click()
      cy.wait('@details')

      // Verify auto-fill indicator is visible
      cy.contains('Datos cargados desde Google Places').should('be.visible')

      // Click "Write manually" button
      cy.contains('Escribir manualmente').click()

      // Verify auto-fill indicator is gone
      cy.contains('Datos cargados desde Google Places').should('not.exist')

      // Verify manual entry still works
      cy.get('#location').clear().type('Custom Location')
      cy.get('#location').should('have.value', 'Custom Location')
    })

    it('should clear auto-filled labels when writing manually', () => {
      cy.intercept('POST', autocompleteUrl, mockAutocompleteResponse).as('autocomplete')
      cy.intercept('POST', detailsUrl, mockDetailsResponse).as('details')

      cy.get('[data-testid="new-camp-btn"]').click()

      cy.get('#name').type('Camping')
      cy.wait('@autocomplete')
      cy.get('.p-autocomplete-item').first().click()
      cy.wait('@details')

      cy.contains('Escribir manualmente').click()

      cy.contains('(Auto-completado)').should('not.exist')
    })
  })

  describe('Error Handling', () => {
    it('should display user-friendly error message when API is unavailable', () => {
      cy.intercept('POST', autocompleteUrl, {
        statusCode: 503,
        body: {
          success: false,
          data: null,
          error: {
            message: 'El servicio de ubicaciones no está disponible. Por favor intenta más tarde.',
            code: 'PLACES_SERVICE_UNAVAILABLE'
          }
        }
      }).as('autocompleteError')

      cy.get('[data-testid="new-camp-btn"]').click()

      cy.get('#name').type('Camping')

      cy.wait('@autocompleteError')

      cy.contains('El servicio de ubicaciones no está disponible').should('be.visible')
    })

    it('should allow manual data entry when Google Places API fails', () => {
      cy.intercept('POST', autocompleteUrl, {
        statusCode: 503,
        body: { success: false, data: null, error: { message: 'Service unavailable', code: 'UNAVAILABLE' } }
      }).as('autocompleteError')

      cy.get('[data-testid="new-camp-btn"]').click()

      cy.get('#name').type('Camping error test')

      cy.wait('@autocompleteError')

      // Verify manual entry still works
      cy.get('#location').type('Manual Location')
      cy.get('#location').should('have.value', 'Manual Location')
    })
  })

  describe('Form Submission', () => {
    it('should include googlePlaceId when submitting form with auto-filled data', () => {
      cy.intercept('POST', autocompleteUrl, mockAutocompleteResponse).as('autocomplete')
      cy.intercept('POST', detailsUrl, mockDetailsResponse).as('details')
      cy.intercept('POST', '/api/camps').as('createCamp')

      cy.get('[data-testid="new-camp-btn"]').click()

      cy.get('#name').type('Camping')
      cy.wait('@autocomplete')
      cy.get('.p-autocomplete-item').first().click()
      cy.wait('@details')

      // Fill required pricing fields
      cy.get('#priceAdult input').type('100')
      cy.get('#priceChild input').type('50')
      cy.get('#priceBaby input').type('0')

      cy.get('button[type="submit"]').click()

      cy.wait('@createCamp').its('request.body').should('deep.include', {
        googlePlaceId: 'ChIJN1t_tDeuEmsRUsoyG83frY4'
      })
    })
  })
})
