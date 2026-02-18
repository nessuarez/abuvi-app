<script setup lang="ts">
import { reactive, watch } from 'vue'
import Panel from 'primevue/panel'
import InputNumber from 'primevue/inputnumber'
import Textarea from 'primevue/textarea'
import Checkbox from 'primevue/checkbox'
import InputText from 'primevue/inputtext'
import Button from 'primevue/button'
import Divider from 'primevue/divider'
import type { AccommodationCapacity, SharedRoomInfo } from '@/types/camp'

interface Props {
  modelValue: AccommodationCapacity | null
}

const props = defineProps<Props>()
const emit = defineEmits<{
  'update:modelValue': [value: AccommodationCapacity | null]
}>()

const localValue = reactive<AccommodationCapacity>(
  props.modelValue
    ? { ...props.modelValue, sharedRooms: props.modelValue.sharedRooms ? [...props.modelValue.sharedRooms.map(r => ({ ...r }))] : [] }
    : {
        privateRoomsWithBathroom: null,
        privateRoomsSharedBathroom: null,
        sharedRooms: [],
        bungalows: null,
        campOwnedTents: null,
        memberTentAreaSquareMeters: null,
        memberTentCapacityEstimate: null,
        motorhomeSpots: null,
        notes: null
      }
)

watch(
  localValue,
  (val) => {
    const hasData =
      val.privateRoomsWithBathroom != null ||
      val.privateRoomsSharedBathroom != null ||
      (val.sharedRooms && val.sharedRooms.length > 0) ||
      val.bungalows != null ||
      val.campOwnedTents != null ||
      val.memberTentAreaSquareMeters != null ||
      val.memberTentCapacityEstimate != null ||
      val.motorhomeSpots != null ||
      (val.notes && val.notes.trim().length > 0)

    emit('update:modelValue', hasData ? { ...val, sharedRooms: val.sharedRooms ? [...val.sharedRooms] : null } : null)
  },
  { deep: true }
)

const addSharedRoom = () => {
  if (!localValue.sharedRooms) localValue.sharedRooms = []
  localValue.sharedRooms.push({
    quantity: 1,
    bedsPerRoom: 2,
    hasBathroom: false,
    hasShower: false,
    notes: null
  } as SharedRoomInfo)
}

const removeSharedRoom = (index: number) => {
  localValue.sharedRooms?.splice(index, 1)
}

const clearCapacity = () => {
  localValue.privateRoomsWithBathroom = null
  localValue.privateRoomsSharedBathroom = null
  localValue.sharedRooms = []
  localValue.bungalows = null
  localValue.campOwnedTents = null
  localValue.memberTentAreaSquareMeters = null
  localValue.memberTentCapacityEstimate = null
  localValue.motorhomeSpots = null
  localValue.notes = null
  emit('update:modelValue', null)
}
</script>

<template>
  <Panel header="Capacidad de alojamiento" toggleable :collapsed="true">
    <div class="flex flex-col gap-4">
      <!-- Simple numeric fields -->
      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div>
          <label class="mb-1 block text-sm font-medium text-gray-700">
            Habitaciones privadas con baño
          </label>
          <InputNumber
            v-model="localValue.privateRoomsWithBathroom"
            :min="0"
            class="w-full"
            placeholder="0"
            show-buttons
          />
        </div>

        <div>
          <label class="mb-1 block text-sm font-medium text-gray-700">
            Habitaciones privadas baño compartido
          </label>
          <InputNumber
            v-model="localValue.privateRoomsSharedBathroom"
            :min="0"
            class="w-full"
            placeholder="0"
            show-buttons
          />
        </div>

        <div>
          <label class="mb-1 block text-sm font-medium text-gray-700">Bungalows / Casetas</label>
          <InputNumber
            v-model="localValue.bungalows"
            :min="0"
            class="w-full"
            placeholder="0"
            show-buttons
          />
        </div>

        <div>
          <label class="mb-1 block text-sm font-medium text-gray-700">Tiendas del camping</label>
          <InputNumber
            v-model="localValue.campOwnedTents"
            :min="0"
            class="w-full"
            placeholder="0"
            show-buttons
          />
        </div>

        <div>
          <label class="mb-1 block text-sm font-medium text-gray-700">
            Área tiendas socios (m²)
          </label>
          <InputNumber
            v-model="localValue.memberTentAreaSquareMeters"
            :min="0"
            class="w-full"
            placeholder="0"
          />
        </div>

        <div>
          <label class="mb-1 block text-sm font-medium text-gray-700">
            Capacidad estimada tiendas socios
          </label>
          <InputNumber
            v-model="localValue.memberTentCapacityEstimate"
            :min="0"
            class="w-full"
            placeholder="0"
            show-buttons
          />
        </div>

        <div>
          <label class="mb-1 block text-sm font-medium text-gray-700">
            Plazas autocaravanas
          </label>
          <InputNumber
            v-model="localValue.motorhomeSpots"
            :min="0"
            class="w-full"
            placeholder="0"
            show-buttons
          />
        </div>
      </div>

      <!-- Notes -->
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Notas libres</label>
        <Textarea
          v-model="localValue.notes"
          class="w-full"
          rows="2"
          placeholder="Información adicional sobre el alojamiento..."
        />
      </div>

      <!-- Shared rooms -->
      <div>
        <h4 class="mb-2 text-sm font-semibold text-gray-700">Habitaciones compartidas</h4>

        <div
          v-for="(room, index) in localValue.sharedRooms"
          :key="index"
          class="rounded-lg border border-gray-200 p-3"
        >
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="mb-1 block text-xs font-medium text-gray-600">Habitaciones</label>
              <InputNumber v-model="room.quantity" :min="1" class="w-full" show-buttons />
            </div>
            <div>
              <label class="mb-1 block text-xs font-medium text-gray-600">Camas por hab.</label>
              <InputNumber v-model="room.bedsPerRoom" :min="1" class="w-full" show-buttons />
            </div>
            <div class="flex items-center gap-2">
              <Checkbox v-model="room.hasBathroom" binary :input-id="`bathroom-${index}`" />
              <label :for="`bathroom-${index}`" class="text-sm">Baño propio</label>
            </div>
            <div class="flex items-center gap-2">
              <Checkbox v-model="room.hasShower" binary :input-id="`shower-${index}`" />
              <label :for="`shower-${index}`" class="text-sm">Ducha propia</label>
            </div>
            <div class="col-span-2">
              <label class="mb-1 block text-xs font-medium text-gray-600">Notas</label>
              <InputText v-model="room.notes" class="w-full" placeholder="Ej: segunda planta..." />
            </div>
          </div>
          <div class="mt-2 flex justify-end">
            <Button
              icon="pi pi-trash"
              text
              severity="danger"
              size="small"
              aria-label="Eliminar tipo de habitación"
              @click="removeSharedRoom(index)"
            />
          </div>
          <Divider v-if="index < (localValue.sharedRooms?.length ?? 0) - 1" class="my-0" />
        </div>

        <Button
          label="Añadir tipo habitación compartida"
          icon="pi pi-plus"
          text
          size="small"
          class="mt-2"
          @click="addSharedRoom"
        />
      </div>

      <!-- Clear all -->
      <div class="flex justify-end">
        <Button
          label="Limpiar capacidad"
          severity="secondary"
          text
          size="small"
          @click="clearCapacity"
        />
      </div>
    </div>
  </Panel>
</template>
