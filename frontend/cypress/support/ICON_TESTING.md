# Icon Testing Guide

This document describes the custom Cypress commands available for testing visual elements, specifically PrimeIcons.

## Why Test Icons?

Visual elements like icons can fail to load due to:
- Missing CSS imports
- Build configuration issues
- Font file loading problems
- CDN availability issues

These tests ensure icons are properly loaded and visible to users.

## Custom Commands

### `cy.verifyPrimeIconsLoaded()`

Verifies that the PrimeIcons stylesheet is loaded in the document.

**Usage:**
```typescript
cy.verifyPrimeIconsLoaded()
```

**What it checks:**
- PrimeIcons CSS file is present in document stylesheets
- Font resources are available

---

### `cy.verifyIcon(iconClass)`

Verifies that an icon element is visible and properly styled within a parent element.

**Parameters:**
- `iconClass` (string): The icon class name (e.g., 'pi-eye', 'pi-plus', 'pi-pencil')

**Usage:**
```typescript
// Check icon within a button
cy.get('button').verifyIcon('pi-eye')

// Check icon in any container
cy.contains('button', 'Create User').verifyIcon('pi-plus')
```

**What it checks:**
- Icon element exists
- Icon is visible
- Icon uses the correct font-family (primeicons)

---

### `cy.verifyButtonWithIcon(buttonSelector, iconClass)`

Verifies that a button contains a visible icon and is properly styled.

**Parameters:**
- `buttonSelector` (string): CSS selector for the button
- `iconClass` (string): The icon class name (e.g., 'pi-eye', 'pi-plus')

**Usage:**
```typescript
// Verify a specific button has an icon
cy.verifyButtonWithIcon('[data-testid="view-user-button"]', 'pi-eye')

// Verify the create button has the plus icon
cy.verifyButtonWithIcon('button[aria-label="Create"]', 'pi-plus')
```

**What it checks:**
- Button is visible
- Button is not hidden (display/visibility)
- Icon within button exists and is visible
- Icon uses correct font

---

## Example Test Suite

```typescript
describe('Icon Verification', () => {
  beforeEach(() => {
    cy.visit('/users')
  })

  it('should verify all icons are loaded', () => {
    // 1. Verify library is loaded
    cy.verifyPrimeIconsLoaded()

    // 2. Verify specific buttons with icons
    cy.contains('button', 'Create User').verifyIcon('pi-plus')
    cy.verifyButtonWithIcon('[data-testid="view-user-button"]', 'pi-eye')

    // 3. Verify all icons on page use correct font
    cy.get('i.pi').each(($icon) => {
      cy.wrap($icon).should(($el) => {
        const fontFamily = $el.css('font-family')
        expect(fontFamily).to.include('primeicons')
      })
    })
  })
})
```

## Common Icon Classes

Here are some commonly used PrimeIcons classes:

| Icon | Class | Usage |
|------|-------|-------|
| ➕ Plus | `pi-plus` | Create/Add actions |
| 👁️ Eye | `pi-eye` | View/Show actions |
| ✏️ Pencil | `pi-pencil` | Edit actions |
| 🗑️ Trash | `pi-trash` | Delete actions |
| ✓ Check | `pi-check` | Confirm/Success |
| ✕ Times | `pi-times` | Close/Cancel |
| ⚠️ Warning | `pi-exclamation-triangle` | Warnings |
| ℹ️ Info | `pi-info-circle` | Information |

## Troubleshooting

### Icons not loading in tests

1. **Check CSS import** in `main.ts`:
   ```typescript
   import 'primeicons/primeicons.css'
   ```

2. **Check package.json** has primeicons dependency:
   ```json
   "primeicons": "^7.0.0"
   ```

3. **Verify index.html** has no CSP blocking fonts:
   ```html
   <!-- Should allow font loading -->
   <meta http-equiv="Content-Security-Policy" content="font-src 'self' data:;">
   ```

### Font not rendering in browser

If icons show as boxes (□) in the browser:
- Clear browser cache
- Check Network tab for failed font requests
- Verify font files in `node_modules/primeicons/fonts/`

### Tests passing but icons not visible

If tests pass but you can't see icons visually:
- Check z-index and positioning CSS
- Verify icon color isn't matching background
- Inspect element to see if font-size is set to 0

## Best Practices

1. **Always verify icon library is loaded** at the start of test suites that rely on icons
2. **Use data-testid attributes** on buttons with icons for reliable selection
3. **Test icon visibility on all breakpoints** if responsive design changes icon display
4. **Include icon tests in CI/CD pipeline** to catch build configuration issues early

## Additional Resources

- [PrimeIcons Documentation](https://primevue.org/icons/)
- [Cypress Best Practices](https://docs.cypress.io/guides/references/best-practices)
- [PrimeVue Component Testing](https://primevue.org/installation/#usage)
