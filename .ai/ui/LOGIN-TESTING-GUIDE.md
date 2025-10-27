# Login View Testing Guide

## Pre-requisites

1. **Database:** Ensure the database is running with migrations applied
2. **Backend:** Start the ASP.NET server (`TenXEmpires.Server`)
3. **Frontend:** Start the Vite dev server (`cd tenxempires.client && npm run dev`)

## Creating Test Users

### Method 1: Registration Endpoint
```bash
curl -X POST http://localhost:5000/v1/auth/register \
  -H "Content-Type: application/json" \
  -H "X-XSRF-TOKEN: [get from browser]" \
  -d '{"email":"test@example.com","password":"Test1234!"}'
```

### Method 2: Via UI
1. Navigate to `/register`
2. Enter email and password (min 8 chars)
3. Submit form
4. You'll be automatically signed in

### Method 3: Direct Database Insert (Development Only)
Use the Identity tables to create a test user with hashed password.

## Test Scenarios

### 1. Basic Login Flow

**Test:** Successful login
1. Navigate to `/login`
2. Enter valid credentials:
   - Email: `test@example.com`
   - Password: `Test1234!`
3. Click "Sign in"
4. **Expected:** Redirect to `/game/current` (or returnUrl if specified)
5. **Expected:** Auth cookie set, can access protected routes

**Test:** Invalid credentials
1. Navigate to `/login`
2. Enter invalid email or wrong password
3. Click "Sign in"
4. **Expected:** Error message "Invalid email or password."
5. **Expected:** Form remains enabled, can retry

**Test:** Network error
1. Stop the backend server
2. Attempt to login
3. **Expected:** Error message "Network error. Please check your connection and try again."

### 2. Form Validation

**Test:** Email validation
1. Enter invalid email format (e.g., "notanemail")
2. Click "Sign in"
3. **Expected:** Inline error "Please enter a valid email address"
4. **Expected:** Focus moves to email field

**Test:** Required password
1. Enter valid email, leave password empty
2. Click "Sign in"
3. **Expected:** Inline error "Password is required"
4. **Expected:** Focus moves to password field

**Test:** Remember me checkbox
1. Toggle "Remember me" checkbox
2. **Expected:** State persists between toggles
3. Login with "Remember me" checked
4. **Expected:** Session persists longer (configured in backend)

### 3. Modal Flows

**Test:** Forgot Password modal
1. Click "Forgot password?" link
2. **Expected:** URL changes to `/login?modal=forgot`
3. **Expected:** Modal opens with "Forgot Password" title
4. Enter email address
5. Click "Send Reset Link"
6. **Expected:** Error message (endpoint not yet implemented)
7. Click "Cancel" or backdrop or Escape
8. **Expected:** Modal closes, URL returns to `/login`

**Test:** Verify Email modal
1. Navigate to `/login?modal=verify`
2. **Expected:** Modal opens with "Verify Email Address" title
3. Enter email address
4. Click "Send Verification Email"
5. **Expected:** Error message (endpoint not yet implemented)
6. Close modal
7. **Expected:** Returns to login page

### 4. ReturnUrl Handling

**Test:** Default redirect
1. Navigate to `/login` (no returnUrl)
2. Login successfully
3. **Expected:** Redirect to `/game/current`

**Test:** Custom returnUrl
1. Navigate to `/login?returnUrl=/about`
2. Login successfully
3. **Expected:** Redirect to `/about`

**Test:** ReturnUrl preserved in modals
1. Navigate to `/login?returnUrl=/gallery`
2. Click "Forgot password?"
3. **Expected:** URL is `/login?modal=forgot&returnUrl=/gallery`
4. Close modal
5. Login successfully
6. **Expected:** Redirect to `/gallery`

### 5. Accessibility Testing

**Test:** Keyboard navigation
1. Navigate to `/login`
2. Press Tab repeatedly
3. **Expected:** Focus moves through: Email → Password → Remember Me → Sign in → Forgot password → Register
4. Press Enter on "Sign in" button
5. **Expected:** Form submits

**Test:** Modal keyboard navigation
1. Open forgot password modal
2. Press Tab
3. **Expected:** Focus trapped within modal
4. Press Escape
5. **Expected:** Modal closes, focus returns to trigger

**Test:** Screen reader (if available)
1. Use NVDA/JAWS/VoiceOver
2. Navigate through form
3. **Expected:** Labels announced correctly
4. Trigger validation error
5. **Expected:** Error message announced

**Test:** Focus management
1. Submit form with validation errors
2. **Expected:** Focus moves to first error field automatically
3. **Expected:** Error message in aria-describedby is announced

### 6. Loading States

**Test:** Form disabled during submission
1. Enter credentials
2. Click "Sign in"
3. **During request:**
   - Button text changes to "Signing in…"
   - All inputs disabled and grayed out
   - Form cannot be resubmitted
4. **After response:**
   - Form re-enabled if error
   - Redirected if success

### 7. CSRF Protection

**Test:** CSRF token present
1. Open DevTools → Application → Cookies
2. **Expected:** Cookie named `XSRF-TOKEN` present
3. Submit login form
4. Check Network tab → Request Headers
5. **Expected:** `X-XSRF-TOKEN` header present with cookie value

**Test:** CSRF refresh on app init
1. Clear all cookies
2. Refresh page
3. **Expected:** CSRF cookie re-issued automatically via CsrfProvider

### 8. UI/UX Details

**Test:** Auto-focus
1. Navigate to `/login`
2. **Expected:** Email field focused automatically

**Test:** Error display
1. Trigger any error (validation or server)
2. **Expected:** Error shown in red box with clear message
3. **Expected:** Error clears on next submit attempt

**Test:** Link styling
1. Check "Forgot password?" and "Create account" links
2. **Expected:** Indigo color, underline on hover
3. **Expected:** Keyboard focus visible

## Browser Compatibility

Test in the following browsers:
- [ ] Chrome (latest)
- [ ] Firefox (latest)
- [ ] Safari (latest)
- [ ] Edge (latest)

## Mobile Testing

Test on:
- [ ] iOS Safari
- [ ] Android Chrome
- [ ] Small screen (320px width)

**Check:**
- Form fits viewport
- Inputs are tappable (44px min height)
- Modal doesn't overflow screen
- Keyboard doesn't obscure inputs

## Performance

**Test:** Initial load
1. Open DevTools → Network
2. Navigate to `/login`
3. **Expected:** Page loads in < 1s
4. **Expected:** No unnecessary requests

**Test:** Form submission
1. Submit login form
2. Check Network tab
3. **Expected:** Single POST to `/v1/auth/login`
4. **Expected:** Response time < 500ms (local)

## Known Issues / Limitations

1. **Forgot Password:** Backend endpoint not implemented, shows error message
2. **Email Verification:** Backend endpoint not implemented, shows error message
3. **Account Lockout:** Not yet implemented in backend
4. **Rate Limiting:** Configured in backend but not visually indicated in UI
5. **Session Expired:** No auto-refresh or warning yet

## Debugging Tips

### Login fails silently
- Check browser console for errors
- Verify backend is running and reachable
- Check CSRF token in cookies and headers
- Verify user exists in database

### Modal won't open
- Check URL has correct query param
- Check browser console for errors
- Verify modal component is rendered

### Styles look wrong
- Run `npm run build` to rebuild
- Clear browser cache
- Check Tailwind is processing correctly

### Focus issues
- Check if JavaScript is enabled
- Verify refs are attached correctly
- Check for conflicting focus management

## Reporting Issues

When reporting bugs, include:
1. Browser and version
2. Steps to reproduce
3. Expected vs actual behavior
4. Screenshots if relevant
5. Console errors
6. Network requests (from DevTools)

## Next Steps After Testing

Once manual testing is complete:
1. ✅ Mark tested scenarios in this document
2. 📝 Document any bugs found
3. 🔧 Fix critical issues
4. 🎯 Consider automated tests (Playwright/Cypress)
5. 📊 Add analytics events for login success/failure
6. 🔐 Implement remaining auth features (2FA, social login)

