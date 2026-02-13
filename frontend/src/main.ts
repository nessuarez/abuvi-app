import { createApp } from "vue";
import { createPinia } from "pinia";
import PrimeVue from "primevue/config";
import ToastService from "primevue/toastservice";
import App from "./App.vue";
import router from "./router";
import Aura from "@primeuix/themes/aura";

import "primeicons/primeicons.css";
import "./assets/styles/global.css";
import "./assets/main.css";

const app = createApp(App);

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

app.mount("#app");
