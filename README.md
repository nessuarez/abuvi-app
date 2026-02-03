# **ABUVI Web Platform**

A modern, high-performance web platform for the **ABUVI Association**, designed to manage memberships, camp registrations, and preserve the association's 50-year historical archive.

## **🚀 Spec-Driven Development (SDD)**

This project is a showcase of **Spec-Driven Development** assisted by AI Agents (Google Antigravity, Cursor, or Windsurf). Unlike traditional development, here the **Specification is the Source of Truth**.

### **How it works:**

1. **Specs First**: No code is written before defining the feature or standard in `ai-specs/specs/`.
2. **Context-Aware Agents**: We use `.mdc` files (Model Context Rules) to enforce architectural standards (Vertical Slices, Clean Code, Security) directly into the AI's reasoning engine.
3. **Verifiable Progress**: Each feature starts with a "Change Plan" in `ai-specs/changes/`, ensuring the AI follows a logical and documented execution path.
4. **AI Orchestration**: The developer acts as a **Software Architect**, guiding agents through specialized skills defined in the `.agent/` directory.

## **🛠 Tech Stack**

### **Backend**

* **Core**: .NET 9 (Minimal APIs)
* **Database**: PostgreSQL 16 + Entity Framework Core
* **AI/Data Science**: Python 3.12 integration via **CSnakes**
* **Architecture**: Vertical Slice Architecture

### **Frontend**

* **Framework**: Vue 3 (Composition API) + TypeScript
* **UI Toolkit**: PrimeVue + Tailwind CSS
* **Build Tool**: Vite

### **Infrastructure & Integration**

* **Containerization**: Docker & Docker Compose
* **Payments**: Redsys Integration (SHA-256)
* **AI Tooling**: Google Antigravity / Cursor

## **📁 Project Structure**

```text
abuvi-app/                  # Root directory
├── .agent/                 # AI Skills and agent-specific configurations
├── ai-specs/               # SDD Core (The "Brain" of the project)
│   ├── specs/              # Architectural & Feature specifications (.mdc, .md)
│   └── changes/            # Execution plans for specific tasks (Change Plans)
├── src/
│   ├── Abuvi.API/          # .NET 9 Backend (Vertical Slices)
│   ├── Abuvi.Web/          # Vue 3 Frontend
│   └── Abuvi.Analysis/     # Python Data Analysis modules
├── docker-compose.yml      # Local development infrastructure
└── README.md               # You are here
```

## **⚙️ Getting Started**

### **Prerequisites**

* .NET 9 SDK
* Node.js 20+
* Python 3.12
* Docker Desktop

### **Installation**

1. **Clone the repository**:

   ```bash
   git clone https://github.com/your-user/abuvi-app.git
   ```

2. **Setup the infrastructure**:

   ```bash
   docker-compose up -d
   ```

3. **Initialize the Backend**:

   ```bash
   dotnet run --project src/Abuvi.API
   ```

4. **Initialize the Frontend**:

   ```bash
   cd src/Abuvi.Web && npm install && npm run dev
   ```

## **📄 License**

This project is licensed under the **Apache License 2.0** - see the [LICENSE](LICENSE) file for details. This license allows for community use and transparency while protecting the core architectural patterns and the non-profit organization's brand.

*Developed with ❤️ for ABUVI using Spec-Driven Development methodology.*
