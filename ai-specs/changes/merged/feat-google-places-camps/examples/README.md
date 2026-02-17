# Google Places API - Ejemplos de Respuestas

Este directorio contiene ejemplos de respuestas de Google Places API que se usarán como referencia para la implementación.

## Archivos

### google-places-request.json

### google-places-response.json

Respuesta de ejemplo del endpoint **Place Details**.

### google-places-autocomplete-request.json

### google-places-autocomplete-response.json

Respuesta de ejemplo del endpoint **Autocomplete**.

**Cómo obtener un ejemplo:**

1. **Crear un proyecto en Google Cloud Console:**
   - Ve a <https://console.cloud.google.com/>
   - Crea un nuevo proyecto o selecciona uno existente
   - Habilita "Places API"

2. **Obtener API Key:**
   - Ve a "Credenciales"
   - Crea una API Key
   - Restricciones recomendadas: Solo Places API, solo tu IP

3. **Hacer una llamada de prueba:**

   **Paso 1 - Autocomplete (para obtener place_id):**

   ```bash
   curl "https://maps.googleapis.com/maps/api/place/autocomplete/json?input=camping%20madrid&key=TU_API_KEY&language=es"
   ```

   **Paso 2 - Place Details (con el place_id del paso 1):**

   ```bash
   curl "https://maps.googleapis.com/maps/api/place/details/json?place_id=PLACE_ID_DEL_PASO_1&fields=name,formatted_address,geometry,types,photos,website,formatted_phone_number&key=TU_API_KEY&language=es"
   ```

4. **Copiar la respuesta:**
   - Copia toda la respuesta JSON del paso 2
   - Pégala en `google-places-response.json` reemplazando el contenido actual

5. **Verificar estructura:**
   - Asegúrate de que la respuesta incluye:
     - `result.name`
     - `result.formatted_address`
     - `result.geometry.location.lat`
     - `result.geometry.location.lng`
     - `result.types`
     - `result.place_id`

## Campamentos Sugeridos para Pruebas

Aquí algunos campamentos reales en España que puedes usar para obtener ejemplos:

1. **Camping Pico de la Miel** - Cercedilla, Madrid
2. **Camping Aranjuez** - Aranjuez, Madrid
3. **Camping El Escorial** - San Lorenzo de El Escorial, Madrid
4. **Camping Alpha** - Galapagar, Madrid

## Notas de Seguridad

⚠️ **IMPORTANTE:**

- NO incluyas tu API Key real en estos archivos
- La API Key debe estar solo en:
  - Variables de entorno (producción)
  - User Secrets (desarrollo)
  - `.env.local` (nunca en git)

## Uso de los Ejemplos

Estos archivos JSON se usarán para:

1. Entender la estructura de datos de la API
2. Diseñar el mapeo de datos a nuestro modelo Camp
3. Crear tests unitarios con datos mockeados
4. Documentación y referencia durante el desarrollo
