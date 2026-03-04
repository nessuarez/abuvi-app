<!-- TODO: Replace with officially approved legal content before publishing -->
<script setup lang="ts">
import LegalPageLayout from '@/components/legal/LegalPageLayout.vue'

const tocEntries = [
  { id: 'identificacion', label: '1. Identificación del Titular', level: 1 as const },
  { id: 'objeto', label: '2. Objeto del Sitio Web', level: 1 as const },
  { id: 'propiedad-intelectual', label: '3. Propiedad Intelectual e Industrial', level: 1 as const },
  { id: 'responsabilidad', label: '4. Responsabilidad', level: 1 as const },
  { id: 'legislacion', label: '5. Legislación Aplicable y Jurisdicción', level: 1 as const }
]
</script>

<template>
  <LegalPageLayout
    title="Aviso Legal"
    last-updated="Febrero 2026"
    :toc-entries="tocEntries"
    :show-toc="true"
  >
    <p class="mb-8 leading-relaxed text-gray-600">
      En cumplimiento de la Ley 34/2002, de 11 de julio, de Servicios de la Sociedad de la
      Información y de Comercio Electrónico (LSSI), se informa a los usuarios de los datos
      identificativos de la entidad titular del sitio web <strong>abuvi.org</strong>.
    </p>

    <!-- Section 1: Identification -->
    <section id="identificacion" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">1. Identificación del Titular</h2>
      <dl class="divide-y divide-gray-100 rounded-lg border border-gray-200">
        <div class="grid grid-cols-1 gap-1 px-4 py-3 sm:grid-cols-3">
          <dt class="text-sm font-semibold text-gray-700">Denominación social</dt>
          <dd class="text-sm text-gray-600 sm:col-span-2">
            Asociación ABUVI (Amigos de la Buena Vida)
          </dd>
        </div>
        <div class="grid grid-cols-1 gap-1 px-4 py-3 sm:grid-cols-3">
          <dt class="text-sm font-semibold text-gray-700">Forma jurídica</dt>
          <dd class="text-sm text-gray-600 sm:col-span-2">Asociación sin ánimo de lucro</dd>
        </div>
        <div class="grid grid-cols-1 gap-1 px-4 py-3 sm:grid-cols-3">
          <dt class="text-sm font-semibold text-gray-700">CIF</dt>
          <dd class="text-sm text-gray-600 sm:col-span-2">G-79013322</dd>
        </div>
        <div class="grid grid-cols-1 gap-1 px-4 py-3 sm:grid-cols-3">
          <dt class="text-sm font-semibold text-gray-700">Domicilio social</dt>
          <dd class="text-sm text-gray-600 sm:col-span-2">C/BUTRÓN 27, 28022 Madrid</dd>
        </div>
        <div class="grid grid-cols-1 gap-1 px-4 py-3 sm:grid-cols-3">
          <dt class="text-sm font-semibold text-gray-700">Registro</dt>
          <dd class="text-sm text-gray-600 sm:col-span-2">[Pendiente: número y registro oficial]</dd>
        </div>
        <div class="grid grid-cols-1 gap-1 px-4 py-3 sm:grid-cols-3">
          <dt class="text-sm font-semibold text-gray-700">Correo electrónico</dt>
          <dd class="text-sm text-gray-600 sm:col-span-2">
            <a href="mailto:junta.abuvi@gmail.com" class="text-primary-600 hover:underline">
              junta.abuvi@gmail.com
            </a>
          </dd>
        </div>
        <div class="grid grid-cols-1 gap-1 px-4 py-3 sm:grid-cols-3">
          <dt class="text-sm font-semibold text-gray-700">Nombre de dominio</dt>
          <dd class="text-sm text-gray-600 sm:col-span-2">abuvi.org</dd>
        </div>
        <div class="grid grid-cols-1 gap-1 px-4 py-3 sm:grid-cols-3">
          <dt class="text-sm font-semibold text-gray-700">Proveedor de hosting</dt>
          <dd class="text-sm text-gray-600 sm:col-span-2">
            NAMECHEAP INC. (
            <a
              href="https://www.namecheap.com"
              target="_blank"
              rel="noopener noreferrer"
              class="text-primary-600 hover:underline"
            >
              www.namecheap.com
            </a>
            )
          </dd>
        </div>
      </dl>
    </section>

    <!-- Section 2: Purpose -->
    <section id="objeto" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">2. Objeto del Sitio Web</h2>
      <p class="leading-relaxed text-gray-700">
        El presente sitio web tiene como objeto proporcionar información y servicios a los socios
        y participantes de los campamentos organizados por la Asociación ABUVI. A través de esta
        plataforma, los usuarios pueden acceder a información sobre actividades, campamentos,
        gestionar su perfil de socio y acceder a documentación organizativa.
      </p>
      <p class="mt-4 leading-relaxed text-gray-700">
        El acceso y uso del sitio web implica la aceptación plena de las condiciones indicadas
        en este Aviso Legal. La asociación se reserva el derecho a modificar el contenido del
        sitio web sin previo aviso.
      </p>
    </section>

    <!-- Section 3: Intellectual Property -->
    <section id="propiedad-intelectual" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">
        3. Propiedad Intelectual e Industrial
      </h2>
      <p class="leading-relaxed text-gray-700">
        Todos los contenidos del sitio web, incluyendo textos, fotografías, gráficos, imágenes,
        iconos, tecnología, software y demás contenidos audiovisuales o sonoros, así como su
        diseño gráfico y códigos fuente, son propiedad de la Asociación ABUVI o de terceros
        que han autorizado su uso, y están protegidos por las leyes de propiedad intelectual e
        industrial vigentes en España y en la Unión Europea.
      </p>
      <p class="mt-4 leading-relaxed text-gray-700">
        Queda expresamente prohibida la reproducción, distribución, comunicación pública y
        transformación de la totalidad o parte de los contenidos de este sitio web, con fines
        comerciales, en cualquier soporte y por cualquier medio técnico, sin la autorización
        expresa de la Asociación ABUVI. El logotipo, nombre comercial y signos distintivos de
        ABUVI son titularidad de la asociación.
      </p>
      <p class="mt-4 leading-relaxed text-gray-700">
        El código fuente de esta aplicación está disponible bajo licencia Apache 2.0.
      </p>
    </section>

    <!-- Section 4: Liability -->
    <section id="responsabilidad" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">4. Responsabilidad</h2>
      <p class="leading-relaxed text-gray-700">
        La Asociación ABUVI no se hace responsable de los daños y perjuicios de cualquier
        naturaleza que pudieran derivarse de la falta de exactitud, exhaustividad, actualidad,
        así como de errores u omisiones en los contenidos publicados en este sitio web.
      </p>
      <p class="mt-4 leading-relaxed text-gray-700">
        El sitio web puede contener enlaces a otros sitios web de terceros. La asociación no
        controla ni se responsabiliza de los contenidos, políticas de privacidad o prácticas
        de los sitios web de terceros a los que se enlaza.
      </p>
      <p class="mt-4 leading-relaxed text-gray-700">
        La disponibilidad del servicio está sujeta a interrupciones por mantenimiento técnico,
        actualizaciones o causas de fuerza mayor, sin que ello genere responsabilidad alguna
        para la asociación.
      </p>
    </section>

    <!-- Section 5: Applicable Law -->
    <section id="legislacion" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">
        5. Legislación Aplicable y Jurisdicción
      </h2>
      <p class="leading-relaxed text-gray-700">
        El presente Aviso Legal se rige en todos y cada uno de sus extremos por la ley española.
        Las partes, con renuncia expresa a cualquier otro fuero que pudiera corresponderles, se
        someten a los Juzgados y Tribunales de Madrid para la resolución de cualquier conflicto
        que pudiera derivarse del acceso o uso del sitio web.
      </p>
      <p class="mt-4 leading-relaxed text-gray-700">
        En caso de que cualquier estipulación del presente Aviso Legal resultara inexigible o
        nula en virtud de la legislación aplicable, dicha inexigibilidad o nulidad no afectará
        a la validez o exigibilidad del resto de las estipulaciones.
      </p>
    </section>
  </LegalPageLayout>
</template>
