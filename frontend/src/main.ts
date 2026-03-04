import { createApp } from "vue";
import { createPinia } from "pinia";
import * as Sentry from "@sentry/vue";
import PrimeVue from "primevue/config";
import ToastService from "primevue/toastservice";
import ConfirmationService from "primevue/confirmationservice";
import Tooltip from "primevue/tooltip";
import App from "./App.vue";
import router from "./router";
import Aura from "@primeuix/themes/aura";

import "primeicons/primeicons.css";
import "leaflet/dist/leaflet.css";
import "./assets/styles/global.css";
import "./assets/main.css";

const app = createApp(App);

// GlitchTip error tracking (Sentry-compatible)
const glitchtipDsn = import.meta.env.VITE_GLITCHTIP_DSN;
if (glitchtipDsn) {
	Sentry.init({
		app,
		dsn: glitchtipDsn,
		environment: import.meta.env.MODE,
		tracesSampleRate: 0,
		beforeSend(event) {
			if (event.request) {
				delete event.request.cookies;
				delete event.request.headers;
			}
			return event;
		},
	});
}

// Userback visual feedback widget
// Script is loaded via index.html (static tag for domain verification).
// Auth-aware init with user identity is done in App.vue after login.

app.use(createPinia());
app.use(router);
app.use(PrimeVue, {
	theme: {
		preset: Aura,
		options: {
			darkModeSelector: false // Disable dark mode
		}
	}
});
app.use(ToastService);
app.use(ConfirmationService);
app.directive("tooltip", Tooltip);

app.mount("#app");
