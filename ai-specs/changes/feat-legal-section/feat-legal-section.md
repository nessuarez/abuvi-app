# Legal Section - Frontend Implementation

## Overview

This document specifies the implementation of the Legal section for the ABUVI website, including four key legal pages that provide transparency, legal compliance, and organizational information to users. These pages are linked from the footer and must comply with Spanish and European legal requirements (GDPR, LOPD, LSSI).

## User Story

**As a** visitor or member of the ABUVI website
**I want** to access clear legal information and organizational documents
**So that** I can understand my rights, the organization's legal framework, data privacy policies, and transparency commitments.

## Acceptance Criteria

- [ ] Four legal pages are created: Aviso Legal, Política de Privacidad, Estatutos, Transparencia
- [ ] All pages are accessible from the footer legal links
- [ ] Each page has a clear structure with proper headings and sections
- [ ] Content is formatted for readability with appropriate typography
- [ ] Pages are fully responsive across all devices
- [ ] Last updated date is displayed on each legal page
- [ ] Print-friendly styling is available for all legal documents
- [ ] All pages meet accessibility standards (WCAG 2.1 AA)
- [ ] SEO metadata is properly configured for each page
- [ ] Users can easily return to the main site from legal pages

---

## Legal Pages Overview

### 1. Aviso Legal (Legal Notice)

Mandatory page under Spanish law (LSSI - Ley de Servicios de la Sociedad de la Información) that identifies the organization and provides legal information about the website.

### 2. Política de Privacidad (Privacy Policy)

Required by GDPR and Spanish LOPD that explains how personal data is collected, processed, stored, and protected.

### 3. Estatutos (Bylaws/Statutes)

Organizational statutes that define ABUVI's structure, governance, objectives, and internal regulations.

---

## Page Specifications

### 1. Aviso Legal (Legal Notice)

#### Route

- URL: `/legal/aviso-legal` or `/aviso-legal`
- Component: `AvisoLegalPage.vue`

#### Required Sections

##### 1.1 Organization Identification

- **Full legal name**: Asociación ABUVI (Amigos de la BUena VIda)
- **Legal form**: Asociación (Non-profit Association)
- **CIF/NIF**: G-79013322
- **Registered address**: C/BUTRÓN 27, 28022 Madrid
- **Registration**: [Registry number and location]
- **Email**: <juntaabuvi@gmail.com>

##### 1.2 Website Information

- **Domain name**: abuvi.org
- **Hosting provider**: NAMECHEAP INC. (<www.namecheap.com>)
- **Purpose**: Information and services for members and camp participants

##### 1.3 Intellectual Property

- Copyright notice for website content
- Rights over ABUVI logo, images, and materials
- Conditions for content use and reproduction
- Attribution requirements

##### 1.4 Liability Disclaimer

- Accuracy of information disclaimer
- External links disclaimer
- Service availability disclaimer
- Force majeure clause

##### 1.5 Applicable Law and Jurisdiction

- Spanish law applicability
- Competent jurisdiction for disputes

#### Content Structure (Template)

```markdown
# Aviso Legal

**Última actualización:** [Date]

## 1. Identificación del Titular

En cumplimiento de la Ley 34/2002, de 11 de julio, de Servicios de la Sociedad de la Información y de Comercio Electrónico (LSSI), se informa a los usuarios de los datos identificativos de la entidad titular del sitio web:

- **Denominación social:** Asociación ABUVI (Amigos de la Buena Vida)
- **CIF:** [Number]
- **Domicilio social:** [Address]
- **Registro:** [Registry information]
- **Correo electrónico:** info@abuvi.org
- **Teléfono:** +34 600 000 000

## 2. Objeto

El presente sitio web tiene como objeto...

## 3. Propiedad Intelectual e Industrial

[Copyright and intellectual property terms]

## 4. Responsabilidad

[Liability disclaimers]

## 5. Legislación Aplicable y Jurisdicción

[Applicable law and jurisdiction]
```

---

### 2. Política de Privacidad (Privacy Policy)

#### Route

- URL: `/legal/privacidad` or `/privacidad`
- Component: `PrivacidadPage.vue`

#### Required Sections (GDPR Compliance)

##### 2.1 Data Controller Information

- Organization name and contact details
- Data Protection Officer (DPO) contact if applicable

##### 2.2 What Data We Collect

- Personal identification data (name, email, phone)
- Camp registration data (age, medical information, emergency contacts)
- Membership data
- Payment information
- Website usage data (cookies, analytics)

##### 2.3 Legal Basis for Processing

- Consent
- Contract execution (camp registration, membership)
- Legal obligation
- Legitimate interest

##### 2.4 Purpose of Data Collection

- Camp registration and management
- Membership management
- Communication and newsletters
- Service improvement
- Legal compliance

##### 2.5 Data Retention

- How long data is kept
- Criteria for retention periods
- Deletion procedures

##### 2.6 Data Recipients

- Who has access to data (staff, volunteers)
- Third-party processors (payment providers, email services)
- International data transfers (if any)

##### 2.7 User Rights (GDPR)

- Right to access
- Right to rectification
- Right to erasure ("right to be forgotten")
- Right to restriction of processing
- Right to data portability
- Right to object
- Right to withdraw consent
- How to exercise rights

##### 2.8 Data Security

- Security measures implemented
- Breach notification procedures

##### 2.9 Cookies Policy

- Types of cookies used
- Purpose of cookies
- How to manage cookies
- Link to detailed cookie policy if separate

##### 2.10 Updates to Privacy Policy

- How users will be notified of changes

##### 2.11 Contact and Complaints

- How to contact about privacy concerns
- Right to file complaint with Spanish Data Protection Agency (AEPD)

#### Content Structure (Template)

```markdown
# Política de Privacidad

**Última actualización:** [Date]

## 1. Responsable del Tratamiento

Asociación ABUVI
- **Correo electrónico:** juntaabuvi@gmail.com
- **Dirección:** C/BUTRÓN 27, 28022 Madrid

## 2. Datos Personales que Recopilamos

[Details of collected data]

## 3. Base Legal y Finalidad del Tratamiento

[Legal basis and purposes]

## 4. Conservación de Datos

[Retention policies]

## 5. Destinatarios de los Datos

[Data recipients]

## 6. Derechos de los Usuarios

De conformidad con el RGPD y la LOPDGDD, los usuarios tienen derecho a:
- Acceder a sus datos personales
- Rectificar datos inexactos
- Solicitar la supresión
- [etc.]

Para ejercer estos derechos, puede contactar en: info@abuvi.org

## 7. Medidas de Seguridad

[Security measures]

## 8. Cookies

[Cookie policy summary]

## 9. Contacto y Reclamaciones

Para cualquier consulta sobre privacidad: info@abuvi.org

También puede presentar una reclamación ante la Agencia Española de Protección de Datos (www.aepd.es)
```

---

### 3. Estatutos (Bylaws/Statutes)

#### Route

- URL: `/legal/estatutos` or `/estatutos`
- Component: `EstatutosPage.vue`

#### Required Sections (Typical Spanish Association Statutes)

##### 3.1 General Provisions

- Name and nature of the association
- Registered office
- Territorial scope
- Duration

##### 3.2 Objectives and Activities

- Association's purpose and goals
- Activities to achieve objectives
- Non-profit nature

##### 3.3 Membership

- Types of members (founding, regular, honorary)
- Admission requirements and procedures
- Rights and duties of members
- Loss of membership (resignation, expulsion)
- Membership fees

##### 3.4 Organizational Structure

- **General Assembly:**
  - Composition and powers
  - Types of assemblies (ordinary, extraordinary)
  - Convocation and quorum requirements
  - Voting procedures
- **Board of Directors:**
  - Composition (President, Secretary, Treasurer, etc.)
  - Election and term of office
  - Powers and responsibilities
  - Meeting procedures

##### 3.5 Financial Regime

- Economic resources
- Budget and accounts
- Fiscal year

##### 3.6 Amendment of Statutes

- Procedure for amendments
- Required majorities

##### 3.7 Dissolution

- Causes for dissolution
- Liquidation procedure
- Destination of remaining assets

#### Implementation Notes

- The statutes content should be provided by ABUVI leadership
- Can be displayed as a long-form document with table of contents
- Consider downloadable PDF version
- Use expandable/collapsible sections for easier navigation

#### Content Structure

```markdown
# Estatutos de la Asociación ABUVI

**Aprobados en:** [Date]
**Última modificación:** [Date]

## Índice
1. [Disposiciones Generales](#disposiciones-generales)
2. [Fines y Actividades](#fines)
3. [Socios](#socios)
4. [Órganos de Gobierno](#organos)
5. [Régimen Económico](#regimen-economico)
6. [Modificación de Estatutos](#modificacion)
7. [Disolución](#disolucion)

---

## 1. Disposiciones Generales

### Artículo 1. Denominación
[Content]

### Artículo 2. Domicilio
[Content]

[Continue with all articles...]
```

---

### 4. Transparencia (Transparency)

#### Route

- URL: `/legal/transparencia` or `/transparencia`
- Component: `TransparenciaPage.vue`

#### Sections

##### 4.1 Who We Are

- Brief history of ABUVI
- Mission and vision
- Values and principles

##### 4.2 Governance Structure

- Board of Directors members (names, positions)
- Election process
- Meeting frequency
- Decision-making process

##### 4.3 Organizational Structure

- Organization chart
- Teams and committees
- Number of members
- Number of volunteers/staff

##### 4.4 Financial Transparency

- **Annual Budget:**
  - Income sources (membership fees, camp fees, donations, grants)
  - Expense breakdown (operations, camp, activities)
- **Annual Financial Report:**
  - Balance sheet summary
  - Downloadable full report (PDF)
- **Audit Information** (if applicable)

##### 4.5 Activities Report

- Annual summary of activities
- Number of camp participants
- Events organized
- Projects and initiatives
- Social impact metrics

##### 4.6 Documents and Reports

- Downloadable annual reports
- Meeting minutes (general assemblies)
- Activity reports
- Financial statements

##### 4.7 Code of Conduct

- Ethical principles
- Conflict of interest policy
- Complaint procedures

##### 4.8 Contact for Transparency

- Email for transparency inquiries
- Response time commitment

#### Content Structure

```markdown
# Transparencia

**Última actualización:** [Date]

## Quiénes Somos

ABUVI (Amigos de la Buena Vida) es una asociación sin ánimo de lucro fundada en 1976...

## Estructura de Gobierno

### Junta Directiva (2025-2026)
- **Presidente/a:** [Name]
- **Secretario/a:** [Name]
- **Tesorero/a:** [Name]
- **Vocales:** [Names]

## Información Financiera

### Presupuesto 2026

**Ingresos**
- Cuotas de socios: XX%
- Campamento: XX%
- Subvenciones: XX%

**Gastos**
- Operaciones: XX%
- Campamento: XX%
- Actividades: XX%

[Descarga el presupuesto completo (PDF)]

### Cuentas Anuales 2025
[Descarga el informe financiero (PDF)]

## Memoria de Actividades

[Summary of annual activities]

## Documentos

- [Informe Anual 2025 (PDF)]
- [Cuentas Anuales 2025 (PDF)]
- [Acta Asamblea General 2025 (PDF)]

## Contacto Transparencia

Para consultas sobre transparencia: transparencia@abuvi.org
```

---

## Technical Implementation

### Shared Component Architecture

#### LegalPageLayout Component

Create a reusable layout component for all legal pages:

```vue
<template>
  <div class="legal-page">
    <div class="legal-header">
      <h1>{{ title }}</h1>
      <p class="last-updated">Última actualización: {{ lastUpdated }}</p>
    </div>

    <div class="legal-content">
      <aside class="table-of-contents" v-if="showToc">
        <!-- Auto-generated TOC -->
      </aside>

      <main class="legal-body">
        <slot />
      </main>
    </div>

    <div class="legal-actions">
      <button @click="printPage">Imprimir / Descargar PDF</button>
      <router-link to="/">Volver al inicio</router-link>
    </div>
  </div>
</template>
```

**Props:**

- `title`: Page title
- `lastUpdated`: Last update date
- `showToc`: Whether to show table of contents (default: true)

---

### File Structure

```
src/
├── views/
│   └── legal/
│       ├── AvisoLegalPage.vue
│       ├── PrivacidadPage.vue
│       ├── EstatutosPage.vue
│       └── TransparenciaPage.vue
├── components/
│   └── legal/
│       ├── LegalPageLayout.vue
│       ├── TableOfContents.vue
│       └── DownloadableDocument.vue
├── router/
│   └── index.ts (add legal routes)
└── assets/
    └── documents/
        ├── estatutos-abuvi-2026.pdf
        ├── informe-anual-2025.pdf
        ├── cuentas-anuales-2025.pdf
        └── acta-asamblea-2025.pdf
```

---

### Router Configuration

```typescript
// router/index.ts
const routes = [
  // ... existing routes
  {
    path: '/legal',
    children: [
      {
        path: 'aviso-legal',
        name: 'AvisoLegal',
        component: () => import('@/views/legal/AvisoLegalPage.vue'),
        meta: { title: 'Aviso Legal - ABUVI' }
      },
      {
        path: 'privacidad',
        name: 'Privacidad',
        component: () => import('@/views/legal/PrivacidadPage.vue'),
        meta: { title: 'Política de Privacidad - ABUVI' }
      },
      {
        path: 'estatutos',
        name: 'Estatutos',
        component: () => import('@/views/legal/EstatutosPage.vue'),
        meta: { title: 'Estatutos - ABUVI' }
      },
      {
        path: 'transparencia',
        name: 'Transparencia',
        component: () => import('@/views/legal/TransparenciaPage.vue'),
        meta: { title: 'Transparencia - ABUVI' }
      }
    ]
  }
]
```

---

### Styling Considerations

#### Typography

- Use readable serif font for long-form legal content (e.g., Georgia, Times)
- Or stick with sans-serif but increase line-height to 1.7-1.8
- Font size: 16px minimum for body text
- Generous line spacing for readability

#### Layout

- Max content width: 800px for optimal reading
- Generous margins and padding
- Clear heading hierarchy with distinct sizes
- Use lists and numbered sections for legal content

#### Print Styles

Create print-specific CSS for clean PDF generation:

```css
@media print {
  .legal-page {
    max-width: 100%;
  }

  .legal-header,
  .legal-content {
    page-break-inside: avoid;
  }

  nav, footer, .legal-actions {
    display: none;
  }

  a {
    color: black;
    text-decoration: underline;
  }

  /* Ensure links show URL when printed */
  a[href]:after {
    content: " (" attr(href) ")";
  }
}
```

---

## Implementation Steps

### Phase 1: Setup and Structure (Day 1)

1. [ ] Create `views/legal/` directory
2. [ ] Create `components/legal/` directory
3. [ ] Create `LegalPageLayout.vue` component
4. [ ] Add legal routes to router configuration
5. [ ] Update footer links to point to legal routes (remove `#`)

### Phase 2: Content Collection (Day 1-2)

1. [ ] Gather organization information for Aviso Legal
2. [ ] Draft or obtain Privacy Policy content (consider legal consultation)
3. [ ] Obtain official Estatutos document from organization
4. [ ] Gather transparency data (board members, financial reports)
5. [ ] Collect downloadable documents (PDFs)

### Phase 3: Page Implementation (Day 2-3)

1. [ ] Create AvisoLegalPage.vue with content
2. [ ] Create PrivacidadPage.vue with GDPR-compliant content
3. [ ] Create EstatutosPage.vue with statutes
4. [ ] Create TransparenciaPage.vue with transparency info

### Phase 4: Features and Polish (Day 3-4)

1. [ ] Implement TableOfContents component for navigation
2. [ ] Add print functionality to each page
3. [ ] Add downloadable PDFs for key documents
4. [ ] Implement "last updated" date display
5. [ ] Add breadcrumb navigation if applicable

### Phase 5: Testing and Compliance (Day 4-5)

1. [ ] Test all legal page routes and navigation
2. [ ] Verify content accuracy and legal compliance
3. [ ] Test print functionality across browsers
4. [ ] Verify accessibility (screen readers, keyboard navigation)
5. [ ] Test responsive design on mobile devices
6. [ ] Check SEO metadata for all pages
7. [ ] Validate content with legal advisor if possible

### Phase 6: Documentation (Day 5)

1. [ ] Document how to update legal content
2. [ ] Create process for updating "last updated" dates
3. [ ] Document where to store new PDF documents
4. [ ] Update project documentation

---

## Non-Functional Requirements

### Legal Compliance

- **Spanish Law (LSSI)**: Aviso Legal must comply with LSSI requirements
- **GDPR**: Privacy Policy must be GDPR-compliant
- **LOPD**: Comply with Spanish data protection law
- **Transparency**: Follow good governance practices for associations

### Content Management

- Easy to update content without developer intervention
- Version control for legal documents
- Clear audit trail of changes to legal content

### Accessibility (WCAG 2.1 AA)

- All legal content must be accessible to screen readers
- Proper heading hierarchy for document structure
- High contrast text for readability
- Keyboard navigation support
- Downloadable PDFs should be accessible (tagged PDFs)

### SEO

- Proper meta titles and descriptions
- Semantic HTML structure
- No indexing restrictions (legal pages should be indexed)

### Performance

- Fast page load times
- Optimize PDF file sizes
- Lazy-load PDFs only when requested

### Internationalization (Future)

- Structure content to support future English translations
- Use i18n-ready component structure

---

## Content Responsibility

### Content Ownership

- **Aviso Legal**: Legal team or board
- **Privacidad**: Data Protection Officer or legal advisor
- **Estatutos**: Board of Directors (official document)
- **Transparencia**: Treasurer and board

### Update Frequency

- **Aviso Legal**: Review annually or when org info changes
- **Privacidad**: Review annually or when data practices change
- **Estatutos**: Update only when officially amended
- **Transparencia**: Update quarterly or annually

### Approval Process

1. Draft content prepared by responsible party
2. Legal review (especially for Aviso Legal and Privacidad)
3. Board approval
4. Publication on website
5. Update "last updated" date

---

## Acceptance Testing Checklist

### Content

- [ ] All organization information is accurate
- [ ] Privacy policy covers all data processing activities
- [ ] Estatutos match official registered version
- [ ] Transparency information is current and complete
- [ ] All contact information is correct

### Functionality

- [ ] All legal pages load correctly
- [ ] Footer links navigate to correct legal pages
- [ ] Table of contents anchors work correctly
- [ ] Print functionality works in all browsers
- [ ] PDF downloads work correctly
- [ ] "Return to home" link functions properly

### Legal Compliance

- [ ] Aviso Legal meets LSSI requirements
- [ ] Privacy Policy is GDPR-compliant
- [ ] All required GDPR user rights are documented
- [ ] Contact information for exercising rights is clear
- [ ] Cookie policy is included or referenced

### Design & UX

- [ ] Pages are readable on all devices
- [ ] Typography is comfortable for long-form reading
- [ ] Print layout is clean and professional
- [ ] Last updated dates are visible
- [ ] Navigation is intuitive

### Accessibility

- [ ] Proper heading hierarchy (H1 → H2 → H3)
- [ ] All links are descriptive
- [ ] Color contrast meets WCAG AA
- [ ] Keyboard navigation works
- [ ] Screen reader tested

### SEO

- [ ] Page titles are descriptive
- [ ] Meta descriptions are present
- [ ] Canonical URLs are set
- [ ] No duplicate content issues

---

## Future Enhancements

### Phase 2 (Optional)

- [ ] Cookie consent banner integration
- [ ] User consent management system
- [ ] Downloadable data export for users (GDPR right to portability)
- [ ] Automated privacy request form
- [ ] Multi-language support (English)
- [ ] Interactive organization chart for transparency
- [ ] Financial data visualizations (charts, graphs)
- [ ] Searchable document archive

---

## Dependencies

### External Services

- Legal advisor consultation (recommended for Privacy Policy)
- PDF hosting solution (can use static files initially)

### NPM Packages (if needed)

- `jspdf` or `html2pdf` for client-side PDF generation (optional)
- Markdown parser if content is stored in Markdown

### Required Documents

- Official Estatutos (from organization registry)
- CIF and registration information
- Board member information
- Financial reports and budgets

---

## Related Documentation

- [Layout - Main Page Structure](../feat-layout-frontend/layout-ppal_enriched.md)
- [Base Standards](../../specs/base-standards.md)
- GDPR Resources: <https://gdpr.eu/>
- Spanish AEPD: <https://www.aepd.es/>
- LSSI Information: <https://www.boe.es/buscar/act.php?id=BOE-A-2002-13758>

---

## Notes

- **Legal Review**: It's highly recommended to have Privacy Policy and Aviso Legal reviewed by a legal professional
- **Regular Updates**: Legal pages should be reviewed at least annually
- **Member Communication**: When Privacy Policy changes significantly, notify members
- **Backup**: Keep backups of all legal documents and previous versions
- **Dates**: Always update "last updated" date when content changes

---

## Success Metrics

- All legal pages accessible and functional
- Zero broken links from footer
- Legal compliance verified by advisor
- Positive feedback from members on transparency
- No accessibility violations
- Fast page load times (< 2 seconds)
- PDF downloads working reliably
