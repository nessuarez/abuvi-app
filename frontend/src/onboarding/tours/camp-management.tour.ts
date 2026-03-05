import type { OnboardingTour } from '@/types/onboarding'

export const campManagementTour: OnboardingTour = {
  id: 'camp-management',
  name: 'Guía de Gestión de Campamentos',
  description: 'Aprende a gestionar ubicaciones y ediciones de campamento',
  routes: ['/camps/locations', '/camps/editions'],
  requiresBoard: true,
  steps: [
    {
      element: '[data-onboarding="camp-locations-table"]',
      title: 'Ubicaciones de Campamento',
      description: 'Consulta y gestiona todas las ubicaciones de campamento desde esta tabla. Puedes añadir, editar y revisar los detalles de cada ubicación.',
      side: 'top',
    },
    {
      element: '[data-onboarding="camp-editions-table"]',
      title: 'Ediciones de Campamento',
      description: 'Gestiona las ediciones de campamento, establece fechas y configura los ajustes de inscripción para cada edición.',
      side: 'top',
    },
  ],
}
