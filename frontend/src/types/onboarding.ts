import type { Ref } from 'vue'

/** Media attachment for a tour step */
export interface OnboardingStepMedia {
  type: 'video' | 'audio' | 'image'
  src: string
  alt?: string
}

/** Single step in an onboarding tour */
export interface OnboardingStep {
  /** CSS selector for the target element */
  element: string
  /** Step title */
  title: string
  /** Step description (supports HTML) */
  description: string
  /** Popover position */
  side?: 'top' | 'bottom' | 'left' | 'right'
  /** Optional media to embed */
  media?: OnboardingStepMedia
  /** Callback when user advances */
  onNext?: () => void
  /** Callback when user goes back */
  onPrevious?: () => void
}

/** Tour definition */
export interface OnboardingTour {
  /** Unique tour identifier */
  id: string
  /** Human-readable tour name (for help menu) */
  name: string
  /** Optional description shown in help menu */
  description?: string
  /** Route path(s) where this tour is relevant */
  routes: string[]
  /** Whether this tour requires board role */
  requiresBoard?: boolean
  /** Tour steps */
  steps: OnboardingStep[]
}

/** Return type of useOnboarding composable */
export interface UseOnboardingReturn {
  /** Start a tour by its ID */
  startTour: (tourId: string) => void
  /** Check if a tour has been completed */
  hasCompletedTour: (tourId: string) => boolean
  /** Reset a specific tour's completion status */
  resetTour: (tourId: string) => void
  /** Reset all tour completion statuses */
  resetAllTours: () => void
  /** Get tours available for the current route */
  getAvailableTours: () => OnboardingTour[]
  /** Auto-trigger uncompleted tours for the current route */
  autoTrigger: () => void
  /** Whether a tour is currently active */
  isActive: Ref<boolean>
}
