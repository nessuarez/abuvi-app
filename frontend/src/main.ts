import { createApp } from "vue";
import { createPinia } from "pinia";
import PrimeVue from "primevue/config";
import App from "./App.vue";
import router from "./router";

// Note: PrimeVue CSS imports commented out due to Vite build issues
// Can be added via CDN in index.html or with proper Vite configuration
// import 'primevue/resources/themes/lara-light-blue/theme.css'
import "primeicons/primeicons.css";
import "./assets/main.css";

const app = createApp(App);

app.use(createPinia());
app.use(router);
app.use(PrimeVue);

app.mount("#app");
