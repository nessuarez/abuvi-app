describe('Blob Storage Admin Stats Panel', () => {
  const adminToken = 'mock-admin-token'
  const adminUser = {
    id: 'admin-1',
    email: 'admin@abuvi.org',
    firstName: 'Admin',
    lastName: 'User',
    role: 'Admin'
  }

  function loginAsAdmin() {
    window.localStorage.setItem('abuvi_auth_token', adminToken)
    window.localStorage.setItem('abuvi_user', JSON.stringify(adminUser))
  }

  function loginAsBoard() {
    window.localStorage.setItem('abuvi_auth_token', 'mock-board-token')
    window.localStorage.setItem(
      'abuvi_user',
      JSON.stringify({ ...adminUser, id: 'board-1', email: 'board@abuvi.org', role: 'Board' })
    )
  }

  beforeEach(() => {
    cy.clearLocalStorage()
    cy.intercept('GET', '**/blobs/stats', { fixture: 'blob-stats.json' }).as('getStats')
  })

  it('Admin sees the Almacenamiento sidebar item', () => {
    cy.window().then(loginAsAdmin)
    cy.visit('/admin')
    cy.get('[data-testid="sidebar-storage"]').should('be.visible').and('contain.text', 'Almacenamiento')
  })

  it('Stats load when navigating to storage page and summary cards are visible', () => {
    cy.window().then(loginAsAdmin)
    cy.visit('/admin/storage')
    cy.wait('@getStats')

    cy.get('[data-testid="card-total-objects"]').should('be.visible').and('contain.text', '27')
    cy.get('[data-testid="card-total-size"]').should('be.visible').and('contain.text', '15 MB')
  })

  it('Quota progress bar is displayed with usage percentage', () => {
    cy.window().then(loginAsAdmin)
    cy.visit('/admin/storage')
    cy.wait('@getStats')

    cy.get('[data-testid="quota-section"]').should('be.visible')
    cy.get('[data-testid="quota-progress-bar"]').should('exist')
    cy.get('[data-testid="quota-section"]').should('contain.text', '0.0%')
  })

  it('Folder breakdown table shows all four folders', () => {
    cy.window().then(loginAsAdmin)
    cy.visit('/admin/storage')
    cy.wait('@getStats')

    cy.get('[data-testid="folder-stats-table"]').should('be.visible')
    cy.get('[data-testid="folder-stats-table"]').should('contain.text', 'photos')
    cy.get('[data-testid="folder-stats-table"]').should('contain.text', 'media-items')
    cy.get('[data-testid="folder-stats-table"]').should('contain.text', 'camp-locations')
    cy.get('[data-testid="folder-stats-table"]').should('contain.text', 'camp-photos')
  })

  it('Board user does NOT see the Almacenamiento sidebar item', () => {
    cy.window().then(loginAsBoard)
    cy.visit('/admin')
    cy.get('[data-testid="sidebar-storage"]').should('not.exist')
  })

  it('Refresh button triggers a new stats fetch', () => {
    cy.window().then(loginAsAdmin)
    cy.visit('/admin/storage')
    cy.wait('@getStats')

    cy.get('[data-testid="refresh-btn"]').click()
    cy.wait('@getStats')
  })
})
