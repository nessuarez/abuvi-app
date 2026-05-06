# Frontend Implementation Plan (ENRICHED): feat-accommodation-features — Configurable Accommodation Features

> **Enrichment focus:** Replaces the plain `InputText` emoji field with a searchable icon picker powered by `emoji-mart`. All other sections remain as the base spec unless explicitly updated.

---

## Icon Picker Strategy

### Problem with the Original Approach

The base spec calls for a plain `InputText` where the board types an emoji directly via the OS keyboard. This has several UX problems:

- Board members on Windows have to open the emoji panel manually (`Win + .`) and search there — no context about what other icons have been used.
- No visual consistency guidance — different board members may use wildly different emoji styles.
- No validation that the value is actually a valid emoji (could be typed text like "wifi").

### Recommended Library: `emoji-mart`

**Package:** [`emoji-mart`](https://github.com/missive/emoji-mart) (v5.x — vanilla JS, framework-agnostic)

**Why `emoji-mart`:**

- Works with Vue 3 as vanilla JS (no wrapper needed — just mount the `Picker` web component).
- Has built-in keyword search in English (and extensible to Spanish via custom data).
- Emojis are stored as a single Unicode character or short string — **no data model change needed**.
- The data file (`@emoji-mart/data`) covers all standard emoji with keyword aliases.
- ~200 KB data file, loaded lazily (not in the main bundle).
- Already widely used in production admin panels (Notion, Linear, GitHub, Slack).
- Search covers accommodation-relevant terms: "wifi", "pool", "shower", "bed", "parking", "air", "pet", "family", "accessible", "fire", "kitchen", "toilet", etc.

**Alternative considered and rejected:** `@iconify/vue` + `@iconify-json/tabler`

- Tabler icons are SVG, which requires changing the rendering logic everywhere (catalog panel, assignment dialog, zone/accommodation badge rows).
- Bundle for the full Tabler JSON is ~1.5 MB offline or requires CDN calls per icon.
- Icon name strings (e.g. `"tabler:wifi"`) are less portable and harder for non-technical users to understand.
- Emoji remains the simpler, more universally understood choice for this admin context.

---

## New npm Dependency

Add to `frontend/package.json`:

```jsonc
"emoji-mart": "^5.6.0",
"@emoji-mart/data": "^1.2.1"
```

Install:

```
pnpm add emoji-mart @emoji-mart/data
```

No TypeScript `@types` package needed — `emoji-mart` ships its own types.

---

## Changes to Step 1: TypeScript Types

No change to `accommodation-feature.ts`. The `icon` field remains `string` (stores a single emoji character, e.g. `"📶"`).

---

## Changes to Step 6: `AccommodationFeatureDialog.vue`

This is the primary change. The `icon` field becomes an interactive picker instead of a plain text input.

### Icon Picker Sub-Component: `EmojiPickerField.vue`

Create a reusable wrapper:

**File:** `frontend/src/components/ui/EmojiPickerField.vue` *(new)*

```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue'

// emoji-mart is vanilla JS — import the Picker class and data
import data from '@emoji-mart/data'

const props = defineProps<{
  modelValue: string
  error?: string | null
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const showPicker = ref(false)
const pickerRef = ref<HTMLElement | null>(null)

function onEmojiSelect(emoji: { native: string }) {
  emit('update:modelValue', emoji.native)
  showPicker.value = false
}

onMounted(async () => {
  // Dynamically import to keep it out of the main bundle
  const { Picker } = await import('emoji-mart')

  if (pickerRef.value) {
    const picker = new Picker({
      data,
      locale: 'en',
      onEmojiSelect,
      previewPosition: 'none',
      skinTonePosition: 'none',
      theme: 'light',
    })
    pickerRef.value.appendChild(picker as unknown as HTMLElement)
  }
})

function togglePicker() {
  showPicker.value = !showPicker.value
}

// Close on outside click
function handleClickOutside(event: MouseEvent) {
  const target = event.target as HTMLElement
  if (!target.closest('.emoji-picker-wrapper')) {
    showPicker.value = false
  }
}
</script>

<template>
  <div class="emoji-picker-wrapper relative">
    <!-- Trigger button: shows current icon or placeholder -->
    <button
      type="button"
      class="flex items-center gap-2 rounded border px-3 py-2 text-sm hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500"
      :class="error ? 'border-red-500' : 'border-gray-300'"
      @click="togglePicker"
    >
      <span v-if="modelValue" class="text-2xl leading-none">{{ modelValue }}</span>
      <span v-else class="text-gray-400">Seleccionar icono…</span>
      <i class="pi pi-chevron-down text-xs text-gray-400" />
    </button>

    <!-- Selected preview label -->
    <span v-if="modelValue" class="ml-2 text-xs text-gray-500">
      Vista previa: <span class="text-base">{{ modelValue }}</span>
    </span>

    <!-- Picker popover -->
    <div
      v-if="showPicker"
      class="absolute left-0 top-full z-50 mt-1 shadow-xl"
      v-click-outside="() => (showPicker = false)"
    >
      <div ref="pickerRef" />
    </div>
  </div>
</template>
```

> **Note on `v-click-outside`:** Use VueUse's `onClickOutside` composable instead of a custom directive — it is already available via `@vueuse/core` (already installed).

### Revised `AccommodationFeatureDialog.vue` Icon Field Section

Replace the icon `InputText` with `EmojiPickerField`:

```vue
<!-- BEFORE -->
<div class="flex flex-col gap-1">
  <label class="text-sm font-medium">Icono</label>
  <InputText v-model="form.icon" placeholder="Ej: 📶" maxlength="100" />
  <small class="text-red-500">{{ validationErrors.icon }}</small>
</div>

<!-- AFTER -->
<div class="flex flex-col gap-1">
  <label class="text-sm font-medium">Icono</label>
  <p class="text-xs text-gray-500">
    Busca por nombre en inglés (ej: "wifi", "pool", "bed", "shower").
  </p>
  <EmojiPickerField v-model="form.icon" :error="validationErrors.icon" />
  <small v-if="validationErrors.icon" class="text-red-500">{{ validationErrors.icon }}</small>
</div>
```

Import in `AccommodationFeatureDialog.vue`:

```typescript
import EmojiPickerField from '@/components/ui/EmojiPickerField.vue'
```

### Validation

Validation for `icon` should check that the value is non-empty and is a valid emoji (single or double Unicode code point). A lightweight check:

```typescript
function isEmoji(str: string): boolean {
  // Matches one emoji grapheme cluster (handles compound emojis like 👨‍👩‍👧)
  const emojiRegex = /^\p{Emoji}/u
  return emojiRegex.test(str.trim())
}

// In validateForm():
if (!form.icon.trim()) {
  errors.icon = 'El icono es obligatorio'
} else if (!isEmoji(form.icon)) {
  errors.icon = 'Selecciona un emoji válido usando el selector'
}
```

---

## Changes to Step 7: `FeatureAssignmentDialog.vue`

No change to the dialog logic. Icons already render correctly as emoji in the checkbox list:

```vue
<span class="text-lg mr-1">{{ feature.icon }}</span>
<span>{{ feature.name }}</span>
```

---

## Changes to Step 8: `AccommodationFeaturesCataloguePanel.vue`

In the DataTable, the **Icono** column renders emoji naturally:

```vue
<Column header="Icono">
  <template #body="{ data: feature }">
    <span class="text-xl">{{ feature.icon }}</span>
  </template>
</Column>
```

No change needed here — emoji strings render fine in `<span>` elements.

---

## Changes to Step 9 & 10: Feature Badges on Accommodation/Zone Rows

Feature badges already show `feature.icon` as emoji. No change needed. The spec's badge cap (3 visible + "+N more") still applies.

---

## Changes to Step 12: Unit Tests

Add to `AccommodationFeatureDialog.test.ts`:

```
icon field — shows placeholder when icon is empty
icon field — shows emoji preview when icon is set
icon field — shows validation error when icon is not a valid emoji
icon field — emoji picker popover opens on trigger click
icon field — selecting an emoji closes the picker and updates the form
```

Since `emoji-mart` is a side-effectful DOM component, mock it in Vitest:

```typescript
vi.mock('emoji-mart', () => ({
  Picker: class {
    constructor(_opts: unknown) {}
    // Picker appends itself to the DOM in onMounted — mock does nothing
  },
}))
```

Test `EmojiPickerField.vue` in isolation with `@vue/test-utils`:

```typescript
it('emits update:modelValue when emoji selected', async () => {
  const wrapper = mount(EmojiPickerField, { props: { modelValue: '' } })
  // Simulate emoji selection by calling the onEmojiSelect handler directly
  await wrapper.vm.onEmojiSelect({ native: '📶' })
  expect(wrapper.emitted('update:modelValue')?.[0]).toEqual(['📶'])
})
```

---

## Revised Dependencies Section

| Package | Version | Purpose |
|---|---|---|
| `emoji-mart` | `^5.6.0` | Emoji picker UI component (vanilla JS) |
| `@emoji-mart/data` | `^1.2.1` | Emoji dataset with keyword search |

All other dependencies remain the same as the base spec.

---

## Revised UI/UX Considerations (Icon Field Only)

| Aspect | Decision |
|---|---|
| **Trigger UI** | Button showing current emoji (or placeholder) + chevron icon |
| **Picker position** | Popover below the trigger button, `z-50`, `shadow-xl` |
| **Close behaviour** | Click outside closes (via VueUse `onClickOutside`), ESC key closes |
| **Preview** | Live emoji preview next to the trigger button (large size) |
| **Search hint** | Helper text: "Busca por nombre en inglés (ej: 'wifi', 'pool', 'bed')" |
| **Mobile** | Picker is scrollable on small screens; consider `max-h-72 overflow-y-auto` wrapper |
| **Empty state** | "Seleccionar icono…" placeholder in the trigger button |
| **Validation** | Error border on trigger button; error text below |
| **Bundle** | Dynamic import of `emoji-mart` — not in the main chunk |

---

## Implementation Notes

1. **`emoji-mart` Picker is a custom HTML element** — it appends itself to a container DOM node via `new Picker({ ... })`. Use `ref` + `onMounted` as shown above. Do not try to use it as a Vue component.
2. **Dynamic import** — wrap `import('emoji-mart')` in a dynamic import inside `onMounted` so the ~200 KB data file is not bundled with the main app chunk.
3. **Locale** — `emoji-mart` ships English keywords by default. Spanish search aliases are not available in the official data file. Add a helper text telling board members to search in English.
4. **VueUse `onClickOutside`** — already available in `@vueuse/core`. Use it on the picker wrapper element to close on outside click.
5. **Form reset** — when the dialog opens in "create" mode, `form.icon` starts as `''`. When opened in "edit" mode, it starts with `feature.icon`. The picker reads the initial value from the button display; it does not need to be told the current value internally.
6. **`EmojiPickerField` is reusable** — place it in `frontend/src/components/ui/` so it can be reused in other contexts (e.g., a future tag or category feature).
