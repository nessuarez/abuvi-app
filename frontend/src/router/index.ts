import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
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
      path: '/login',
      name: 'login',
      component: () => import('@/pages/LoginPage.vue'),
      meta: {
        title: 'Login'
      }
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/pages/RegisterPage.vue'),
      meta: {
        title: 'Register'
      }
    },
    {
      path: '/users',
      name: 'users',
      component: () => import('@/pages/UsersPage.vue'),
      meta: {
        title: 'User Management',
        requiresAuth: true,
        requiresAdmin: true // Admin only endpoint per backend
      }
    },
    {
      path: '/users/:id',
      name: 'user-detail',
      component: () => import('@/pages/UserDetailPage.vue'),
      meta: {
        title: 'User Details',
        requiresAuth: true // Authenticated users can view user details
      }
    }
  ]
})

// Route guard for authentication and authorization
router.beforeEach((to, from, next) => {
  const authStore = useAuthStore()

  // Check if route requires authentication
  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    // Redirect to login with return URL
    next({
      path: '/login',
      query: { redirect: to.fullPath }
    })
    return
  }

  // Check if route requires admin role
  if (to.meta.requiresAdmin && !authStore.isAdmin) {
    // Redirect to home page (or show 403 page)
    next({ path: '/' })
    return
  }

  // Allow navigation
  next()
})

export default router
