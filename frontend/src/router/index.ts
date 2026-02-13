import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    // Public route - Landing/Auth page
    {
      path: '/',
      name: 'landing',
      component: () => import('@/views/LandingPage.vue'),
      meta: {
        requiresAuth: false,
        title: 'ABUVI'
      }
    },

    // Protected routes - Authenticated users only
    {
      path: '/home',
      name: 'home',
      component: () => import('@/views/HomePage.vue'),
      meta: {
        requiresAuth: true,
        title: 'ABUVI | Home'
      }
    },
    {
      path: '/camp',
      name: 'camp',
      component: () => import('@/views/CampPage.vue'),
      meta: {
        requiresAuth: true,
        title: 'ABUVI | Camp'
      }
    },
    {
      path: '/anniversary',
      name: 'anniversary',
      component: () => import('@/views/AnniversaryPage.vue'),
      meta: {
        requiresAuth: true,
        title: 'ABUVI | 50th Anniversary'
      }
    },
    {
      path: '/profile',
      name: 'profile',
      component: () => import('@/views/ProfilePage.vue'),
      meta: {
        requiresAuth: true,
        title: 'ABUVI | Profile'
      }
    },
    {
      path: '/admin',
      name: 'admin',
      component: () => import('@/views/AdminPage.vue'),
      meta: {
        requiresAuth: true,
        requiresAdmin: true,
        title: 'ABUVI | Admin'
      }
    },

    // Legacy routes for user management (backward compatibility)
    {
      path: '/users',
      name: 'users',
      component: () => import('@/pages/UsersPage.vue'),
      meta: {
        title: 'User Management',
        requiresAuth: true,
        requiresBoard: true
      }
    },
    {
      path: '/users/:id',
      name: 'user-detail',
      component: () => import('@/pages/UserDetailPage.vue'),
      meta: {
        title: 'User Details',
        requiresAuth: true
      }
    },

    // Legacy login/register routes - redirect to landing
    {
      path: '/login',
      redirect: '/'
    },
    {
      path: '/register',
      redirect: '/'
    }
  ]
})

// Route guard for authentication
router.beforeEach((to, from, next) => {
  const auth = useAuthStore()

  // Update document title
  document.title = (to.meta.title as string) || 'ABUVI'

  // Check if route requires authentication
  if (to.meta.requiresAuth && !auth.isAuthenticated) {
    // Redirect to landing page with redirect URL
    next({ path: '/', query: { redirect: to.fullPath } })
    return
  }

  // Check if route requires admin role
  if (to.meta.requiresAdmin && !auth.isAdmin) {
    // Redirect to home if not admin
    next({ path: '/home' })
    return
  }

  // Check if route requires board role (Admin or Board)
  if (to.meta.requiresBoard && !auth.isBoard) {
    // Redirect to home page
    next({ path: '/home' })
    return
  }

  // Redirect authenticated users from landing page to home
  if (to.path === '/' && auth.isAuthenticated) {
    const redirect = to.query.redirect as string | undefined
    next(redirect || '/home')
    return
  }

  next()
})

export default router
