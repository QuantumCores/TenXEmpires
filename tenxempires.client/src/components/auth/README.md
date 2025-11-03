# Authentication Components

This directory contains the authentication-related components for the TenXEmpires application.

## Components

### LoginForm
Handles user login with email and password.

**Features:**
- Zod validation for email format and required password
- Client-side validation with inline error messages
- Auto-focus on first field (email) and on validation errors
- Loading state during submission
- Accessibility with ARIA attributes
- Disabled state styling during submission

**Props:**
- `onSubmit: (model: LoginFormModel) => Promise<void>` - Callback when form is submitted
- `isSubmitting?: boolean` - Loading state
- `error?: string` - Server error message to display

### LoginSupportLinks
Provides links to "Forgot password?" and "Create account".

**Features:**
- Preserves `returnUrl` query parameter across navigation
- Opens forgot password modal via query param
- Links to registration page

### ForgotPasswordModal
Modal for password reset functionality.

**Status:** ⚠️ Backend endpoint not yet implemented (`/v1/auth/forgot-password`)

**Features:**
- Email validation
- Accessible modal with keyboard navigation
- Error handling for missing endpoint

### VerifyEmailModal
Modal for resending email verification.

**Status:** ⚠️ Backend endpoint not yet implemented (`/v1/auth/resend-verification`)

**Features:**
- Email validation
- Accessible modal with keyboard navigation
- Error handling for missing endpoint

## Usage

### Login Page Implementation

```tsx
import { LoginForm } from '../../components/auth/LoginForm'
import { LoginSupportLinks } from '../../components/auth/LoginSupportLinks'
import { ForgotPasswordModal } from '../../components/auth/ForgotPasswordModal'
import { VerifyEmailModal } from '../../components/auth/VerifyEmailModal'

export default function Login() {
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string>()
  
  async function handleSubmit(model: LoginFormModel) {
    setIsSubmitting(true)
    const { ok } = await postJson('/api/auth/login', model)
    setIsSubmitting(false)
    // Handle response...
  }
  
  return (
    <>
      <LoginForm 
        onSubmit={handleSubmit}
        isSubmitting={isSubmitting}
        error={error}
      />
      <LoginSupportLinks />
      
      {/* Modals render based on query params */}
      {modalType === 'forgot' && <ForgotPasswordModal onRequestClose={closeModal} />}
      {modalType === 'verify' && <VerifyEmailModal onRequestClose={closeModal} />}
    </>
  )
}
```

## Modal Routing

Modals are controlled via query parameters on the `/login` route:

- `/login?modal=forgot` - Opens Forgot Password modal
- `/login?modal=verify` - Opens Verify Email modal
- `/login?returnUrl=/game/123` - Redirects to specific URL after login

## API Integration

### Login Endpoint
- **URL:** `POST /v1/auth/login`
- **Body:** `{ email: string, password: string, rememberMe?: boolean }`
- **Success:** `204 No Content` + auth cookies set
- **Error:** `400 Bad Request` with `ApiErrorDto { code: string, message: string }`

### CSRF Protection
The application uses CSRF tokens for all auth mutations:
1. CSRF token is obtained via `GET /v1/auth/csrf` on app init
2. Token is stored in `XSRF-TOKEN` cookie (non-HttpOnly)
3. Frontend reads cookie and sends via `X-XSRF-TOKEN` header
4. Backend validates via `[ValidateAntiForgeryToken]` attribute

## Accessibility

All components follow WCAG 2.1 Level AA guidelines:

- ✅ Semantic HTML with proper labels
- ✅ ARIA attributes for error states (`aria-invalid`, `aria-describedby`)
- ✅ Keyboard navigation support
- ✅ Focus management (auto-focus on errors)
- ✅ Screen reader announcements for errors
- ✅ Sufficient color contrast
- ✅ Focus visible styles

## Testing

### Manual Testing Checklist

#### Login Form
- [ ] Email validation (invalid format shows error)
- [ ] Password required (empty shows error)
- [ ] Submit with valid credentials succeeds
- [ ] Submit with invalid credentials shows server error
- [ ] Form disabled during submission
- [ ] Network errors handled gracefully
- [ ] Focus moves to first error field on validation failure
- [ ] Remember me checkbox toggles correctly

#### Keyboard Navigation
- [ ] Tab through all form fields
- [ ] Enter submits form
- [ ] Escape closes modals
- [ ] Tab traps within modal when open
- [ ] Focus returns to trigger after modal close

#### Modals
- [ ] Forgot password modal opens via `?modal=forgot`
- [ ] Verify email modal opens via `?modal=verify`
- [ ] Close button works
- [ ] Click backdrop closes modal
- [ ] Escape key closes modal
- [ ] Email validation works in modals

#### ReturnUrl
- [ ] Default redirect to `/game/current` after login
- [ ] Custom `returnUrl` parameter preserved
- [ ] ReturnUrl preserved when opening modals

### Screen Reader Testing
- [ ] Form labels announced correctly
- [ ] Error messages announced on change
- [ ] Loading states announced
- [ ] Modal opens with title announced

## Browser Support

Tested in:
- Chrome 120+
- Firefox 120+
- Safari 17+
- Edge 120+

## Future Enhancements

1. **Backend Endpoints Needed:**
   - `POST /v1/auth/forgot-password` - Password reset
   - `POST /v1/auth/resend-verification` - Email verification
   
2. **Planned Features:**
   - Social login (Google, GitHub)
   - Two-factor authentication
   - Password strength indicator
   - Remember device functionality
   - Biometric authentication support

## Troubleshooting

### Login fails with CSRF error
- Check that CSRF provider is initialized in `AppProviders`
- Verify CSRF cookie is being set by `/v1/auth/csrf`
- Check that `X-XSRF-TOKEN` header is being sent

### Modal doesn't close
- Ensure `onRequestClose` callback is properly wired
- Check browser console for errors
- Verify modal query param is being removed from URL

### Focus not returning after modal close
- Check that `ModalContainer` is properly unmounting
- Verify previous focused element is being stored

### Email validation too strict
- Adjust Zod schema in component files
- Backend validation is authoritative

