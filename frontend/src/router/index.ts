import { createRouter, createWebHistory } from "vue-router";
import { useAuthStore } from "@/stores/auth";

const router = createRouter({
	history: createWebHistory(import.meta.env.BASE_URL),
	routes: [
		// Public route - Landing/Auth page
		{
			path: "/",
			name: "landing",
			component: () => import("@/views/LandingPage.vue"),
			meta: {
				requiresAuth: false,
				title: "ABUVI"
			}
		},

		// Protected routes - Authenticated users only
		{
			path: "/home",
			name: "home",
			component: () => import("@/views/HomePage.vue"),
			meta: {
				requiresAuth: true,
				title: "ABUVI | Inicio"
			}
		},
		{
			path: "/camp",
			name: "camp",
			component: () => import("@/views/CampPage.vue"),
			meta: {
				requiresAuth: true,
				title: "ABUVI | Campamento"
			}
		},
		{
			path: "/anniversary",
			name: "anniversary",
			component: () => import("@/views/AnniversaryPage.vue"),
			meta: {
				requiresAuth: true,
				title: "ABUVI | 50 Aniversario"
			}
		},
		{
			path: "/profile",
			name: "profile",
			component: () => import("@/views/ProfilePage.vue"),
			meta: {
				requiresAuth: true,
				title: "ABUVI | Mi Perfil"
			}
		},
		{
			path: "/admin",
			name: "admin",
			component: () => import("@/views/AdminPage.vue"),
			meta: {
				requiresAuth: true,
				requiresBoard: true,
				title: "ABUVI | Administración"
			}
		},
		{
			path: "/family-unit",
			name: "family-unit",
			component: () => import("@/views/FamilyUnitPage.vue"),
			meta: {
				requiresAuth: true,
				title: "ABUVI | Mi Unidad Familiar"
			}
		},
		{
			path: "/family-unit/me",
			redirect: "/family-unit"
		},

		// Camp Management routes (Board only)
		{
			path: "/camps/locations",
			name: "camp-locations",
			component: () => import("@/views/camps/CampLocationsPage.vue"),
			meta: {
				title: "ABUVI | Ubicaciones de Campamento",
				requiresAuth: true,
				requiresBoard: true
			}
		},
		{
			path: "/camps/locations/:id",
			name: "camp-location-detail",
			component: () => import("@/views/camps/CampLocationDetailPage.vue"),
			meta: {
				title: "ABUVI | Detalles de Ubicación",
				requiresAuth: true,
				requiresBoard: true
			}
		},

		// Camp Editions Management (Board only)
		{
			path: "/camps/editions",
			name: "camp-editions",
			component: () => import("@/views/camps/CampEditionsPage.vue"),
			meta: {
				title: "ABUVI | Gestión de Ediciones",
				requiresAuth: true,
				requiresBoard: true
			}
		},
		// Camp Edition Detail (authenticated users)
		{
			path: "/camps/editions/:id",
			name: "camp-edition-detail",
			component: () => import("@/views/camps/CampEditionDetailPage.vue"),
			meta: {
				title: "ABUVI | Detalle de Edición",
				requiresAuth: true
			}
		},

		// Registration routes — authenticated members
		// IMPORTANT: /registrations/new/:editionId MUST be before /registrations/:id
		{
			path: "/registrations",
			name: "registrations",
			component: () => import("@/views/registrations/RegistrationsPage.vue"),
			meta: {
				requiresAuth: true,
				title: "ABUVI | Mis Inscripciones"
			}
		},
		{
			path: "/registrations/new/:editionId",
			name: "registration-new",
			component: () => import("@/views/registrations/RegisterForCampPage.vue"),
			meta: {
				requiresAuth: true,
				title: "ABUVI | Nueva Inscripción"
			}
		},
		{
			path: "/registrations/:id",
			name: "registration-detail",
			component: () => import("@/views/registrations/RegistrationDetailPage.vue"),
			meta: {
				requiresAuth: true,
				title: "ABUVI | Detalle de Inscripción"
			}
		},

		// Legacy user management routes — redirect to admin panel
		{
			path: "/users",
			redirect: "/admin"
		},
		{
			path: "/users/:id",
			redirect: "/admin"
		},

		// Public legal routes — no auth required
		{
			path: "/legal/notice",
			name: "legal-notice",
			component: () => import("@/views/legal/NoticeLegalPage.vue"),
			meta: { title: "ABUVI | Aviso Legal", requiresAuth: false }
		},
		{
			path: "/legal/privacy",
			name: "legal-privacy",
			component: () => import("@/views/legal/PrivacyPage.vue"),
			meta: { title: "ABUVI | Política de Privacidad", requiresAuth: false }
		},
		{
			path: "/legal/bylaws",
			name: "legal-bylaws",
			component: () => import("@/views/legal/BylawsPage.vue"),
			meta: { title: "ABUVI | Estatutos", requiresAuth: false }
		},
		{
			path: "/legal/transparency",
			name: "legal-transparency",
			component: () => import("@/views/legal/TransparencyPage.vue"),
			meta: { title: "ABUVI | Transparencia", requiresAuth: false }
		},

		// Password reset routes — public, no auth required
		{
			path: "/forgot-password",
			name: "forgot-password",
			component: () => import("@/views/ForgotPasswordPage.vue"),
			meta: { requiresAuth: false, title: "ABUVI | Recuperar Contraseña" }
		},
		{
			path: "/reset-password",
			name: "reset-password",
			component: () => import("@/views/ResetPasswordPage.vue"),
			meta: { requiresAuth: false, title: "ABUVI | Nueva Contraseña" }
		},

		// Legacy login/register routes - redirect to landing
		{
			path: "/login",
			redirect: "/"
		},
		{
			path: "/register",
			redirect: "/"
		}
	]
});

// Route guard for authentication
router.beforeEach((to, from, next) => {
	const auth = useAuthStore();

	// Update document title
	document.title = (to.meta.title as string) || "ABUVI";

	// Check if route requires authentication
	if (to.meta.requiresAuth && !auth.isAuthenticated) {
		// Redirect to landing page with redirect URL
		next({ path: "/", query: { redirect: to.fullPath } });
		return;
	}

	// Check if route requires admin role
	if (to.meta.requiresAdmin && !auth.isAdmin) {
		// Redirect to home if not admin
		next({ path: "/home" });
		return;
	}

	// Check if route requires board role (Admin or Board)
	if (to.meta.requiresBoard && !auth.isBoard) {
		// Redirect to home page
		next({ path: "/home" });
		return;
	}

	// Redirect authenticated users from landing page to home
	if (to.path === "/" && auth.isAuthenticated) {
		const redirect = to.query.redirect as string | undefined;
		next(redirect || "/home");
		return;
	}

	next();
});

export default router;
