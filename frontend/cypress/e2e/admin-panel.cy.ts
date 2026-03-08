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

  it('/admin redirects to /admin/camps by default', () => {
    cy.login('board@abuvi.org', 'password123')
    cy.visit('/admin')
    cy.url().should('include', '/admin/camps')
  })

  it('admin sidebar shows grouped menu items', () => {
    cy.login('board@abuvi.org', 'password123')
    cy.visit('/admin')
    cy.get('[data-testid="admin-sidebar"]').should('be.visible')
    cy.get('[data-testid="sidebar-camps"]').should('be.visible')
    cy.get('[data-testid="sidebar-registrations"]').should('be.visible')
    cy.get('[data-testid="sidebar-family-units"]').should('be.visible')
    cy.get('[data-testid="sidebar-users"]').should('be.visible')
  })

  it('clicking sidebar items navigates to sub-routes', () => {
    cy.login('board@abuvi.org', 'password123')
    cy.visit('/admin')
    cy.get('[data-testid="sidebar-users"]').click()
    cy.url().should('include', '/admin/users')
    cy.get('[data-testid="users-admin-panel"]').should('be.visible')
  })

  it('sidebar highlights active item', () => {
    cy.login('board@abuvi.org', 'password123')
    cy.visit('/admin/users')
    cy.get('[data-testid="sidebar-users"]').should('have.attr', 'aria-current', 'page')
  })
})
