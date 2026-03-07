import type { OnboardingTour } from '@/types/onboarding'

export const welcomeTour: OnboardingTour = {
  id: 'welcome',
  name: 'Tour de Bienvenida',
  description: 'Conoce la plataforma ABUVI',
  routes: ['/home'],
  requiresBoard: false,
  steps: [
    {
      element: '[data-onboarding="hero-carousel"]',
      title: 'Bienvenido a ABUVI',
      description:
        'Este es el carrusel de novedades. Aquí encontrarás las últimas noticias y eventos destacados de ABUVI.',
      side: 'bottom',
    },
    {
      element: '[data-onboarding="main-nav"]',
      title: 'Menú de Navegación',
      description:
        'Usa la barra de navegación para moverte entre las diferentes secciones de la plataforma.',
      side: 'bottom',
    },
    {
      element: '[data-onboarding="user-menu"]',
      title: 'Tu Perfil',
      description:
        'Accede a la configuración de tu perfil y cierra sesión desde aquí.',
      side: 'bottom',
    },
  ],
}
