import type { OnboardingTour } from '@/types/onboarding'

export const membershipTour: OnboardingTour = {
  id: 'membership',
  name: 'Guía de Membresía',
  description: 'Aprende a gestionar tu membresía',
  routes: ['/profile'],
  requiresBoard: false,
  steps: [
    {
      element: '[data-onboarding="membership-section"]',
      title: 'Tu Membresía',
      description: 'Consulta y gestiona el estado de tu membresía y cuotas desde esta sección.',
      side: 'top',
    },
  ],
}
