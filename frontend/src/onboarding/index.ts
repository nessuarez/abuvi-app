import { registerTour } from '@/composables/useOnboarding'
// import { welcomeTour } from './tours/welcome.tour'  // temporarily disabled
import { registrationTour } from './tours/registration.tour'
import { membershipTour } from './tours/membership.tour'
import { campManagementTour } from './tours/camp-management.tour'

// registerTour(welcomeTour)  // temporarily disabled
registerTour(registrationTour)
registerTour(membershipTour)
registerTour(campManagementTour)
