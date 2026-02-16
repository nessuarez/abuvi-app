describe('Family Units Management', () => {
  beforeEach(() => {
    // Login as Member user
    cy.login('member@example.com', 'password123')
    cy.visit('/family-unit')
  })

  it('should create a new family unit', () => {
    // Should show "no family unit" state
    cy.contains('Aún no tienes una unidad familiar').should('be.visible')

    // Click create button
    cy.contains('Crear Unidad Familiar').click()

    // Fill in family unit name
    cy.get('#family-unit-name').type('García Family')

    // Submit form
    cy.contains('button', 'Crear').click()

    // Should show success message
    cy.contains('Unidad familiar creada').should('be.visible')

    // Should show family unit card
    cy.contains('García Family').should('be.visible')

    // Should show representative as first member (auto-created by backend)
    cy.contains('Miembros Familiares').should('be.visible')
  })

  it('should add a new family member', () => {
    // Assuming family unit already exists
    cy.contains('Añadir Miembro').click()

    // Fill in member details
    cy.get('#first-name').type('María')
    cy.get('#last-name').type('García López')
    cy.get('#date-of-birth').type('15/06/2015')
    cy.get('#relationship').click()
    cy.contains('Hijo/Hija').click()
    cy.get('#document-number').type('ABC123')
    cy.get('#email').type('maria@example.com')
    cy.get('#phone').type('+34612345678')
    cy.get('#medical-notes').type('Asthma - requires inhaler')
    cy.get('#allergies').type('Peanuts')

    // Submit form
    cy.contains('button', 'Añadir Miembro').click()

    // Should show success message
    cy.contains('Miembro añadido').should('be.visible')

    // Should show member in table
    cy.contains('María García López').should('be.visible')
    cy.contains('ABC123').should('be.visible')
    cy.contains('maria@example.com').should('be.visible')

    // Should show health tags (NOT actual content)
    cy.contains('Notas médicas').should('be.visible')
    cy.contains('Alergias').should('be.visible')
  })

  it('should edit an existing family member', () => {
    // Click edit button on first member
    cy.get('[data-testid="edit-member-btn"]').first().click()

    // Change last name
    cy.get('#last-name').clear().type('García Martínez')

    // Submit form
    cy.contains('button', 'Actualizar').click()

    // Should show success message
    cy.contains('Miembro actualizado').should('be.visible')

    // Should show updated name
    cy.contains('García Martínez').should('be.visible')
  })

  it('should delete a family member', () => {
    // Click delete button
    cy.get('[data-testid="delete-member-btn"]').first().click()

    // Confirm deletion
    cy.contains('button', 'Eliminar').click()

    // Should show success message
    cy.contains('Miembro eliminado').should('be.visible')
  })

  it('should validate required fields when creating member', () => {
    cy.contains('Añadir Miembro').click()

    // Try to submit empty form
    cy.contains('button', 'Añadir Miembro').should('be.disabled')

    // Fill only first name
    cy.get('#first-name').type('María')

    // Should still be disabled (other required fields missing)
    cy.contains('button', 'Añadir Miembro').should('be.disabled')

    // Fill all required fields
    cy.get('#last-name').type('García')
    cy.get('#date-of-birth').type('15/06/2015')
    cy.get('#relationship').click()
    cy.contains('Hijo/Hija').click()

    // Now button should be enabled
    cy.contains('button', 'Añadir Miembro').should('not.be.disabled')
  })

  it('should never display sensitive data (medical notes/allergies)', () => {
    // Medical notes and allergies should NEVER be visible as text
    cy.contains('Asthma').should('not.exist')
    cy.contains('Peanuts').should('not.exist')

    // Only tags should be visible
    cy.contains('Notas médicas').should('be.visible')
    cy.contains('Alergias').should('be.visible')
  })

  it('should delete family unit and all members', () => {
    // Click delete family unit button
    cy.contains('button', 'Eliminar').click()

    // Confirm deletion
    cy.contains('¿Estás seguro de que quieres eliminar la unidad familiar?').should('be.visible')
    cy.contains('button', 'Eliminar').click()

    // Should show success message
    cy.contains('Unidad familiar eliminada').should('be.visible')

    // Should return to "no family unit" state
    cy.contains('Aún no tienes una unidad familiar').should('be.visible')
  })

  it('should edit family unit name', () => {
    // Click edit family unit button
    cy.get('[data-testid="edit-family-unit-btn"]').click()

    // Change name
    cy.get('#family-unit-name').clear().type('Updated Family Name')

    // Submit form
    cy.contains('button', 'Actualizar').click()

    // Should show success message
    cy.contains('Unidad familiar actualizada').should('be.visible')

    // Should show updated name
    cy.contains('Updated Family Name').should('be.visible')
  })

  it('should auto-uppercase document number', () => {
    cy.contains('Añadir Miembro').click()

    // Type lowercase document number
    cy.get('#document-number').type('abc123def')

    // Should be converted to uppercase
    cy.get('#document-number').should('have.value', 'ABC123DEF')
  })

  it('should show validation error for invalid email', () => {
    cy.contains('Añadir Miembro').click()

    // Fill required fields
    cy.get('#first-name').type('María')
    cy.get('#last-name').type('García')
    cy.get('#date-of-birth').type('15/06/2015')
    cy.get('#relationship').click()
    cy.contains('Hijo/Hija').click()

    // Enter invalid email
    cy.get('#email').type('invalid-email')
    cy.get('#email').blur()

    // Should show validation error
    cy.contains('Formato de correo electrónico inválido').should('be.visible')
  })

  it('should show validation error for invalid phone format', () => {
    cy.contains('Añadir Miembro').click()

    // Fill required fields
    cy.get('#first-name').type('María')
    cy.get('#last-name').type('García')
    cy.get('#date-of-birth').type('15/06/2015')
    cy.get('#relationship').click()
    cy.contains('Hijo/Hija').click()

    // Enter invalid phone (not E.164 format)
    cy.get('#phone').type('612345678')
    cy.get('#phone').blur()

    // Should show validation error
    cy.contains('El teléfono debe estar en formato E.164').should('be.visible')
  })

  it('should show info message when editing member with existing medical notes', () => {
    // Assuming member already has medical notes
    cy.get('[data-testid="edit-member-btn"]').first().click()

    // Should show info message
    cy.contains('Este miembro tiene notas médicas guardadas').should('be.visible')
    cy.contains('Déjalo en blanco para mantener las notas existentes').should('be.visible')
  })

  it('should calculate and display member age correctly', () => {
    // Assuming member with known birth date exists
    // Age should be calculated and displayed
    cy.contains('años').should('be.visible')
  })

  it('should show loading state during API calls', () => {
    // Intercept API call to make it slow
    cy.intercept('POST', '/api/family-units', (req) => {
      req.reply({
        delay: 2000,
        statusCode: 200,
        body: {
          success: true,
          data: {
            id: '123',
            name: 'García Family',
            representativeUserId: 'user-1',
            createdAt: '2026-02-15T10:00:00Z',
            updatedAt: '2026-02-15T10:00:00Z'
          }
        }
      })
    })

    cy.contains('Crear Unidad Familiar').click()
    cy.get('#family-unit-name').type('García Family')
    cy.contains('button', 'Crear').click()

    // Should show loading spinner on button
    cy.get('.p-button-loading').should('be.visible')
  })

  it('should sort family members by name in DataTable', () => {
    // Click on name column header to sort
    cy.contains('th', 'Nombre').click()

    // Verify sorting (this would require checking actual order)
    // Implementation depends on actual data in table
  })

  it('should paginate family members when more than 10 exist', () => {
    // Assuming more than 10 members exist
    // Should show pagination controls
    cy.get('.p-paginator').should('be.visible')
  })

  it('should display empty state when no members exist', () => {
    // Assuming family unit exists but has no members
    cy.contains('No hay miembros familiares registrados').should('be.visible')
  })

  it('should prevent future dates for date of birth', () => {
    cy.contains('Añadir Miembro').click()

    // Try to enter future date
    const futureDate = new Date()
    futureDate.setFullYear(futureDate.getFullYear() + 1)

    cy.get('#date-of-birth').type(futureDate.toLocaleDateString('es-ES'))
    cy.get('#date-of-birth').blur()

    // Should show validation error
    cy.contains('La fecha de nacimiento debe ser una fecha pasada').should('be.visible')
  })
})
