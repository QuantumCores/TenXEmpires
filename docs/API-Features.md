# API Production Features

This document describes the production-ready features implemented in the TenX Empires API.

## Table of Contents
- [Rate Limiting](#rate-limiting)
- [CORS Configuration](#cors-configuration)
- [API Versioning](#api-versioning)
- [HTTP Caching](#http-caching)

---

## Rate Limiting

Rate limiting protects the API from abuse and ensures fair usage across all clients.

### Policies

The API implements three rate limiting policies:

#### 1. **Global Limiter** (Default)
- **Limit**: 100 requests per minute
- **Applied to**: All endpoints by default
- **Partition by**: Authenticated user name or host

#### 2. **PublicApi Policy**
- **Limit**: 300 requests per minute
- **Applied to**: Public endpoints (lookups, static data)
- **Partition by**: Host
- **Used by**: Unit Definitions endpoint

#### 3. **AuthenticatedApi Policy**
- **Limit**: 60 requests per minute
- **Applied to**: Authenticated user-specific endpoints
- **Partition by**: User identity

### Rate Limit Response

When rate limit is exceeded, the API returns:

**Status Code**: `429 Too Many Requests`

**Response Body**:
```json
{
  "code": "RATE_LIMIT_EXCEEDED",
  "message": "Too many requests. Please try again later.",
  "retryAfterSeconds": 60
}
```

**Headers**:
- `Retry-After`: Seconds until the rate limit resets

### Applying Rate Limiting

To apply a specific rate limiting policy to a controller or action:

```csharp
[EnableRateLimiting("PublicApi")]
public class MyController : ControllerBase
{
    // All actions use PublicApi policy
}
```

Or for specific actions:

```csharp
[EnableRateLimiting("AuthenticatedApi")]
public async Task<IActionResult> MyAction()
{
    // This action uses AuthenticatedApi policy
}
```

---

## CORS Configuration

CORS (Cross-Origin Resource Sharing) allows browser-based clients to access the API from different origins.

### Configuration

CORS settings are configured in `appsettings.json`:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://yourdomain.com",
      "https://www.yourdomain.com"
    ],
    "AllowCredentials": true,
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
    "AllowedHeaders": ["*"],
    "ExposedHeaders": ["ETag", "X-Total-Count"]
  }
}
```

### Development Settings

For local development, `appsettings.Development.json` includes common localhost ports:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5173",
      "http://localhost:3000",
      "https://localhost:5173",
      "https://localhost:3000",
      "https://localhost:55414"
    ],
    "AllowCredentials": true,
    "AllowedMethods": ["*"],
    "AllowedHeaders": ["*"],
    "ExposedHeaders": ["ETag", "X-Total-Count", "X-Request-Id"]
  }
}
```

### Settings Explained

- **AllowedOrigins**: List of origins that can access the API
  - Use `["*"]` to allow all origins (not recommended for production)
- **AllowCredentials**: Allow cookies and authentication headers
  - Must be `false` if AllowedOrigins is `*`
- **AllowedMethods**: HTTP methods allowed from browser clients
  - Use `["*"]` to allow all methods
- **AllowedHeaders**: Request headers allowed from clients
  - Use `["*"]` to allow all headers
- **ExposedHeaders**: Response headers that browsers can access
  - Important for custom headers like `ETag`, pagination headers, etc.

### Production Recommendations

1. **Never use `AllowedOrigins: ["*"]` in production**
2. Explicitly list all allowed origins
3. Minimize exposed headers to only what's necessary
4. Set `AllowCredentials: false` unless authentication is required

---

## API Versioning

API versioning ensures backward compatibility as the API evolves.

### Versioning Strategy

The API uses **URL Path Versioning** as the primary method:

```
GET /v1/unit-definitions
GET /v2/unit-definitions
```

### Alternative Versioning Methods

The API also supports versioning via:

#### 1. **Header-based Versioning**
```http
GET /unit-definitions
X-Api-Version: 1.0
```

#### 2. **Media Type Versioning**
```http
GET /unit-definitions
Accept: application/json;version=1.0
```

### Current Version

- **Default Version**: `v1.0`
- **Assumes default when unspecified**: Yes
- **Reports API version in response headers**: Yes

### Response Headers

All responses include version information:

```http
X-Api-Version: 1.0
X-Api-Supported-Versions: 1.0
```

### Creating a New Version

To create a new version of an endpoint:

```csharp
[ApiController]
[ApiVersion("2.0")]
[Route("v{version:apiVersion}/unit-definitions")]
public class UnitDefinitionsV2Controller : ControllerBase
{
    // Version 2.0 implementation
}
```

### Deprecating a Version

To mark a version as deprecated:

```csharp
[ApiVersion("1.0", Deprecated = true)]
public class MyController : ControllerBase
{
    // This version is deprecated
}
```

Swagger will automatically mark deprecated endpoints.

### Swagger Documentation

Swagger UI organizes endpoints by version:

```
http://localhost:5000/swagger/index.html
```

Select the version from the dropdown to view version-specific documentation.

---

## HTTP Caching

The API implements sophisticated HTTP caching to reduce bandwidth and improve performance.

### ETag Support

ETags provide efficient cache validation:

```http
GET /v1/unit-definitions
Response:
  Status: 200 OK
  ETag: "abc123def456"
  Cache-Control: public, max-age=600
```

Subsequent requests with the ETag:

```http
GET /v1/unit-definitions
If-None-Match: "abc123def456"

Response:
  Status: 304 Not Modified
```

### Cache-Control Headers

The API sets appropriate cache directives:

- **Public endpoints**: `Cache-Control: public, max-age=600`
  - Can be cached by browsers and CDNs
  - Valid for 10 minutes (600 seconds)

- **Private endpoints**: `Cache-Control: private, max-age=300`
  - Only cached by the browser
  - Valid for 5 minutes (300 seconds)

### Benefits

1. **Reduced bandwidth**: 304 responses have no body
2. **Faster responses**: Cached responses are instant
3. **Lower server load**: Fewer database queries
4. **Better scalability**: CDN can cache public endpoints

### Best Practices for Clients

1. **Always send `If-None-Match`** with the stored ETag
2. **Respect `Cache-Control` directives**
3. **Handle 304 responses** by using cached data
4. **Check `X-Api-Version`** for version changes

---

## Testing the Features

### Testing Rate Limiting

```bash
# Make multiple rapid requests
for i in {1..350}; do
  curl http://localhost:5000/v1/unit-definitions
done

# After 300 requests, you should receive 429 responses
```

### Testing CORS

```javascript
// From a browser console on a different origin
fetch('http://localhost:5000/v1/unit-definitions')
  .then(response => response.json())
  .then(data => console.log(data));

// If origin is allowed, request succeeds
// If origin is blocked, browser shows CORS error
```

### Testing API Versioning

```bash
# URL-based versioning
curl http://localhost:5000/v1/unit-definitions

# Header-based versioning
curl -H "X-Api-Version: 1.0" http://localhost:5000/unit-definitions

# Check version in response
curl -I http://localhost:5000/v1/unit-definitions
# Look for: X-Api-Version: 1.0
```

### Testing HTTP Caching

```bash
# First request - gets full response
curl -i http://localhost:5000/v1/unit-definitions

# Note the ETag value, e.g., "abc123"

# Second request with ETag
curl -i -H 'If-None-Match: "abc123"' http://localhost:5000/v1/unit-definitions

# Should return 304 Not Modified
```

---

## Configuration Summary

### appsettings.json

```json
{
  "Cors": {
    "AllowedOrigins": ["https://yourdomain.com"],
    "AllowCredentials": true,
    "AllowedMethods": ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
    "AllowedHeaders": ["*"],
    "ExposedHeaders": ["ETag", "X-Total-Count"]
  }
}
```

### Rate Limiting Configuration

Rate limiting is configured in code (`Program.cs`) and can be customized:

- Change permit limits
- Adjust time windows
- Create new policies
- Customize rejection responses

### API Versioning Configuration

Versioning is configured in `Program.cs`:

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});
```

---

## Security Considerations

1. **Rate Limiting**: Protects against DoS attacks and abuse
2. **CORS**: Prevents unauthorized cross-origin access
3. **API Versioning**: Ensures clients can migrate safely
4. **HTTP Caching**: Reduces server load without compromising data freshness

---

## Monitoring and Metrics

Consider adding monitoring for:

- Rate limit rejections per endpoint
- CORS violations
- API version usage distribution
- Cache hit rates

These metrics help optimize limits and identify issues.

