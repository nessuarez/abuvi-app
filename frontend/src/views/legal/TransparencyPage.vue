<!-- TODO: Replace placeholder data with real board members, financial data, and activity reports -->
<script setup lang="ts">
import LegalPageLayout from '@/components/legal/LegalPageLayout.vue'
import DataTable from 'primevue/datatable'
import Column from 'primevue/column'
import Card from 'primevue/card'
import Button from 'primevue/button'

const tocEntries = [
  { id: 'quienes-somos', label: 'Quiénes Somos', level: 1 as const },
  { id: 'estructura-gobierno', label: 'Estructura de Gobierno', level: 1 as const },
  { id: 'estructura-organizativa', label: 'Estructura Organizativa', level: 1 as const },
  { id: 'informacion-financiera', label: 'Información Financiera', level: 1 as const },
  { id: 'memoria-actividades', label: 'Memoria de Actividades', level: 1 as const },
  { id: 'documentos', label: 'Documentos y Memorias', level: 1 as const },
  { id: 'contacto', label: 'Contacto Transparencia', level: 1 as const }
]

interface BoardMember {
  position: string
  name: string
  term: string
}

const boardMembers: BoardMember[] = [
  { position: 'Presidente/a', name: '[Pendiente]', term: '2025-2026' },
  { position: 'Vicepresidente/a', name: '[Pendiente]', term: '2025-2026' },
  { position: 'Secretario/a', name: '[Pendiente]', term: '2025-2026' },
  { position: 'Tesorero/a', name: '[Pendiente]', term: '2025-2026' },
  { position: 'Vocal 1', name: '[Pendiente]', term: '2025-2026' },
  { position: 'Vocal 2', name: '[Pendiente]', term: '2025-2026' }
]

interface Document {
  title: string
  year: number
  type: string
}

const documents: Document[] = [
  { title: 'Informe Anual de Actividades', year: 2025, type: 'Memoria' },
  { title: 'Cuentas Anuales', year: 2025, type: 'Financiero' },
  { title: 'Acta Asamblea General Ordinaria', year: 2025, type: 'Acta' },
  { title: 'Presupuesto Aprobado', year: 2026, type: 'Financiero' }
]

const stats = [
  { label: 'Socios', value: '[Pendiente]', icon: 'pi pi-users', description: 'Socios activos' },
  {
    label: 'Años de historia',
    value: '50+',
    icon: 'pi pi-calendar',
    description: 'Desde 1976'
  },
  {
    label: 'Campistas 2025',
    value: '[Pendiente]',
    icon: 'pi pi-sun',
    description: 'Participantes en el campamento'
  }
]
</script>

<template>
  <LegalPageLayout
    title="Transparencia"
    last-updated="Febrero 2026"
    :toc-entries="tocEntries"
    :show-toc="true"
  >
    <!-- Section: Who we are -->
    <section id="quienes-somos" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">Quiénes Somos</h2>
      <p class="leading-relaxed text-gray-700">
        ABUVI (Amigos de la Buena Vida) es una asociación sin ánimo de lucro fundada en 1976.
        Nació como un grupo de amigos con el deseo de compartir momentos de convivencia,
        amistad y contacto con la naturaleza. A lo largo de más de cinco décadas, hemos
        organizado campamentos anuales que reúnen a familias y amigos de generaciones diversas.
      </p>
      <p class="mt-4 leading-relaxed text-gray-700">
        <strong>Misión:</strong> Promover la amistad, la convivencia familiar y el contacto
        con la naturaleza a través de actividades organizadas con valores de solidaridad,
        respeto y alegría.
      </p>
      <p class="mt-4 leading-relaxed text-gray-700">
        <strong>Visión:</strong> Ser una asociación referente en la organización de
        experiencias de convivencia que trascienden generaciones y refuerzan los lazos de
        amistad y familia.
      </p>
      <p class="mt-4 leading-relaxed text-gray-700">
        <strong>Valores:</strong> Amistad, solidaridad, respeto a la naturaleza, inclusión
        y alegría de vivir.
      </p>
    </section>

    <!-- Section: Governance -->
    <section id="estructura-gobierno" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">Estructura de Gobierno</h2>
      <p class="mb-4 leading-relaxed text-gray-700">
        La Asociación ABUVI se gobierna mediante la Asamblea General de socios y una Junta
        Directiva elegida democráticamente por un periodo de dos años.
      </p>

      <h3 class="mb-3 text-lg font-semibold text-gray-800">Junta Directiva 2025-2026</h3>
      <DataTable
        :value="boardMembers"
        striped-rows
        class="mb-4"
        :pt="{ root: { class: 'text-sm' } }"
      >
        <Column field="position" header="Cargo" />
        <Column field="name" header="Nombre" />
        <Column field="term" header="Mandato" />
      </DataTable>

      <div class="mt-4 rounded-lg bg-gray-50 p-4 text-sm text-gray-600">
        <p><strong>Proceso de elección:</strong> Elección democrática en Asamblea General Ordinaria.</p>
        <p class="mt-1"><strong>Frecuencia de reuniones:</strong> Mínimo trimestral.</p>
        <p class="mt-1">
          <strong>Toma de decisiones:</strong> Mayoría simple en Junta Directiva; mayoría
          cualificada en Asamblea General para asuntos estatutarios.
        </p>
      </div>
    </section>

    <!-- Section: Organizational structure -->
    <section id="estructura-organizativa" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">Estructura Organizativa</h2>
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <Card v-for="stat in stats" :key="stat.label">
          <template #content>
            <div class="flex flex-col items-center py-2 text-center">
              <i :class="stat.icon" class="mb-2 text-3xl text-primary-600" />
              <p class="text-2xl font-bold text-gray-900">{{ stat.value }}</p>
              <p class="mt-1 text-sm font-semibold text-gray-700">{{ stat.label }}</p>
              <p class="mt-0.5 text-xs text-gray-500">{{ stat.description }}</p>
            </div>
          </template>
        </Card>
      </div>
    </section>

    <!-- Section: Financial information -->
    <section id="informacion-financiera" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">Información Financiera</h2>

      <h3 class="mb-3 text-lg font-semibold text-gray-800">Presupuesto 2026</h3>
      <div class="mb-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div class="rounded-lg border border-gray-200 p-4">
          <h4 class="mb-3 text-sm font-semibold uppercase text-gray-500">Ingresos</h4>
          <ul class="space-y-2 text-sm text-gray-700">
            <li class="flex justify-between">
              <span>Cuotas de socios</span>
              <span class="font-medium">[XX%]</span>
            </li>
            <li class="flex justify-between">
              <span>Inscripciones campamento</span>
              <span class="font-medium">[XX%]</span>
            </li>
            <li class="flex justify-between">
              <span>Subvenciones</span>
              <span class="font-medium">[XX%]</span>
            </li>
            <li class="flex justify-between">
              <span>Otros</span>
              <span class="font-medium">[XX%]</span>
            </li>
          </ul>
        </div>
        <div class="rounded-lg border border-gray-200 p-4">
          <h4 class="mb-3 text-sm font-semibold uppercase text-gray-500">Gastos</h4>
          <ul class="space-y-2 text-sm text-gray-700">
            <li class="flex justify-between">
              <span>Campamento</span>
              <span class="font-medium">[XX%]</span>
            </li>
            <li class="flex justify-between">
              <span>Operaciones</span>
              <span class="font-medium">[XX%]</span>
            </li>
            <li class="flex justify-between">
              <span>Actividades</span>
              <span class="font-medium">[XX%]</span>
            </li>
            <li class="flex justify-between">
              <span>Administración</span>
              <span class="font-medium">[XX%]</span>
            </li>
          </ul>
        </div>
      </div>
      <Button
        label="Descargar presupuesto completo (PDF)"
        icon="pi pi-download"
        outlined
        size="small"
        disabled
        v-tooltip.top="'Próximamente disponible'"
      />

      <h3 class="mb-3 mt-8 text-lg font-semibold text-gray-800">Cuentas Anuales 2025</h3>
      <p class="mb-3 text-sm leading-relaxed text-gray-700">
        Las cuentas anuales del ejercicio 2025 fueron aprobadas en la Asamblea General
        Ordinaria de [fecha pendiente].
      </p>
      <Button
        label="Descargar cuentas anuales 2025 (PDF)"
        icon="pi pi-download"
        outlined
        size="small"
        disabled
        v-tooltip.top="'Próximamente disponible'"
      />
    </section>

    <!-- Section: Activity report -->
    <section id="memoria-actividades" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">Memoria de Actividades</h2>
      <p class="mb-4 leading-relaxed text-gray-700">
        Resumen de las principales actividades desarrolladas por la Asociación ABUVI
        durante el año 2025:
      </p>
      <ul class="list-disc space-y-3 pl-6 text-gray-700">
        <li>
          <strong>Campamento anual 2025:</strong> [Pendiente — número de participantes,
          fechas y lugar].
        </li>
        <li>
          <strong>Reuniones de socios:</strong> [Pendiente — número de reuniones y
          eventos organizados].
        </li>
        <li>
          <strong>Asamblea General Ordinaria:</strong> Celebrada el [pendiente]. Se
          aprobaron las cuentas del ejercicio anterior y el presupuesto para 2026.
        </li>
        <li>
          <strong>Celebración del 50 Aniversario:</strong> [Pendiente — descripción del
          acto conmemorativo].
        </li>
      </ul>
    </section>

    <!-- Section: Documents -->
    <section id="documentos" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">Documentos y Memorias</h2>
      <DataTable :value="documents" striped-rows :pt="{ root: { class: 'text-sm' } }">
        <Column field="title" header="Documento" />
        <Column field="year" header="Año" style="width: 80px" />
        <Column field="type" header="Tipo" style="width: 100px" />
        <Column header="Descarga" style="width: 120px">
          <template #body>
            <Button
              icon="pi pi-download"
              outlined
              size="small"
              disabled
              v-tooltip.top="'Próximamente disponible'"
              aria-label="Descargar documento"
            />
          </template>
        </Column>
      </DataTable>
    </section>

    <!-- Section: Contact -->
    <section id="contacto" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">Contacto Transparencia</h2>
      <p class="leading-relaxed text-gray-700">
        Para cualquier consulta relacionada con la transparencia de nuestra asociación —
        información financiera, composición de órganos de gobierno o documentos
        corporativos — puede contactar con nosotros:
      </p>
      <div class="mt-4 rounded-lg bg-gray-50 p-4">
        <p class="text-sm text-gray-700">
          <strong>Correo electrónico:</strong>
          <a
            href="mailto:juntaabuvi@gmail.com"
            class="ml-1 font-medium text-primary-600 hover:underline"
          >
            juntaabuvi@gmail.com
          </a>
        </p>
        <p class="mt-2 text-sm text-gray-500">
          Nos comprometemos a responder en un plazo máximo de 15 días hábiles.
        </p>
      </div>
    </section>
  </LegalPageLayout>
</template>
