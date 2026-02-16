# Camp Management API - Quick Start Guide

## Getting Started in 5 Minutes

This guide will help you get up and running with the Camp Management API quickly.

---

## Prerequisites

- Valid JWT token with Board or Admin role
- API base URL: `https://api.example.com` (replace with your actual URL)
- HTTP client (curl, Postman, or similar)

---

## Step 1: Authentication

All API requests require authentication. Include your JWT token in the Authorization header:

```bash
export TOKEN="your_jwt_token_here"
```

Test authentication with the health check:

```bash
curl -X GET https://api.example.com/health
```

Expected response:
```json
{
  "status": "healthy",
  "timestamp": "2026-02-15T10:00:00Z"
}
```

---

## Step 2: Check Current Age Ranges

Before creating camps, check the current age range configuration:

```bash
curl -X GET https://api.example.com/api/settings/age-ranges \
  -H "Authorization: Bearer $TOKEN"
```

Expected response:
```json
{
  "success": true,
  "data": {
    "babyMaxAge": 2,
    "childMinAge": 3,
    "childMaxAge": 12,
    "adultMinAge": 13,
    "updatedBy": "...",
    "updatedAt": "2026-02-15T10:00:00Z"
  }
}
```

**Age Categories:**
- Baby: 0-2 years
- Child: 3-12 years
- Adult: 13+ years

---

## Step 3: Create Your First Camp

Create a camp location with age-based pricing:

```bash
curl -X POST https://api.example.com/api/camps \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Summer Family Camp 2026",
    "description": "Our annual summer retreat for families",
    "location": "Yosemite National Park, CA",
    "latitude": 37.8651,
    "longitude": -119.5383,
    "pricePerAdult": 180.00,
    "pricePerChild": 120.00,
    "pricePerBaby": 60.00
  }'
```

Expected response:
```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Summer Family Camp 2026",
    "description": "Our annual summer retreat for families",
    "location": "Yosemite National Park, CA",
    "latitude": 37.8651,
    "longitude": -119.5383,
    "pricePerAdult": 180.00,
    "pricePerChild": 120.00,
    "pricePerBaby": 60.00,
    "isActive": true,
    "createdAt": "2026-02-15T10:05:00Z",
    "updatedAt": "2026-02-15T10:05:00Z"
  }
}
```

**Save the camp ID** for the next steps!

---

## Step 4: List All Camps

Retrieve all active camps:

```bash
curl -X GET "https://api.example.com/api/camps?isActive=true" \
  -H "Authorization: Bearer $TOKEN"
```

---

## Step 5: Update a Camp

Update the camp pricing or details:

```bash
curl -X PUT https://api.example.com/api/camps/3fa85f64-5717-4562-b3fc-2c963f66afa6 \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Summer Family Camp 2026",
    "description": "Our annual summer retreat - NOW WITH LAKE ACCESS!",
    "location": "Yosemite National Park, CA",
    "latitude": 37.8651,
    "longitude": -119.5383,
    "pricePerAdult": 200.00,
    "pricePerChild": 140.00,
    "pricePerBaby": 70.00,
    "isActive": true
  }'
```

---

## Step 6: (Optional) Update Age Ranges

If you need to change the age range boundaries:

```bash
curl -X PUT https://api.example.com/api/settings/age-ranges \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "babyMaxAge": 3,
    "childMinAge": 4,
    "childMaxAge": 14,
    "adultMinAge": 15
  }'
```

**Note:** This affects all camps and editions association-wide!

---

## Common Workflows

### Workflow 1: Setting Up Multiple Camps

```bash
# 1. Create first camp
curl -X POST https://api.example.com/api/camps \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "Mountain Camp", "pricePerAdult": 180, "pricePerChild": 120, "pricePerBaby": 60}'

# 2. Create second camp
curl -X POST https://api.example.com/api/camps \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "Beach Camp", "pricePerAdult": 200, "pricePerChild": 140, "pricePerBaby": 70}'

# 3. List all camps
curl -X GET https://api.example.com/api/camps \
  -H "Authorization: Bearer $TOKEN"
```

### Workflow 2: Deactivating a Camp

Instead of deleting, deactivate camps to preserve history:

```bash
curl -X PUT https://api.example.com/api/camps/{id} \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Old Camp",
    "pricePerAdult": 180,
    "pricePerChild": 120,
    "pricePerBaby": 60,
    "isActive": false
  }'
```

---

## Troubleshooting

### Error: 401 Unauthorized

**Problem:** Missing or invalid authentication token

**Solution:**
1. Verify your token is valid
2. Check token hasn't expired
3. Ensure token is included in Authorization header: `Bearer <token>`

---

### Error: 403 Forbidden

**Problem:** User doesn't have sufficient permissions

**Solution:**
1. Verify user has Board or Admin role
2. Check JWT token contains correct role claims

---

### Error: 400 Bad Request - Validation Error

**Problem:** Request data doesn't meet validation requirements

**Solution:**
1. Check all required fields are provided
2. Verify field lengths (name: 200 chars, description: 2000 chars)
3. Ensure prices are non-negative
4. Validate GPS coordinates:
   - Latitude: -90 to 90
   - Longitude: -180 to 180
5. For age ranges, ensure no overlaps:
   - Baby max < Child min
   - Child max < Adult min

---

### Error: 404 Not Found

**Problem:** Resource doesn't exist

**Solution:**
1. Verify the camp ID is correct
2. Check camp hasn't been deleted
3. Use GET /api/camps to list all camps

---

## Testing with Postman

### Import Collection

1. Create a new collection named "Abuvi Camp Management"
2. Set collection-level authorization:
   - Type: Bearer Token
   - Token: `{{jwt_token}}`
3. Add environment variable `jwt_token` with your token

### Sample Requests

Create these requests in your collection:

1. **Health Check**
   - GET `{{base_url}}/health`

2. **Get Age Ranges**
   - GET `{{base_url}}/api/settings/age-ranges`

3. **Create Camp**
   - POST `{{base_url}}/api/camps`
   - Body: See Step 3 above

4. **List Camps**
   - GET `{{base_url}}/api/camps?isActive=true`

5. **Get Camp**
   - GET `{{base_url}}/api/camps/{{camp_id}}`

6. **Update Camp**
   - PUT `{{base_url}}/api/camps/{{camp_id}}`
   - Body: See Step 5 above

7. **Delete Camp**
   - DELETE `{{base_url}}/api/camps/{{camp_id}}`

---

## Next Steps

After mastering the basics:

1. **Read Full API Documentation:** See [camps-api.md](./camps-api.md) for complete endpoint details
2. **Explore Camp Editions:** Once camps are created, you can create specific editions with dates and custom pricing
3. **Add Extras:** Configure additional services/products for camp editions
4. **Set Up Registrations:** Allow families to register for camp editions

---

## Code Examples

### JavaScript/TypeScript (fetch)

```typescript
async function createCamp(token: string) {
  const response = await fetch('https://api.example.com/api/camps', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      name: 'Summer Family Camp 2026',
      description: 'Our annual summer retreat',
      location: 'Yosemite National Park, CA',
      latitude: 37.8651,
      longitude: -119.5383,
      pricePerAdult: 180.00,
      pricePerChild: 120.00,
      pricePerBaby: 60.00
    })
  });

  const result = await response.json();

  if (result.success) {
    console.log('Camp created:', result.data.id);
    return result.data;
  } else {
    console.error('Error:', result.error.message);
    throw new Error(result.error.message);
  }
}
```

### Python (requests)

```python
import requests

def create_camp(token):
    url = 'https://api.example.com/api/camps'
    headers = {
        'Authorization': f'Bearer {token}',
        'Content-Type': 'application/json'
    }
    data = {
        'name': 'Summer Family Camp 2026',
        'description': 'Our annual summer retreat',
        'location': 'Yosemite National Park, CA',
        'latitude': 37.8651,
        'longitude': -119.5383,
        'pricePerAdult': 180.00,
        'pricePerChild': 120.00,
        'pricePerBaby': 60.00
    }

    response = requests.post(url, json=data, headers=headers)
    result = response.json()

    if result['success']:
        print(f"Camp created: {result['data']['id']}")
        return result['data']
    else:
        print(f"Error: {result['error']['message']}")
        raise Exception(result['error']['message'])
```

### C# (.NET)

```csharp
using System.Net.Http;
using System.Net.Http.Json;

public async Task<CampResponse> CreateCampAsync(string token)
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", token);

    var request = new CreateCampRequest
    {
        Name = "Summer Family Camp 2026",
        Description = "Our annual summer retreat",
        Location = "Yosemite National Park, CA",
        Latitude = 37.8651m,
        Longitude = -119.5383m,
        PricePerAdult = 180.00m,
        PricePerChild = 120.00m,
        PricePerBaby = 60.00m
    };

    var response = await client.PostAsJsonAsync(
        "https://api.example.com/api/camps",
        request);

    response.EnsureSuccessStatusCode();

    var result = await response.Content
        .ReadFromJsonAsync<ApiResponse<CampResponse>>();

    if (result.Success)
    {
        Console.WriteLine($"Camp created: {result.Data.Id}");
        return result.Data;
    }
    else
    {
        throw new Exception(result.Error.Message);
    }
}
```

---

## Rate Limiting

Currently, there are no rate limits on the API. However, best practices suggest:

- Cache responses when appropriate
- Use pagination for large datasets
- Batch operations when possible

---

## Support & Resources

- **Full API Documentation:** [camps-api.md](./camps-api.md)
- **GitHub Repository:** https://github.com/nessuarez/abuvi-app
- **Issue Tracker:** https://github.com/nessuarez/abuvi-app/issues
- **API Status:** Check `/health` endpoint

---

**Last Updated:** February 15, 2026
**Version:** 1.0
