# Camp Management API Documentation

Welcome to the Abuvi Camp Management API documentation!

## 📚 Documentation Index

### Getting Started
- **[Quick Start Guide](./quick-start.md)** - Get up and running in 5 minutes
- **[Full API Reference](./camps-api.md)** - Complete endpoint documentation
- **[Configuration Reference](../configuration.md)** - All `appsettings.json` settings and production Docker setup

### API Sections

1. **Camp Locations API** - Manage camp locations with age-based pricing
   - CRUD operations for camps
   - GPS coordinates support
   - Age-based pricing templates

2. **Age Ranges Configuration API** - Configure association-wide age ranges
   - Get/Update age range boundaries
   - Validation and audit trail
   - Board+ only access

## 🚀 Quick Links

- **Base URL:** `https://api.example.com/api`
- **Authentication:** JWT Bearer Token (Board+ role required)
- **Current Version:** 1.0
- **Status:** Production Ready ✅

## 📖 Available Documentation

| Document | Description |
|----------|-------------|
| [Quick Start](./quick-start.md) | 5-minute tutorial with code examples |
| [API Reference](./camps-api.md) | Complete endpoint documentation |

## 🎯 What You Can Do

### Camp Locations
- ✅ Create camp locations with pricing templates
- ✅ Update camp details and pricing
- ✅ List camps with filtering and pagination
- ✅ Soft delete with isActive flag
- ✅ GPS coordinates for mapping

### Age Ranges
- ✅ Configure global age boundaries
- ✅ Non-overlapping validation
- ✅ Audit trail tracking
- ✅ Applied association-wide

## 🔐 Authentication

All endpoints require:
- Valid JWT token
- Board or Admin role

```bash
curl -X GET https://api.example.com/api/camps \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

## 📊 Test Coverage

- **Unit Tests:** 29 tests (all passing ✅)
- **Integration Tests:** 5 tests (all passing ✅)
- **Code Coverage:** 90%+ on service layer

## 🛠️ Technology Stack

- **.NET 9** - Web API framework
- **PostgreSQL** - Database
- **EF Core** - ORM
- **FluentValidation** - Request validation
- **JWT** - Authentication
- **OpenAPI/Swagger** - API documentation

## 📝 Response Format

All responses follow a consistent format:

```json
{
  "success": true,
  "data": { /* response data */ },
  "error": null
}
```

## ⚠️ Common HTTP Status Codes

| Code | Description |
|------|-------------|
| 200 | OK - Request successful |
| 201 | Created - Resource created |
| 204 | No Content - Delete successful |
| 400 | Bad Request - Validation error |
| 401 | Unauthorized - Missing/invalid auth |
| 403 | Forbidden - Insufficient permissions |
| 404 | Not Found - Resource not found |

## 🔄 Pagination

Use `skip` and `take` parameters:

```bash
GET /api/camps?skip=0&take=10
```

- Default: `skip=0`, `take=100`
- Maximum `take`: 100

## 📅 Changelog

See implementation details:

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | Feb 15, 2026 | Initial release with Camps and Age Ranges APIs |

## 🤝 Support

- **Issues:** [GitHub Issues](https://github.com/nessuarez/abuvi-app/issues)
- **Discussions:** [GitHub Discussions](https://github.com/nessuarez/abuvi-app/discussions)
- **Email:** support@example.com

## 📦 What's Next?

Upcoming features (Phase 2+):
- Camp Editions (specific dates and years)
- Camp Extras (additional services)
- Family Registrations
- Payment Processing

## 🎓 Learning Resources

1. Start with the [Quick Start Guide](./quick-start.md)
2. Reference the [Full API Documentation](./camps-api.md)
3. Check code examples in each language
4. Test with the Swagger UI at `/swagger`

---

**Happy Coding! 🚀**

For questions or feedback, please open an issue on GitHub.
