import { createRouter, createWebHistory } from 'vue-router'
import HomePage from '@/pages/HomePage.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomePage
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/pages/auth/RegisterPage.vue')
    },
    {
      path: '/verify-email',
      name: 'verifyEmail',
      component: () => import('@/pages/auth/VerifyEmailPage.vue')
    },
    {
      path: '/resend-verification',
      name: 'resendVerification',
      component: () => import('@/pages/auth/ResendVerificationPage.vue')
    }
  ]
})

export default router
