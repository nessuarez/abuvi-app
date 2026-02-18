const CAMP_ID = 'camp-test-1'
const CAMP_URL = `/camps/locations/${CAMP_ID}`
const PHOTOS_API = `/api/camps/${CAMP_ID}/photos`

const boardUser = {
  id: 'user-board',
  email: 'board@example.com',
  firstName: 'Board',
  lastName: 'User',
  role: 'Board',
  isActive: true
}

const memberUser = {
  id: 'user-member',
  email: 'member@example.com',
  firstName: 'Member',
  lastName: 'User',
  role: 'Member',
  isActive: true
}

const mockCamp = {
  success: true,
  data: {
    id: CAMP_ID,
    name: 'Camping Test',
    description: 'Un campamento de prueba',
    location: 'Sierra Norte, Madrid',
    latitude: 40.5,
    longitude: -3.8,
    status: 'Active',
    basePriceAdult: 400,
    basePriceChild: 250,
    basePriceBaby: 0,
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: '2025-01-01T00:00:00Z',
    photos: [
      {
        id: 'photo-1',
        campId: CAMP_ID,
        url: 'https://picsum.photos/400/300?random=1',
        description: 'Vista del lago',
        displayOrder: 0,
        isPrimary: true,
        isOriginal: true,
        createdAt: '2025-01-01T00:00:00Z',
        updatedAt: '2025-01-01T00:00:00Z'
      },
      {
        id: 'photo-2',
        campId: CAMP_ID,
        url: 'https://picsum.photos/400/300?random=2',
        description: 'Zona de tiendas',
        displayOrder: 1,
        isPrimary: false,
        isOriginal: false,
        createdAt: '2025-01-01T00:00:00Z',
        updatedAt: '2025-01-01T00:00:00Z'
      }
    ]
  },
  error: null
}

const mockCampNoPhotos = {
  ...mockCamp,
  data: { ...mockCamp.data, photos: [] }
}

const setAuthState = (user: typeof boardUser | typeof memberUser) => {
  cy.window().then((win) => {
    win.localStorage.setItem('abuvi_auth_token', 'fake-jwt-token')
    win.localStorage.setItem('abuvi_user', JSON.stringify(user))
  })
}

describe('Camp Photo Gallery', () => {
  beforeEach(() => {
    cy.intercept('GET', `/api/camps/${CAMP_ID}`, mockCamp).as('getCamp')
  })

  describe('Role-based visibility', () => {
    it('should show photo gallery section for Board users', () => {
      cy.visit(CAMP_URL)
      setAuthState(boardUser)
      cy.reload()
      cy.wait('@getCamp')

      cy.get('[data-testid="photo-grid"]').should('be.visible')
    })

    it('should NOT show photo gallery section for Member users', () => {
      cy.visit(CAMP_URL)
      setAuthState(memberUser)
      cy.reload()
      cy.wait('@getCamp')

      cy.get('[data-testid="photo-grid"]').should('not.exist')
      cy.get('[data-testid="add-photo-button"]').should('not.exist')
    })

    it('should show empty state when camp has no photos (Board user)', () => {
      cy.intercept('GET', `/api/camps/${CAMP_ID}`, mockCampNoPhotos).as('getCampNoPhotos')

      cy.visit(CAMP_URL)
      setAuthState(boardUser)
      cy.reload()
      cy.wait('@getCampNoPhotos')

      cy.get('[data-testid="empty-photo-state"]').should('be.visible')
      cy.contains('No hay fotos todavía').should('be.visible')
    })
  })

  describe('Add photo', () => {
    beforeEach(() => {
      cy.visit(CAMP_URL)
      setAuthState(boardUser)
      cy.reload()
      cy.wait('@getCamp')
    })

    it('should open add photo dialog when clicking "Añadir foto"', () => {
      cy.get('[data-testid="add-photo-button"]').click()

      cy.contains('Añadir foto').should('be.visible')
      cy.get('#photoUrl').should('be.visible')
    })

    it('should add a new photo and show it in the gallery', () => {
      const newPhoto = {
        id: 'photo-new',
        campId: CAMP_ID,
        url: 'https://picsum.photos/400/300?random=99',
        description: 'Nueva foto',
        displayOrder: 2,
        isPrimary: false,
        isOriginal: false,
        createdAt: '2025-06-01T00:00:00Z',
        updatedAt: '2025-06-01T00:00:00Z'
      }

      cy.intercept('POST', PHOTOS_API, {
        body: { success: true, data: newPhoto, error: null }
      }).as('addPhoto')

      cy.get('[data-testid="add-photo-button"]').click()

      cy.get('#photoUrl').type(newPhoto.url)
      cy.get('#photoDescription').type('Nueva foto')

      cy.contains('button', 'Añadir').click()

      cy.wait('@addPhoto')
      cy.contains('Foto añadida correctamente').should('be.visible')
    })

    it('should show validation error when URL is missing', () => {
      cy.get('[data-testid="add-photo-button"]').click()

      cy.contains('button', 'Añadir').click()

      cy.contains('La URL de la foto es obligatoria').should('be.visible')
    })
  })

  describe('Delete photo', () => {
    beforeEach(() => {
      cy.visit(CAMP_URL)
      setAuthState(boardUser)
      cy.reload()
      cy.wait('@getCamp')
    })

    it('should show confirmation dialog before deleting a photo', () => {
      cy.get('[data-testid="camp-photo-card"]').first().within(() => {
        cy.get('[data-testid="delete-photo-button"]').click()
      })

      cy.contains('Eliminar foto').should('be.visible')
      cy.contains('Esta acción no se puede deshacer').should('be.visible')
    })

    it('should delete a photo after confirmation', () => {
      cy.intercept('DELETE', `${PHOTOS_API}/photo-1`, {
        body: { success: true, data: null, error: null }
      }).as('deletePhoto')

      cy.get('[data-testid="camp-photo-card"]').first().within(() => {
        cy.get('[data-testid="delete-photo-button"]').click()
      })

      cy.contains('button', 'Eliminar').click()

      cy.wait('@deletePhoto')
      cy.contains('Foto eliminada correctamente').should('be.visible')
    })
  })

  describe('Set primary photo', () => {
    beforeEach(() => {
      cy.visit(CAMP_URL)
      setAuthState(boardUser)
      cy.reload()
      cy.wait('@getCamp')
    })

    it('should set a photo as primary', () => {
      const updatedPhoto = { ...mockCamp.data.photos[1], isPrimary: true }

      cy.intercept('POST', `${PHOTOS_API}/photo-2/set-primary`, {
        body: { success: true, data: updatedPhoto, error: null }
      }).as('setPrimary')

      cy.get('[data-testid="camp-photo-card"]').eq(1).within(() => {
        cy.get('[data-testid="set-primary-button"]').click()
      })

      cy.wait('@setPrimary')
      cy.contains('Foto principal actualizada').should('be.visible')
    })
  })

  describe('Reorder photos', () => {
    beforeEach(() => {
      cy.visit(CAMP_URL)
      setAuthState(boardUser)
      cy.reload()
      cy.wait('@getCamp')
    })

    it('should toggle reorder mode when clicking "Reordenar"', () => {
      cy.contains('button', 'Reordenar').click()

      cy.contains('Arrastra las fotos para cambiar el orden').should('be.visible')
      cy.contains('button', 'Guardar orden').should('be.visible')
    })

    it('should save reordered photos when clicking "Guardar orden"', () => {
      cy.intercept('PUT', `${PHOTOS_API}/reorder`, {
        body: { success: true, data: null, error: null }
      }).as('reorder')

      cy.contains('button', 'Reordenar').click()
      cy.contains('button', 'Guardar orden').click()

      cy.wait('@reorder')
      cy.contains('Orden de fotos guardado').should('be.visible')
    })
  })
})
