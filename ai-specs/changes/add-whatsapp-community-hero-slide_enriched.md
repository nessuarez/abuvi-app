# Add WhatsApp Community Slide to Hero Carousel

## User Story

**As a** site visitor,
**I want to** see a slide in the hero carousel inviting me to join the ABUVI WhatsApp community,
**So that** I can easily connect with the community through WhatsApp.

## Context

The home page hero carousel (`HomeHeroCarousel.vue`) currently displays 3 static slides using PrimeVue's `Galleria` component. Each slide has: image, headline, description, and a CTA button. The CTA currently uses `<router-link>` for internal navigation only.

This new slide requires an **external link** (WhatsApp community URL), which means the component must be updated to support both internal routes and external URLs.

## Requirements

### Functional

1. Add a 4th slide to the hero carousel with WhatsApp community content:
   - **Headline**: "Unete a nuestra Comunidad" (or similar, in Spanish)
   - **Description**: Short invitation text encouraging users to join the ABUVI WhatsApp community
   - **CTA Label**: "Unirse a WhatsApp" (or similar)
   - **CTA Link**: External WhatsApp community invitation URL (to be provided by the user)
   - **Image**: A relevant image (to be provided or sourced by the user)

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

#### New Slide Data

```typescript
{
  image: imgWhatsapp, // new image asset
  imageAlt: 'Comunidad WhatsApp ABUVI',
  headline: 'Unete a nuestra Comunidad',
  description: 'Mantente al dia con las novedades, eventos y actividades de ABUVI. Unete a nuestro grupo de WhatsApp.',
  ctaLabel: 'Unirse a WhatsApp',
  ctaPath: '<WHATSAPP_COMMUNITY_URL>', // TO BE PROVIDED
  external: true,
}
```

### Files to Modify

| File | Change |
|------|--------|
| `frontend/src/components/home/HomeHeroCarousel.vue` | Add `external?` to interface, add new slide data, conditional CTA rendering |
| `frontend/src/components/home/__tests__/HomeHeroCarousel.test.ts` | Update test to expect 4 slides, add test for external link rendering |
| `frontend/src/assets/images/` | Add new WhatsApp/community image |

### Image Asset

The user needs to provide:
1. **A background image** for the slide (suggested: a photo of the community, a WhatsApp-themed image, or a social gathering photo). Should be optimized for web (compressed JPEG, reasonable resolution for full-width hero display).
2. **The WhatsApp community invitation URL** (e.g., `https://chat.whatsapp.com/...`)

## Acceptance Criteria

- [ ] A 4th slide appears in the hero carousel with WhatsApp community content
- [ ] The CTA button opens the WhatsApp community link in a new tab
- [ ] External link uses `target="_blank"` and `rel="noopener noreferrer"` for security
- [ ] The slide visually matches the existing carousel style
- [ ] Existing slides and navigation (dots, autoplay) continue working correctly
- [ ] Unit tests updated to cover 4 slides and external link behavior
- [ ] Image is optimized and stored in `frontend/src/assets/images/`

## Blockers / Questions for the User

1. **WhatsApp community URL**: What is the invitation link?
2. **Image**: Do you have a specific image to use, or should a placeholder/stock image be used?
3. **Slide position**: Should this be the last slide (4th), or placed at a specific position in the rotation?
4. **Copy**: Are the suggested headline/description acceptable, or do you have preferred text?

## Non-Functional Requirements

- **Security**: External links must use `rel="noopener noreferrer"`
- **Performance**: Image should be optimized (compressed JPEG, max ~200KB)
- **Accessibility**: Image alt text must be descriptive; CTA link should be clearly labeled