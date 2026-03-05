import type { OnboardingTour } from '@/types/onboarding'

export const registrationTour: OnboardingTour = {
  id: 'registration-flow',
  name: 'Guía de Inscripción',
  description: 'Aprende cómo inscribirte en una edición de campamento',
  routes: ['/registrations/new'],
  requiresBoard: false,
  steps: [
    {
      element: '[data-onboarding="registration-stepper"]',
      title: 'Asistente de Inscripción',
      description: 'Sigue estos pasos para completar tu inscripción al campamento. Cada paso te guía a través del proceso.',
      side: 'bottom',
    },
  ],
}
