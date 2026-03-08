<script setup lang="ts">
import LegalPageLayout from '@/components/legal/LegalPageLayout.vue'
</script>

<template>
  <LegalPageLayout
    title="Estatutos de la Asociación ABUVI"
    :show-toc="false"
  >
    <div class="flex flex-col items-center justify-center py-16 text-center">
      <i class="pi pi-file-edit mb-4 text-5xl text-gray-300" />
      <p class="text-lg font-medium text-gray-700">
        Próximamente publicaremos aquí la documentación oficial de la asociación.
      </p>
      <p class="mt-3 text-sm text-gray-500">
        Si necesitas consultar los estatutos, contacta con la Junta Directiva en
        <a
          href="mailto:junta.abuvi@gmail.com"
          class="font-medium text-primary-600 hover:underline"
        >
          junta.abuvi@gmail.com
        </a>
      </p>
    </div>
  </LegalPageLayout>
</template>
