<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useToast } from 'primevue/usetoast'
import InputText from 'primevue/inputtext'
import Textarea from 'primevue/textarea'
import Button from 'primevue/button'

const toast = useToast()

const form = reactive({
  name: '',
  email: '',
  message: '',
})

const errors = ref<Record<string, string>>({})

const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

const validate = (): boolean => {
  errors.value = {}
  if (!form.name.trim()) errors.value.name = 'El nombre es obligatorio'
  if (!form.email.trim()) {
    errors.value.email = 'El correo electrónico es obligatorio'
  } else if (!emailRegex.test(form.email)) {
    errors.value.email = 'El correo electrónico no es válido'
  }
  if (!form.message.trim()) errors.value.message = 'El mensaje es obligatorio'
  return Object.keys(errors.value).length === 0
}

const handleSubmit = () => {
  if (!validate()) return
  toast.add({
    severity: 'success',
    summary: 'Mensaje enviado',
    detail: '¡Gracias!',
    life: 4000,
  })
  form.name = ''
  form.email = ''
  form.message = ''
  errors.value = {}
}
</script>

<template>
  <section aria-label="Contacto para el 50 aniversario" class="mx-auto max-w-2xl px-6">
    <div class="mb-8 text-center">
      <h2 class="mb-4 text-3xl font-bold text-amber-900 md:text-4xl">Colabora con la asociación</h2>
      <p class="text-gray-600">
        Contacta con los organizadores si quieres colaborar en este 50º aniversario
      </p>
    </div>

    <form class="space-y-6 rounded-xl bg-white p-8 shadow-sm" @submit.prevent="handleSubmit">
      <!-- Nombre -->
      <div>
        <label for="contact-name" class="mb-2 block text-sm font-semibold text-gray-700">
          Nombre <span class="text-red-500">*</span>
        </label>
        <InputText
          id="contact-name"
          v-model="form.name"
          placeholder="Tu nombre completo"
          class="w-full"
          :invalid="!!errors.name"
        />
        <small v-if="errors.name" class="mt-1 block text-red-500">{{ errors.name }}</small>
      </div>

      <!-- Correo electrónico -->
      <div>
        <label for="contact-email" class="mb-2 block text-sm font-semibold text-gray-700">
          Correo electrónico <span class="text-red-500">*</span>
        </label>
        <InputText
          id="contact-email"
          v-model="form.email"
          type="email"
          placeholder="tu@email.com"
          class="w-full"
          :invalid="!!errors.email"
        />
        <small v-if="errors.email" class="mt-1 block text-red-500">{{ errors.email }}</small>
      </div>

      <!-- Mensaje -->
      <div>
        <label for="contact-message" class="mb-2 block text-sm font-semibold text-gray-700">
          Mensaje <span class="text-red-500">*</span>
        </label>
        <Textarea
          id="contact-message"
          v-model="form.message"
          :maxlength="1000"
          :rows="5"
          placeholder="¿Cómo te gustaría colaborar?"
          class="w-full"
          :invalid="!!errors.message"
        />
        <small v-if="errors.message" class="mt-1 block text-red-500">{{ errors.message }}</small>
        <small class="mt-1 block text-right text-gray-400">{{ form.message.length }}/1000</small>
      </div>

      <!-- Submit -->
      <div class="pt-2">
        <Button type="submit" label="Enviar mensaje" icon="pi pi-envelope" class="w-full" />
      </div>
    </form>
  </section>
</template>
