<!-- TODO: Replace with officially approved legal content — requires legal advisor review (GDPR) -->
<script setup lang="ts">
import LegalPageLayout from '@/components/legal/LegalPageLayout.vue'
import Message from 'primevue/message'

const tocEntries = [
  { id: 'responsable', label: '1. Responsable del Tratamiento', level: 1 as const },
  { id: 'datos-recopilados', label: '2. Datos Personales que Recopilamos', level: 1 as const },
  { id: 'base-legal', label: '3. Base Legal y Finalidad', level: 1 as const },
  { id: 'conservacion', label: '4. Conservación de Datos', level: 1 as const },
  { id: 'destinatarios', label: '5. Destinatarios de los Datos', level: 1 as const },
  { id: 'derechos', label: '6. Derechos de los Usuarios (RGPD)', level: 1 as const },
  { id: 'seguridad', label: '7. Medidas de Seguridad', level: 1 as const },
  { id: 'cookies', label: '8. Política de Cookies', level: 1 as const },
  { id: 'contacto', label: '9. Contacto y Reclamaciones', level: 1 as const }
]
</script>

<template>
  <LegalPageLayout title="Política de Privacidad" last-updated="Febrero 2026" :toc-entries="tocEntries"
    :show-toc="true">
    <p class="mb-8 leading-relaxed text-gray-600">
      En cumplimiento del Reglamento (UE) 2016/679, General de Protección de Datos (RGPD), y
      la Ley Orgánica 3/2018, de Protección de Datos Personales y garantía de los derechos
      digitales (LOPDGDD), le informamos sobre el tratamiento de sus datos personales.
    </p>

    <!-- Section 1 -->
    <section id="responsable" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">1. Responsable del Tratamiento</h2>
      <dl class="divide-y divide-gray-100 rounded-lg border border-gray-200">
        <div class="grid grid-cols-1 gap-1 px-4 py-3 sm:grid-cols-3">
          <dt class="text-sm font-semibold text-gray-700">Entidad</dt>
          <dd class="text-sm text-gray-600 sm:col-span-2">
            Asociación ABUVI (Amigos de la Buena Vida)
          </dd>
        </div>
        <div class="grid grid-cols-1 gap-1 px-4 py-3 sm:grid-cols-3">
          <dt class="text-sm font-semibold text-gray-700">CIF</dt>
          <dd class="text-sm text-gray-600 sm:col-span-2">G-79013322</dd>
        </div>
        <div class="grid grid-cols-1 gap-1 px-4 py-3 sm:grid-cols-3">
          <dt class="text-sm font-semibold text-gray-700">Dirección</dt>
          <dd class="text-sm text-gray-600 sm:col-span-2">C/BUTRÓN 27, 28022 Madrid</dd>
        </div>
        <div class="grid grid-cols-1 gap-1 px-4 py-3 sm:grid-cols-3">
          <dt class="text-sm font-semibold text-gray-700">Correo electrónico</dt>
          <dd class="text-sm text-gray-600 sm:col-span-2">
            <a href="mailto:junta.abuvi@gmail.com" class="text-primary-600 hover:underline">
              junta.abuvi@gmail.com
            </a>
          </dd>
        </div>
      </dl>
    </section>

    <!-- Section 2 -->
    <section id="datos-recopilados" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">2. Datos Personales que Recopilamos</h2>
      <p class="mb-4 leading-relaxed text-gray-700">
        Dependiendo de los servicios que utilice, podemos recopilar los siguientes datos:
      </p>
      <ul class="list-disc space-y-2 pl-6 text-gray-700">
        <li>
          <strong>Datos de identificación:</strong> nombre, apellidos, dirección de correo
          electrónico, teléfono y número de documento de identidad.
        </li>
        <li>
          <strong>Datos de inscripción al campamento:</strong> edad, información médica relevante,
          contactos de emergencia y datos de los menores a cargo.
        </li>
        <li>
          <strong>Datos de socios/as:</strong> información de membresía, historial de participación
          y cuotas.
        </li>
        <li>
          <strong>Datos de uso del sitio web:</strong> dirección IP, tipo de navegador, páginas
          visitadas y cookies técnicas.
        </li>
      </ul>
    </section>

    <!-- Section 3 -->
    <section id="base-legal" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">3. Base Legal y Finalidad</h2>
      <div class="space-y-4">
        <div class="rounded-lg border border-gray-200 p-4">
          <h3 class="mb-2 text-base font-semibold text-gray-800">Ejecución del contrato</h3>
          <p class="text-sm leading-relaxed text-gray-700">
            Tratamos sus datos para gestionar la inscripción al campamento y la membresía de
            socio/a, en base a la relación contractual establecida.
          </p>
        </div>
        <div class="rounded-lg border border-gray-200 p-4">
          <h3 class="mb-2 text-base font-semibold text-gray-800">Consentimiento</h3>
          <p class="text-sm leading-relaxed text-gray-700">
            Para el envío de comunicaciones informativas y boletines, basándonos en su
            consentimiento expreso, que puede retirar en cualquier momento.
          </p>
        </div>
        <div class="rounded-lg border border-gray-200 p-4">
          <h3 class="mb-2 text-base font-semibold text-gray-800">Obligación legal</h3>
          <p class="text-sm leading-relaxed text-gray-700">
            Para cumplir con las obligaciones legales aplicables a asociaciones sin ánimo de
            lucro en España.
          </p>
        </div>
        <div class="rounded-lg border border-gray-200 p-4">
          <h3 class="mb-2 text-base font-semibold text-gray-800">Interés legítimo</h3>
          <p class="text-sm leading-relaxed text-gray-700">
            Para la gestión interna de la asociación, mejora de los servicios y mantenimiento
            de la seguridad de la plataforma.
          </p>
        </div>
      </div>
    </section>

    <!-- Section 4 -->
    <section id="conservacion" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">4. Conservación de Datos</h2>
      <p class="leading-relaxed text-gray-700">
        Los datos personales se conservarán durante el tiempo necesario para cumplir con las
        finalidades para las que fueron recogidos y para determinar las posibles responsabilidades
        que pudieran derivarse de las mismas.
      </p>
      <ul class="mt-4 list-disc space-y-2 pl-6 text-gray-700">
        <li>
          <strong>Datos de socios/as activos:</strong> durante la vigencia de la membresía y
          hasta 5 años después de la baja.
        </li>
        <li>
          <strong>Datos de inscripción al campamento:</strong> durante 5 años tras la
          finalización del campamento por obligaciones legales.
        </li>
        <li>
          <strong>Datos de menores:</strong> hasta que el menor cumpla la mayoría de edad,
          más 5 años adicionales.
        </li>
        <li>
          <strong>Comunicaciones de marketing:</strong> hasta que retire su consentimiento.
        </li>
      </ul>
    </section>

    <!-- Section 5 -->
    <section id="destinatarios" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">5. Destinatarios de los Datos</h2>
      <p class="leading-relaxed text-gray-700">
        No cedemos sus datos personales a terceros, salvo obligación legal o cuando sea
        estrictamente necesario para la prestación del servicio:
      </p>
      <ul class="mt-4 list-disc space-y-2 pl-6 text-gray-700">
        <li>
          <strong>Personal de la asociación:</strong> miembros de la junta directiva y
          voluntarios con acceso necesario para la organización del campamento.
        </li>
        <li>
          <strong>Proveedores de servicios:</strong> plataformas de correo electrónico y
          servicios de alojamiento web, con los que existen contratos de encargado del
          tratamiento.
        </li>
        <li>
          <strong>Autoridades públicas:</strong> cuando sea requerido por ley o resolución
          judicial.
        </li>
      </ul>
      <p class="mt-4 leading-relaxed text-gray-700">
        No realizamos transferencias internacionales de datos fuera del Espacio Económico
        Europeo, salvo cuando los proveedores de servicios ofrezcan garantías adecuadas
        conforme al RGPD.
      </p>
    </section>

    <!-- Section 6: GDPR Rights -->
    <section id="derechos" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">6. Derechos de los Usuarios (RGPD)</h2>
      <p class="mb-4 leading-relaxed text-gray-700">
        De conformidad con el RGPD y la LOPDGDD, puede ejercer los siguientes derechos:
      </p>
      <ul class="list-disc space-y-3 pl-6 text-gray-700">
        <li>
          <strong>Derecho de acceso:</strong> conocer qué datos personales tratamos sobre usted.
        </li>
        <li>
          <strong>Derecho de rectificación:</strong> solicitar la corrección de datos inexactos
          o incompletos.
        </li>
        <li>
          <strong>Derecho de supresión</strong> («derecho al olvido»): solicitar la eliminación
          de sus datos cuando ya no sean necesarios.
        </li>
        <li>
          <strong>Derecho a la limitación del tratamiento:</strong> solicitar que suspendamos
          el tratamiento de sus datos en determinadas circunstancias.
        </li>
        <li>
          <strong>Derecho a la portabilidad:</strong> recibir sus datos en un formato
          estructurado y de uso común.
        </li>
        <li>
          <strong>Derecho de oposición:</strong> oponerse al tratamiento de sus datos para
          determinadas finalidades.
        </li>
        <li>
          <strong>Derecho a retirar el consentimiento:</strong> en cualquier momento, sin que
          ello afecte a la licitud del tratamiento anterior.
        </li>
      </ul>
      <div class="mt-6 rounded-lg bg-gray-50 p-4">
        <p class="text-sm text-gray-700">
          Para ejercer cualquiera de estos derechos, puede contactar con nosotros en:
          <a href="mailto:junta.abuvi@gmail.com" class="font-medium text-primary-600 hover:underline">
            junta.abuvi@gmail.com
          </a>
          adjuntando una copia de su documento de identidad.
        </p>
      </div>
    </section>

    <!-- Section 7 -->
    <section id="seguridad" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">7. Medidas de Seguridad</h2>
      <p class="leading-relaxed text-gray-700">
        La Asociación ABUVI ha adoptado las medidas técnicas y organizativas necesarias para
        garantizar la seguridad de sus datos personales y evitar su alteración, pérdida,
        tratamiento o acceso no autorizado, habida cuenta del estado de la tecnología, la
        naturaleza de los datos almacenados y los riesgos a los que están expuestos.
      </p>
      <p class="mt-4 leading-relaxed text-gray-700">
        Entre las medidas implementadas se incluyen: cifrado de datos en tránsito (HTTPS),
        control de acceso basado en roles, autenticación segura y copias de seguridad periódicas.
      </p>
      <p class="mt-4 leading-relaxed text-gray-700">
        En caso de violación de seguridad que afecte a sus datos personales, le notificaremos
        sin dilación indebida cuando sea probable que conlleve un alto riesgo para sus derechos
        y libertades.
      </p>
    </section>

    <!-- Section 8 -->
    <section id="cookies" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">8. Política de Cookies</h2>
      <p class="leading-relaxed text-gray-700">
        Este sitio web utiliza únicamente cookies técnicas estrictamente necesarias para el
        funcionamiento de la plataforma, incluyendo cookies de sesión para mantener el inicio
        de sesión del usuario.
      </p>
      <p class="mt-4 leading-relaxed text-gray-700">
        No utilizamos cookies de seguimiento, analíticas o publicitarias de terceros. Puede
        configurar su navegador para bloquear o eliminar cookies, aunque esto podría afectar
        al funcionamiento del sitio.
      </p>
    </section>

    <!-- Section 9 -->
    <section id="contacto" class="mb-10">
      <h2 class="mb-4 text-2xl font-bold text-gray-900">9. Contacto y Reclamaciones</h2>
      <p class="leading-relaxed text-gray-700">
        Para cualquier consulta sobre el tratamiento de sus datos personales, puede contactar
        con nosotros en:
        <a href="mailto:junta.abuvi@gmail.com" class="font-medium text-primary-600 hover:underline">
          junta.abuvi@gmail.com
        </a>
      </p>
      <p class="mt-4 leading-relaxed text-gray-700">
        Si considera que el tratamiento de sus datos no se ajusta a la normativa vigente, tiene
        derecho a presentar una reclamación ante la autoridad de control competente:
      </p>
      <div class="mt-4 rounded-lg border border-gray-200 p-4">
        <p class="font-semibold text-gray-800">
          Agencia Española de Protección de Datos (AEPD)
        </p>
        <p class="text-sm text-gray-600">C/ Jorge Juan, 6, 28001 Madrid</p>
        <p class="text-sm">
          <a href="https://www.aepd.es" target="_blank" rel="noopener noreferrer"
            class="text-primary-600 hover:underline">
            www.aepd.es
          </a>
        </p>
      </div>
    </section>
  </LegalPageLayout>
</template>
