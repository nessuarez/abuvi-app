import { ref } from 'vue'
import { useRoute } from 'vue-router'
import { driver as createDriver } from 'driver.js'
import type { DriveStep } from 'driver.js'
import { useAuthStore } from '@/stores/auth'
import type {
  OnboardingTour,
  OnboardingStep,
  OnboardingStepMedia,
  UseOnboardingReturn,
} from '@/types/onboarding'

const STORAGE_KEY = 'abuvi:onboarding:completed'

const tourRegistry = new Map<string, OnboardingTour>()

export function registerTour(tour: OnboardingTour): void {
  tourRegistry.set(tour.id, tour)
}

function getCompletedTours(): string[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? JSON.parse(raw) : []
  } catch {
    return []
  }
}

function saveCompletedTours(completed: string[]): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(completed))
  } catch {
    // localStorage unavailable (e.g. private browsing)
  }
}

function markTourCompleted(tourId: string): void {
  const completed = getCompletedTours()
  if (!completed.includes(tourId)) {
    completed.push(tourId)
    saveCompletedTours(completed)
  }
}

function buildMediaHtml(media: OnboardingStepMedia): string {
  switch (media.type) {
    case 'image':
      return `<img src="${media.src}" alt="${media.alt ?? ''}" class="max-w-full rounded mt-2" />`
    case 'video':
      return `<video src="${media.src}" controls class="max-w-full rounded mt-2"></video>`
    case 'audio':
      return `<audio src="${media.src}" controls class="mt-2"></audio>`
  }
}

function mapStepsToDriverSteps(steps: OnboardingStep[]): DriveStep[] {
  return steps
    .filter((step) => document.querySelector(step.element))
    .map((step) => {
      let description = step.description
      if (step.media) {
        description += buildMediaHtml(step.media)
      }

      const driveStep: DriveStep = {
        element: step.element,
        popover: {
          title: step.title,
          description,
          side: step.side,
        },
      }

      if (step.onNext) {
        const onNextCallback = step.onNext
        driveStep.popover!.onNextClick = (_el, _step, { driver }) => {
          onNextCallback()
          driver.moveNext()
        }
      }

      if (step.onPrevious) {
        const onPrevCallback = step.onPrevious
        driveStep.popover!.onPrevClick = (_el, _step, { driver }) => {
          onPrevCallback()
          driver.movePrevious()
        }
      }

      return driveStep
    })
}

export function useOnboarding(): UseOnboardingReturn {
  const route = useRoute()
  const auth = useAuthStore()
  const isActive = ref(false)

  function startTour(tourId: string): void {
    const tour = tourRegistry.get(tourId)
    if (!tour) {
      console.warn(`[onboarding] Tour "${tourId}" not found in registry`)
      return
    }

    const driverSteps = mapStepsToDriverSteps(tour.steps)
    if (driverSteps.length === 0) {
      console.warn(`[onboarding] Tour "${tourId}" has no visible steps`)
      return
    }

    try {
      const driverInstance = createDriver({
        showProgress: true,
        showButtons: ['next', 'previous', 'close'],
        nextBtnText: 'Siguiente →',
        prevBtnText: '← Anterior',
        doneBtnText: 'Finalizar',
        animate: true,
        allowClose: true,
        overlayColor: 'rgba(0, 0, 0, 0.5)',
        steps: driverSteps,
        onDestroyStarted: () => {
          isActive.value = false
          markTourCompleted(tourId)
          driverInstance.destroy()
        },
        onDestroyed: () => {
          isActive.value = false
        },
      })

      isActive.value = true
      driverInstance.drive()
    } catch (error) {
      console.error(`[onboarding] Failed to start tour "${tourId}"`, error)
      isActive.value = false
    }
  }

  function hasCompletedTour(tourId: string): boolean {
    return getCompletedTours().includes(tourId)
  }

  function resetTour(tourId: string): void {
    const completed = getCompletedTours().filter((id) => id !== tourId)
    saveCompletedTours(completed)
  }

  function resetAllTours(): void {
    try {
      localStorage.removeItem(STORAGE_KEY)
    } catch {
      // localStorage unavailable
    }
  }

  function getAvailableTours(): OnboardingTour[] {
    const currentPath = route.path
    return Array.from(tourRegistry.values()).filter((tour) => {
      if (!tour.routes.some((r) => currentPath.startsWith(r))) {
        return false
      }
      if (tour.requiresBoard && !auth.isBoard) {
        return false
      }
      return true
    })
  }

  function autoTrigger(): void {
    const available = getAvailableTours()
    const uncompleted = available.find((tour) => !hasCompletedTour(tour.id))
    if (uncompleted) {
      startTour(uncompleted.id)
    }
  }

  return {
    startTour,
    hasCompletedTour,
    resetTour,
    resetAllTours,
    getAvailableTours,
    autoTrigger,
    isActive,
  }
}
