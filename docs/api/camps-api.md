# Camp Locations & Age Ranges API Documentation

## Overview

The Camp Locations API provides CRUD operations for managing camp locations with age-based pricing templates. The Age Ranges API allows Board members to configure the association-wide age range definitions used for pricing calculations.

**Base URL:** `/api`

**Version:** 1.0

**Authentication:** JWT Bearer Token required

**Authorization:** Board+ role required for all operations

---

## Table of Contents

1. [Authentication](#authentication)
2. [Camp Locations API](#camp-locations-api)
3. [Age Ranges Configuration API](#age-ranges-configuration-api)
4. [Data Models](#data-models)
5. [Error Handling](#error-handling)
6. [Examples](#examples)

---

## Authentication

All endpoints require JWT authentication with Board or Admin role.

### Request Headers

```http
Authorization: Bearer <jwt_token>
Content-Type: application/json
```

### Required Roles

- **Board**: Can access all camp management and settings endpoints
- **Admin**: Can access all camp management and settings endpoints

---

## Camp Locations API

### Endpoints

#### 1. Get All Camps

Retrieve a list of all camp locations with optional filtering and pagination.

**Endpoint:** `GET /api/camps`

**Query Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `isActive` | boolean | No | - | Filter by active status |
| `skip` | integer | No | 0 | Number of records to skip (pagination) |
| `take` | integer | No | 100 | Number of records to return (max 100) |

**Response:** `200 OK`

```json
{
  "success": true,
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Mountain Retreat Camp",
      "description": "Beautiful mountain location with lake access",
      "location": "Sierra Nevada Mountains, CA",
      "latitude": 37.7749,
      "longitude": -119.4194,
      "pricePerAdult": 180.00,
      "pricePerChild": 120.00,
      "pricePerBaby": 60.00,
      "isActive": true,
      "createdAt": "2026-02-14T12:00:00Z",
      "updatedAt": "2026-02-14T12:00:00Z"
    }
  ],
  "error": null
}
```

---

#### 2. Get Camp by ID

Retrieve a specific camp location by its unique identifier.

**Endpoint:** `GET /api/camps/{id}`

**Path Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | UUID | Yes | Camp identifier |

**Response:** `200 OK`

```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Mountain Retreat Camp",
    "description": "Beautiful mountain location with lake access",
    "location": "Sierra Nevada Mountains, CA",
    "latitude": 37.7749,
    "longitude": -119.4194,
    "pricePerAdult": 180.00,
    "pricePerChild": 120.00,
    "pricePerBaby": 60.00,
    "isActive": true,
    "createdAt": "2026-02-14T12:00:00Z",
    "updatedAt": "2026-02-14T12:00:00Z"
  },
  "error": null
}
```

**Error Response:** `404 Not Found`

```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "Camp not found",
    "code": "NOT_FOUND"
  }
}
```

---

#### 3. Create Camp

Create a new camp location with age-based pricing template.

**Endpoint:** `POST /api/camps`

**Request Body:**

```json
{
  "name": "Mountain Retreat Camp",
  "description": "Beautiful mountain location with lake access",
  "location": "Sierra Nevada Mountains, CA",
  "latitude": 37.7749,
  "longitude": -119.4194,
  "pricePerAdult": 180.00,
  "pricePerChild": 120.00,
  "pricePerBaby": 60.00
}
```

**Validation Rules:**

- `name`: Required, max 200 characters
- `description`: Optional, max 2000 characters
- `location`: Optional, max 500 characters
- `latitude`: Optional, must be between -90 and 90
- `longitude`: Optional, must be between -180 and 180
- `pricePerAdult`: Required, must be >= 0
- `pricePerChild`: Required, must be >= 0
- `pricePerBaby`: Required, must be >= 0

**Response:** `201 Created`

```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Mountain Retreat Camp",
    "description": "Beautiful mountain location with lake access",
    "location": "Sierra Nevada Mountains, CA",
    "latitude": 37.7749,
    "longitude": -119.4194,
    "pricePerAdult": 180.00,
    "pricePerChild": 120.00,
    "pricePerBaby": 60.00,
    "isActive": true,
    "createdAt": "2026-02-14T12:00:00Z",
    "updatedAt": "2026-02-14T12:00:00Z"
  },
  "error": null
}
```

**Response Headers:**

```
Location: /api/camps/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

---

#### 4. Update Camp

Update an existing camp location.

**Endpoint:** `PUT /api/camps/{id}`

**Path Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | UUID | Yes | Camp identifier |

**Request Body:**

```json
{
  "name": "Mountain Retreat Camp (Updated)",
  "description": "Updated description with new amenities",
  "location": "Sierra Nevada Mountains, CA",
  "latitude": 37.7749,
  "longitude": -119.4194,
  "pricePerAdult": 200.00,
  "pricePerChild": 140.00,
  "pricePerBaby": 70.00,
  "isActive": true
}
```

**Validation Rules:** Same as Create Camp

**Response:** `200 OK`

```json
{
  "success": true,
  "data": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Mountain Retreat Camp (Updated)",
    "description": "Updated description with new amenities",
    "location": "Sierra Nevada Mountains, CA",
    "latitude": 37.7749,
    "longitude": -119.4194,
    "pricePerAdult": 200.00,
    "pricePerChild": 140.00,
    "pricePerBaby": 70.00,
    "isActive": true,
    "createdAt": "2026-02-14T12:00:00Z",
    "updatedAt": "2026-02-14T14:30:00Z"
  },
  "error": null
}
```

---

#### 5. Delete Camp

Delete a camp location. Cannot delete camps with existing editions.

**Endpoint:** `DELETE /api/camps/{id}`

**Path Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | UUID | Yes | Camp identifier |

**Response:** `204 No Content`

**Error Response:** `400 Bad Request` (if camp has editions)

```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "Cannot delete camp with existing editions",
    "code": "OPERATION_ERROR"
  }
}
```

---

## Age Ranges Configuration API

### Endpoints

#### 1. Get Age Ranges

Retrieve the current age ranges configuration.

**Endpoint:** `GET /api/settings/age-ranges`

**Response:** `200 OK`

```json
{
  "success": true,
  "data": {
    "babyMaxAge": 2,
    "childMinAge": 3,
    "childMaxAge": 12,
    "adultMinAge": 13,
    "updatedBy": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "updatedAt": "2026-02-14T12:00:00Z"
  },
  "error": null
}
```

**Error Response:** `404 Not Found`

```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "Age ranges configuration not found",
    "code": "NOT_FOUND"
  }
}
```

---

#### 2. Update Age Ranges

Update the age ranges configuration. Only Board+ can perform this operation.

**Endpoint:** `PUT /api/settings/age-ranges`

**Request Body:**

```json
{
  "babyMaxAge": 3,
  "childMinAge": 4,
  "childMaxAge": 14,
  "adultMinAge": 15
}
```

**Validation Rules:**

- All age values must be non-negative
- `babyMaxAge` must be less than `childMinAge`
- `childMaxAge` must be less than `adultMinAge`
- Age ranges must not overlap

**Response:** `200 OK`

```json
{
  "success": true,
  "data": {
    "babyMaxAge": 3,
    "childMinAge": 4,
    "childMaxAge": 14,
    "adultMinAge": 15,
    "updatedBy": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "updatedAt": "2026-02-14T14:30:00Z"
  },
  "error": null
}
```

**Error Response:** `400 Bad Request`

```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "Age ranges must not overlap: baby max age must be less than child min age",
    "code": "VALIDATION_ERROR"
  }
}
```

---

## Data Models

### CampResponse

| Field | Type | Description |
|-------|------|-------------|
| `id` | UUID | Unique identifier |
| `name` | string | Camp name (max 200 chars) |
| `description` | string? | Optional description (max 2000 chars) |
| `location` | string? | Optional location description (max 500 chars) |
| `latitude` | decimal? | GPS latitude (-90 to 90) |
| `longitude` | decimal? | GPS longitude (-180 to 180) |
| `pricePerAdult` | decimal | Price per adult (>= 0) |
| `pricePerChild` | decimal | Price per child (>= 0) |
| `pricePerBaby` | decimal | Price per baby (>= 0) |
| `isActive` | boolean | Whether camp is active |
| `createdAt` | datetime | Creation timestamp (UTC) |
| `updatedAt` | datetime | Last update timestamp (UTC) |

### AgeRangesResponse

| Field | Type | Description |
|-------|------|-------------|
| `babyMaxAge` | integer | Maximum age for baby category |
| `childMinAge` | integer | Minimum age for child category |
| `childMaxAge` | integer | Maximum age for child category |
| `adultMinAge` | integer | Minimum age for adult category |
| `updatedBy` | UUID? | User who last updated the settings |
| `updatedAt` | datetime | Last update timestamp (UTC) |

---

## Error Handling

### Standard Error Response

All error responses follow this format:

```json
{
  "success": false,
  "data": null,
  "error": {
    "message": "Error description",
    "code": "ERROR_CODE"
  }
}
```

### HTTP Status Codes

| Code | Description |
|------|-------------|
| 200 | OK - Request successful |
| 201 | Created - Resource created successfully |
| 204 | No Content - Delete successful |
| 400 | Bad Request - Validation error |
| 401 | Unauthorized - Missing or invalid authentication |
| 403 | Forbidden - Insufficient permissions |
| 404 | Not Found - Resource not found |
| 500 | Internal Server Error - Unexpected error |

### Error Codes

| Code | Description |
|------|-------------|
| `VALIDATION_ERROR` | Request validation failed |
| `NOT_FOUND` | Resource not found |
| `OPERATION_ERROR` | Operation cannot be completed |

---

## Examples

### Example 1: Create a New Camp

**Request:**

```bash
curl -X POST https://api.example.com/api/camps \
  -H "Authorization: Bearer eyJhbGc..." \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Lakeside Family Camp",
    "description": "Perfect for families with water activities",
    "location": "Lake Tahoe, CA",
    "latitude": 39.0968,
    "longitude": -120.0324,
    "pricePerAdult": 175.00,
    "pricePerChild": 115.00,
    "pricePerBaby": 55.00
  }'
```

**Response:**

```json
{
  "success": true,
  "data": {
    "id": "a1b2c3d4-5e6f-7g8h-9i0j-k1l2m3n4o5p6",
    "name": "Lakeside Family Camp",
    "description": "Perfect for families with water activities",
    "location": "Lake Tahoe, CA",
    "latitude": 39.0968,
    "longitude": -120.0324,
    "pricePerAdult": 175.00,
    "pricePerChild": 115.00,
    "pricePerBaby": 55.00,
    "isActive": true,
    "createdAt": "2026-02-14T15:00:00Z",
    "updatedAt": "2026-02-14T15:00:00Z"
  },
  "error": null
}
```

---

### Example 2: Update Age Ranges Configuration

**Request:**

```bash
curl -X PUT https://api.example.com/api/settings/age-ranges \
  -H "Authorization: Bearer eyJhbGc..." \
  -H "Content-Type: application/json" \
  -d '{
    "babyMaxAge": 2,
    "childMinAge": 3,
    "childMaxAge": 13,
    "adultMinAge": 14
  }'
```

**Response:**

```json
{
  "success": true,
  "data": {
    "babyMaxAge": 2,
    "childMinAge": 3,
    "childMaxAge": 13,
    "adultMinAge": 14,
    "updatedBy": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "updatedAt": "2026-02-14T15:15:00Z"
  },
  "error": null
}
```

---

### Example 3: List Active Camps with Pagination

**Request:**

```bash
curl -X GET "https://api.example.com/api/camps?isActive=true&skip=0&take=10" \
  -H "Authorization: Bearer eyJhbGc..."
```

**Response:**

```json
{
  "success": true,
  "data": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Mountain Retreat Camp",
      "description": "Beautiful mountain location",
      "location": "Sierra Nevada, CA",
      "latitude": 37.7749,
      "longitude": -119.4194,
      "pricePerAdult": 180.00,
      "pricePerChild": 120.00,
      "pricePerBaby": 60.00,
      "isActive": true,
      "createdAt": "2026-02-14T12:00:00Z",
      "updatedAt": "2026-02-14T12:00:00Z"
    },
    {
      "id": "a1b2c3d4-5e6f-7g8h-9i0j-k1l2m3n4o5p6",
      "name": "Lakeside Family Camp",
      "description": "Perfect for families",
      "location": "Lake Tahoe, CA",
      "latitude": 39.0968,
      "longitude": -120.0324,
      "pricePerAdult": 175.00,
      "pricePerChild": 115.00,
      "pricePerBaby": 55.00,
      "isActive": true,
      "createdAt": "2026-02-14T15:00:00Z",
      "updatedAt": "2026-02-14T15:00:00Z"
    }
  ],
  "error": null
}
```

---

## Best Practices

### 1. Age-Based Pricing

The pricing fields in camps serve as **templates** for camp editions. When creating a camp edition, these prices are used as defaults but can be overridden.

Age categories are determined by the age ranges configuration:
- **Baby:** 0 to `babyMaxAge`
- **Child:** `childMinAge` to `childMaxAge`
- **Adult:** `adultMinAge` and above

### 2. GPS Coordinates

When providing GPS coordinates:
- Latitude must be between -90 (South Pole) and 90 (North Pole)
- Longitude must be between -180 and 180
- Use decimal degrees format (not degrees-minutes-seconds)
- Both latitude and longitude must be provided together (or both null)

### 3. Soft Delete

Camps use a soft delete pattern via the `isActive` flag. Instead of deleting camps, consider setting `isActive` to `false` to preserve historical data.

### 4. Audit Trail

All camps track:
- `createdAt`: When the camp was created
- `updatedAt`: When the camp was last modified

Age ranges track:
- `updatedBy`: User ID who last modified the settings
- `updatedAt`: When the settings were last modified

### 5. Pagination

For large datasets, use pagination:
- Default: `skip=0`, `take=100`
- Maximum `take` value: 100
- Results are ordered by camp name (alphabetically)

---

## Support

For questions or issues with the API:
- **Documentation:** https://docs.example.com
- **GitHub Issues:** https://github.com/nessuarez/abuvi-app/issues
- **API Version:** Check the `/health` endpoint for system status

---

**Last Updated:** February 14, 2026
**API Version:** 1.0
**Author:** Abuvi Development Team
