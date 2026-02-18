describe('Admin Panel (/admin)', () => {
  it('board user can access admin panel', () => {
    cy.login('board@abuvi.org', 'password123')
    cy.visit('/admin')
    cy.get('h1').should('contain.text', 'Panel de Administración')
  })

  it('regular member is redirected away from /admin', () => {
    cy.login('member@abuvi.org', 'password123')
    cy.visit('/admin')
    cy.url().should('include', '/home')
  })

  it('navigation shows Administración button for board users', () => {
    cy.login('board@abuvi.org', 'password123')
    cy.visit('/home')
    cy.contains('Administración').should('be.visible')
  })

  it('navigation does NOT show Administración for regular members', () => {
    cy.login('member@abuvi.org', 'password123')
    cy.visit('/home')
    cy.contains('Administración').should('not.exist')
  })

  it('admin panel shows three tabs', () => {
    cy.login('board@abuvi.org', 'password123')
    cy.visit('/admin')
    cy.contains('Campamentos').should('be.visible')
    cy.contains('Unidades Familiares').should('be.visible')
    cy.contains('Usuarios').should('be.visible')
  })

  it('clicking Usuarios tab shows user management', () => {
    cy.login('board@abuvi.org', 'password123')
    cy.visit('/admin')
    cy.contains('Usuarios').click()
    cy.get('[data-testid="users-admin-panel"]').should('be.visible')
  })
})
