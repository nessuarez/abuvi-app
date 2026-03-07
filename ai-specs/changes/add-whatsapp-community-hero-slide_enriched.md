# Add WhatsApp Community Slide to Hero Carousel

## User Story

**As a** site visitor,
**I want to** see a slide in the hero carousel inviting me to join the ABUVI WhatsApp community,
**So that** I can easily connect with the community through WhatsApp.

## Context

The home page hero carousel (`HomeHeroCarousel.vue`) currently displays 3 static slides using PrimeVue's `Galleria` component. Each slide has: image, headline, description, and a CTA button. The CTA currently uses `<router-link>` for internal navigation only.

This new slide requires an **external link** (WhatsApp community URL), which means the component must be updated to support both internal routes and external URLs.

## Final Specifications

- **WhatsApp URL**: `https://chat.whatsapp.com/EBsp8GfXGPEB6PM8u3HUGu`
- **Image**: `frontend/src/assets/images/swello-IRWj0hdSbM4-unsplash.jpg` (already exists)
- **Slide position**: 2nd (between "50 Anos de Buena Vida" and "Campamento 2026")
- **Headline**: "Unete a la Comunidad de Whatsapp"

## Requirements

### Functional

1. Add a new slide in **2nd position** to the hero carousel with WhatsApp community content:
   - **Headline**: "Unete a la Comunidad de Whatsapp"
   - **Description**: Short invitation text encouraging users to join the ABUVI WhatsApp community
   - **CTA Label**: "Unirse a WhatsApp" (or similar)
   - **CTA Link**: `https://chat.whatsapp.com/EBsp8GfXGPEB6PM8u3HUGu`
   - **Image**: `swello-IRWj0hdSbM4-unsplash.jpg` (already in assets)

2. The CTA button must open the WhatsApp link in a **new tab** (`target="_blank"`, `rel="noopener noreferrer"`)

3. The slide should blend visually with the existing carousel style (gradient overlay, amber theme, backdrop blur)

### Technical

#### Interface Change

Extend the `HeroSlide` interface to support external links:

```typescript
interface HeroSlide {
  image: string
  imageAlt: string
  headline: string
  description: string
  ctaLabel: string
  ctaPath: string
  external?: boolean // when true, render <a> instead of <router-link>
}
```

#### Template Change

Replace the single `<router-link>` with conditional rendering:

```html
<a
  v-if="slides[activeIndex].external"
  :href="slides[activeIndex].ctaPath"
  target="_blank"
  rel="noopener noreferrer"
  class="inline-block rounded-lg bg-amber-400 ..."
>
  {{ slides[activeIndex].ctaLabel }}
</a>
<router-link
  v-else
  :to="slides[activeIndex].ctaPath"
  class="inline-block rounded-lg bg-amber-400 ..."
>
  {{ slides[activeIndex].ctaLabel }}
</router-link>
```

#### New Slide Data (2nd position in array)

```typescript
{
  image: imgWhatsapp, // import from '@/assets/images/swello-IRWj0hdSbM4-unsplash.jpg'
  imageAlt: 'Comunidad WhatsApp ABUVI',
  headline: 'Unete a la Comunidad de Whatsapp',
  description: 'Mantente al dia con las novedades, eventos y actividades de ABUVI. Unete a nuestro grupo de WhatsApp.',
  ctaLabel: 'Unirse a WhatsApp',
  ctaPath: 'https://chat.whatsapp.com/EBsp8GfXGPEB6PM8u3HUGu',
  external: true,
}
```

### Files to Modify

| File | Change |
|------|--------|
| `frontend/src/components/home/HomeHeroCarousel.vue` | Add `external?` to interface, add new slide data at index 1, conditional CTA rendering |
| `frontend/src/components/home/__tests__/HomeHeroCarousel.test.ts` | Update test to expect 4 slides, add test for external link rendering |

## Acceptance Criteria

- [ ] A 4th slide appears in the hero carousel in 2nd position with WhatsApp community content
- [ ] The CTA button opens the WhatsApp community link in a new tab
- [ ] External link uses `target="_blank"` and `rel="noopener noreferrer"` for security
- [ ] The slide visually matches the existing carousel style
- [ ] Existing slides and navigation (dots, autoplay) continue working correctly
- [ ] Unit tests updated to cover 4 slides and external link behavior

## Non-Functional Requirements

- **Security**: External links must use `rel="noopener noreferrer"`
- **Accessibility**: Image alt text must be descriptive; CTA link should be clearly labeled
