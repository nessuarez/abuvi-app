import { defineConfig } from "cypress";

export default defineConfig({
	e2e: {
		projectId: "2oziu4",
		baseUrl: "http://localhost:5173",
		specPattern: "cypress/e2e/**/*.cy.{js,jsx,ts,tsx}",
		supportFile: "cypress/support/e2e.ts"
	}
});
