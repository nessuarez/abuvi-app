<!-- TODO: Replace with official statutes document provided by ABUVI leadership -->
<script setup lang="ts">
import LegalPageLayout from '@/components/legal/LegalPageLayout.vue'
import Accordion from 'primevue/accordion'
import AccordionPanel from 'primevue/accordionpanel'
import AccordionHeader from 'primevue/accordionheader'
import AccordionContent from 'primevue/accordioncontent'
import Button from 'primevue/button'

// Chapter data for the accordion
interface Chapter {
  value: string
  title: string
  articles: Array<{ number: number; title: string; content: string }>
}

const chapters: Chapter[] = [
  {
    value: '1',
    title: 'Capítulo I: Disposiciones Generales',
    articles: [
      {
        number: 1,
        title: 'Denominación',
        content:
          'La asociación se denomina ABUVI (Amigos de la Buena Vida), constituida al amparo de la Ley Orgánica 1/2002, de 22 de marzo, reguladora del Derecho de Asociación.'
      },
      {
        number: 2,
        title: 'Domicilio',
        content: 'El domicilio social de la asociación se establece en C/BUTRÓN 27, 28022 Madrid.'
      },
      {
        number: 3,
        title: 'Ámbito territorial',
        content:
          'La asociación desarrolla principalmente sus actividades en el territorio español, pudiendo extender sus actividades a otros ámbitos cuando la naturaleza de sus fines lo requiera.'
      },
      {
        number: 4,
        title: 'Naturaleza',
        content:
          'La asociación es una entidad sin ánimo de lucro, ajena a todo interés político, religioso o económico.'
      },
      {
        number: 5,
        title: 'Duración',
        content:
          'La asociación se constituye por tiempo indefinido, desde la fecha de su inscripción en el Registro de Asociaciones.'
      }
    ]
  },
  {
    value: '2',
    title: 'Capítulo II: Fines y Actividades',
    articles: [
      {
        number: 6,
        title: 'Fines',
        content:
          'La asociación tiene como fines principales: promover la amistad, la convivencia y el contacto con la naturaleza; organizar campamentos y actividades al aire libre; fomentar valores de compañerismo y solidaridad entre sus miembros y sus familias.'
      },
      {
        number: 7,
        title: 'Actividades',
        content:
          'Para la consecución de sus fines, la asociación realizará las siguientes actividades: organización de campamentos anuales; actividades culturales y de ocio; encuentros y reuniones entre socios; celebraciones de efemérides de la asociación.'
      },
      {
        number: 8,
        title: 'Carácter no lucrativo',
        content:
          'Las actividades de la asociación no perseguirán ningún beneficio económico para sus miembros. Los posibles excedentes económicos se reinvertirán íntegramente en la consecución de los fines asociativos.'
      }
    ]
  },
  {
    value: '3',
    title: 'Capítulo III: Socios',
    articles: [
      {
        number: 9,
        title: 'Clases de socios',
        content:
          'Podrán ser miembros de la asociación: socios fundadores, aquellos que participaron en la constitución; socios ordinarios, los que ingresan con posterioridad a la fundación; socios de honor, personas físicas o jurídicas que por méritos especiales se hagan acreedores de tal distinción.'
      },
      {
        number: 10,
        title: 'Requisitos de admisión',
        content:
          'Para ser admitido como socio ordinario será necesario presentar solicitud por escrito a la Junta Directiva, que resolverá en un plazo máximo de 30 días.'
      },
      {
        number: 11,
        title: 'Derechos de los socios',
        content:
          'Los socios tendrán derecho a: participar en las actividades de la asociación; asistir con voz y voto a las Asambleas Generales; elegir y ser elegidos para cargos directivos; recibir información sobre las actividades de la asociación.'
      },
      {
        number: 12,
        title: 'Obligaciones de los socios',
        content:
          'Los socios estarán obligados a: cumplir los presentes Estatutos y los acuerdos válidamente adoptados; abonar las cuotas fijadas; actuar conforme a los valores y principios de la asociación.'
      },
      {
        number: 13,
        title: 'Pérdida de la condición de socio',
        content:
          'Se perderá la condición de socio por: voluntad propia; impago de cuotas; incumplimiento de los Estatutos; conducta contraria a los valores de la asociación, previo expediente disciplinario.'
      },
      {
        number: 14,
        title: 'Cuotas',
        content:
          'La cuantía de las cuotas ordinarias y extraordinarias será fijada anualmente por la Asamblea General a propuesta de la Junta Directiva.'
      },
      {
        number: 15,
        title: 'Registro de socios',
        content:
          'La Junta Directiva llevará un registro actualizado de todos los socios de la asociación.'
      }
    ]
  },
  {
    value: '4',
    title: 'Capítulo IV: Órganos de Gobierno',
    articles: [
      {
        number: 16,
        title: 'Órganos de gobierno',
        content:
          'Los órganos de gobierno de la asociación son: la Asamblea General y la Junta Directiva.'
      },
      {
        number: 17,
        title: 'La Asamblea General',
        content:
          'La Asamblea General es el órgano soberano de la asociación. Estará integrada por todos los socios y se reunirá en sesión ordinaria una vez al año, y en sesión extraordinaria cuando las circunstancias lo requieran.'
      },
      {
        number: 18,
        title: 'Convocatoria de la Asamblea',
        content:
          'La convocatoria de la Asamblea General, tanto ordinaria como extraordinaria, se realizará con al menos 15 días de antelación, indicando el orden del día, lugar, fecha y hora.'
      },
      {
        number: 19,
        title: 'Quórum y acuerdos',
        content:
          'La Asamblea quedará válidamente constituida en primera convocatoria con la asistencia de la mitad más uno de los socios, y en segunda convocatoria con cualquier número de asistentes.'
      },
      {
        number: 20,
        title: 'Competencias de la Asamblea General',
        content:
          'Corresponde a la Asamblea General: aprobar y modificar los Estatutos; elegir y cesar a los miembros de la Junta Directiva; aprobar el presupuesto y las cuentas anuales; acordar la disolución de la asociación.'
      },
      {
        number: 21,
        title: 'La Junta Directiva',
        content:
          'La Junta Directiva es el órgano de representación y gestión de la asociación. Estará compuesta por: Presidente/a, Vicepresidente/a, Secretario/a, Tesorero/a y los vocales que determine la Asamblea General.'
      },
      {
        number: 22,
        title: 'Elección de la Junta Directiva',
        content:
          'Los miembros de la Junta Directiva serán elegidos por la Asamblea General por un periodo de [pendiente] años, pudiendo ser reelegidos.'
      },
      {
        number: 23,
        title: 'El Presidente/a',
        content:
          'El Presidente/a ostenta la representación legal de la asociación y preside los órganos colegiados. Ejecuta los acuerdos adoptados por la Junta Directiva y la Asamblea General.'
      },
      {
        number: 24,
        title: 'El/La Secretario/a',
        content:
          'El/La Secretario/a custodia la documentación de la asociación, levanta actas de las reuniones y expide certificaciones.'
      },
      {
        number: 25,
        title: 'El/La Tesorero/a',
        content:
          'El/La Tesorero/a gestiona el patrimonio de la asociación, elabora el presupuesto y las cuentas anuales y controla la contabilidad.'
      },
      {
        number: 26,
        title: 'Reuniones de la Junta Directiva',
        content:
          'La Junta Directiva se reunirá con la periodicidad que las necesidades de la asociación lo requieran, como mínimo una vez al trimestre.'
      },
      {
        number: 27,
        title: 'Acuerdos de la Junta Directiva',
        content:
          'Los acuerdos se adoptarán por mayoría simple de los presentes. En caso de empate, el voto del Presidente/a será de calidad.'
      },
      {
        number: 28,
        title: 'Responsabilidades de la Junta Directiva',
        content:
          'La Junta Directiva responderá de su gestión ante la Asamblea General. Deberá presentar anualmente la memoria de actividades y las cuentas del ejercicio.'
      }
    ]
  },
  {
    value: '5',
    title: 'Capítulo V: Régimen Económico',
    articles: [
      {
        number: 29,
        title: 'Recursos económicos',
        content:
          'Los recursos económicos de la asociación se nutrirán de: cuotas de los socios; donaciones y legados; subvenciones y ayudas públicas o privadas; ingresos derivados de las actividades propias de la asociación.'
      },
      {
        number: 30,
        title: 'Ejercicio económico',
        content:
          'El ejercicio económico coincidirá con el año natural, comenzando el 1 de enero y terminando el 31 de diciembre de cada año.'
      },
      {
        number: 31,
        title: 'Presupuesto',
        content:
          'La Junta Directiva elaborará anualmente el presupuesto de ingresos y gastos, que deberá ser aprobado por la Asamblea General.'
      },
      {
        number: 32,
        title: 'Cuentas anuales',
        content:
          'Las cuentas anuales, comprensivas del balance, la cuenta de resultados y la memoria, deberán ser aprobadas por la Asamblea General ordinaria.'
      }
    ]
  },
  {
    value: '6',
    title: 'Capítulo VI: Modificación de Estatutos',
    articles: [
      {
        number: 33,
        title: 'Modificación de estatutos',
        content:
          'La modificación de los presentes Estatutos requerirá el acuerdo de la Asamblea General Extraordinaria, adoptado por mayoría cualificada de dos tercios de los socios presentes o representados. La propuesta de modificación deberá ser comunicada a todos los socios con al menos 30 días de antelación.'
      }
    ]
  },
  {
    value: '7',
    title: 'Capítulo VII: Disolución',
    articles: [
      {
        number: 34,
        title: 'Causas de disolución',
        content:
          'La asociación se disolverá: por acuerdo de la Asamblea General Extraordinaria adoptado por mayoría de dos tercios; por imposibilidad de cumplir los fines estatutarios; por mandato judicial.'
      },
      {
        number: 35,
        title: 'Liquidación',
        content:
          'En caso de disolución, la Asamblea General nombrará una comisión liquidadora. El patrimonio resultante, una vez cubiertas las obligaciones pendientes, se donará a una entidad sin ánimo de lucro con fines análogos designada por la Asamblea.'
      }
    ]
  }
]
</script>

<template>
  <LegalPageLayout
    title="Estatutos de la Asociación ABUVI"
    last-updated="[Pendiente de fecha oficial]"
    :show-toc="false"
  >
    <!-- Metadata header -->
    <div class="mb-6 rounded-lg bg-gray-50 p-4 text-sm text-gray-600">
      <p><strong>Aprobados en Asamblea General:</strong> [Pendiente]</p>
      <p class="mt-1"><strong>Última modificación:</strong> [Pendiente]</p>
      <p class="mt-1"><strong>Inscripción registral:</strong> [Pendiente]</p>
    </div>

    <!-- Download button -->
    <div class="mb-6 flex items-center gap-3">
      <Button
        label="Descargar PDF"
        icon="pi pi-download"
        outlined
        disabled
        v-tooltip.top="'Próximamente disponible'"
      />
      <span class="text-sm text-gray-500">El documento oficial en PDF estará disponible próximamente.</span>
    </div>

    <!-- Accordion chapters -->
    <Accordion>
      <AccordionPanel v-for="chapter in chapters" :key="chapter.value" :value="chapter.value">
        <AccordionHeader class="font-semibold">
          {{ chapter.title }}
        </AccordionHeader>
        <AccordionContent>
          <div class="space-y-6 py-2">
            <div v-for="article in chapter.articles" :key="article.number">
              <h3 class="mb-2 text-base font-semibold text-gray-800">
                Artículo {{ article.number }}. {{ article.title }}
              </h3>
              <p class="leading-relaxed text-gray-700">{{ article.content }}</p>
            </div>
          </div>
        </AccordionContent>
      </AccordionPanel>
    </Accordion>

    <p class="mt-8 text-center text-sm text-gray-500">
      Estos estatutos son un borrador con fines de maqueta. Consulte con la Junta Directiva
      para el documento oficial vigente.
    </p>
  </LegalPageLayout>
</template>
