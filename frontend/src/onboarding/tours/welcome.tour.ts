import type { OnboardingTour } from '@/types/onboarding'

export const welcomeTour: OnboardingTour = {
  id: 'welcome',
  name: 'Tour de Bienvenida',
  description: 'Conoce la plataforma ABUVI',
  routes: ['/home'],
  requiresBoard: false,
  steps: [
    {
      element: '[data-onboarding="welcome-heading"]',
      title: 'Bienvenido a ABUVI',
      description: 'Este es tu panel de inicio. Desde aquí puedes acceder a todas las funciones clave de la plataforma.',
      side: 'bottom',
    },
    {
      element: '[data-onboarding="quick-access-cards"]',
      title: 'Acceso Rápido',
      description: 'Usa estas tarjetas para navegar rápidamente a las secciones más importantes de la aplicación.',
      side: 'top',
    },
    {
      element: '[data-onboarding="main-nav"]',
      title: 'Menú de Navegación',
      description: 'Usa la barra de navegación para moverte entre las diferentes secciones de la plataforma.',
      side: 'bottom',
    },
    {
      element: '[data-onboarding="user-menu"]',
      title: 'Tu Perfil',
      description: 'Accede a la configuración de tu perfil y cierra sesión desde aquí.',
      side: 'bottom',
    },
  ],
}
