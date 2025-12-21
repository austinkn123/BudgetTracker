# Requirements: AWS Cognito Authentication with Gmail Sign-In

**Document Version:** 1.0  
**Created:** December 21, 2025  
**Product:** BudgetTracker  
**Feature:** User Authentication & Authorization

---

## Executive Summary

This document outlines the requirements for implementing AWS Cognito as the authentication and authorization provider for the BudgetTracker application, with Google (Gmail) as a federated identity provider option. This feature will enable secure user authentication, protect user data, and provide a seamless sign-in experience.

---

## Business Objectives

### Primary Goals
1. **Secure User Data**: Ensure that each user's budget information is private and accessible only to them
2. **Reduce Friction**: Provide a quick, familiar sign-in experience through Google/Gmail authentication
3. **Scalability**: Leverage AWS Cognito's infrastructure to handle authentication at scale
4. **Compliance**: Meet industry standards for authentication and data protection

### Success Metrics
- User sign-up completion rate > 80%
- Authentication time < 3 seconds
- Zero security breaches related to authentication
- User satisfaction with sign-in experience > 4.5/5

---

## User Stories

### Epic: User Authentication

#### US-1: Sign Up with Google
**As a** new user  
**I want to** sign up for BudgetTracker using my Google/Gmail account  
**So that** I can quickly create an account without remembering another password

**Acceptance Criteria:**
- [ ] User sees a "Sign in with Google" button on the landing/login page
- [ ] Clicking the button redirects to Google's OAuth consent screen
- [ ] After successful Google authentication, user is redirected back to BudgetTracker
- [ ] A new user account is automatically created with email from Google profile
- [ ] User is logged in and redirected to the Dashboard
- [ ] User profile includes name and email from Google account
- [ ] System stores Cognito User ID in the database for future authentication

**Priority:** P0 (Must Have)  
**Story Points:** 8  
**Dependencies:** None

---

#### US-2: Sign In with Google
**As a** returning user  
**I want to** sign in to BudgetTracker using my Google/Gmail account  
**So that** I can access my budget data quickly

**Acceptance Criteria:**
- [ ] User sees a "Sign in with Google" button on the login page
- [ ] Clicking the button redirects to Google's OAuth consent screen
- [ ] After successful Google authentication, user is logged in
- [ ] User is redirected to their Dashboard with their expense data
- [ ] Session persists across browser tabs
- [ ] User remains logged in after closing and reopening the browser (remember me functionality)

**Priority:** P0 (Must Have)  
**Story Points:** 5  
**Dependencies:** US-1

---

#### US-3: Sign Up with Email/Password (Optional Alternative)
**As a** new user  
**I want to** sign up for BudgetTracker using my email and password  
**So that** I have an alternative if I prefer not to use Google

**Acceptance Criteria:**
- [ ] User sees an email and password input form on the sign-up page
- [ ] Email validation ensures proper format
- [ ] Password requirements are clearly displayed:
  - Minimum 8 characters
  - At least one uppercase letter
  - At least one lowercase letter
  - At least one number
  - At least one special character
- [ ] User receives a verification email
- [ ] User must verify email before accessing the application
- [ ] After verification, user can log in with email/password

**Priority:** P1 (Should Have)  
**Story Points:** 8  
**Dependencies:** US-1

---

#### US-4: Sign In with Email/Password
**As a** returning user  
**I want to** sign in to BudgetTracker using my email and password  
**So that** I can access my account using my credentials

**Acceptance Criteria:**
- [ ] User can enter email and password on login page
- [ ] Invalid credentials show a clear error message
- [ ] Successful login redirects to Dashboard
- [ ] Account lockout after 5 failed login attempts
- [ ] Clear error message if account is locked

**Priority:** P1 (Should Have)  
**Story Points:** 5  
**Dependencies:** US-3

---

#### US-5: Stay Logged In
**As a** user  
**I want to** remain logged in when I return to the application  
**So that** I don't have to sign in every time

**Acceptance Criteria:**
- [ ] User session persists for 30 days by default
- [ ] Refresh tokens are automatically used to maintain session
- [ ] User can manually log out to end session
- [ ] Session expires after 30 days of inactivity
- [ ] User is redirected to login page when session expires

**Priority:** P0 (Must Have)  
**Story Points:** 5  
**Dependencies:** US-1, US-2

---

#### US-6: Log Out
**As a** logged-in user  
**I want to** log out of my account  
**So that** my data is secure when I'm done using the application

**Acceptance Criteria:**
- [ ] User sees a "Log Out" button in the navigation/header
- [ ] Clicking "Log Out" ends the user session
- [ ] User is redirected to the login page
- [ ] All authentication tokens are cleared
- [ ] User cannot access protected pages after logging out

**Priority:** P0 (Must Have)  
**Story Points:** 3  
**Dependencies:** US-1, US-2

---

#### US-7: Forgot Password
**As a** user who signed up with email/password  
**I want to** reset my password if I forget it  
**So that** I can regain access to my account

**Acceptance Criteria:**
- [ ] User sees a "Forgot Password?" link on the login page
- [ ] User enters their email address
- [ ] System sends a password reset email
- [ ] Email contains a secure, time-limited reset link
- [ ] User can set a new password meeting requirements
- [ ] User is redirected to login page after successful reset
- [ ] Old password is invalidated

**Priority:** P1 (Should Have)  
**Story Points:** 5  
**Dependencies:** US-3

---

#### US-8: View My Profile
**As a** logged-in user  
**I want to** view my profile information  
**So that** I can verify my account details

**Acceptance Criteria:**
- [ ] User can access profile page from navigation
- [ ] Profile displays: name, email, account creation date
- [ ] Profile indicates authentication method (Google or Email)
- [ ] Profile is read-only for Google-authenticated users (managed by Google)

**Priority:** P2 (Nice to Have)  
**Story Points:** 3  
**Dependencies:** US-1, US-2

---

#### US-9: Update Profile
**As a** logged-in user with email/password  
**I want to** update my name  
**So that** my profile reflects my current information

**Acceptance Criteria:**
- [ ] User can edit their name on profile page
- [ ] Changes are saved immediately
- [ ] Success message confirms update
- [ ] Email cannot be changed (requires re-verification)

**Priority:** P2 (Nice to Have)  
**Story Points:** 3  
**Dependencies:** US-8

---

#### US-10: Protected API Access
**As a** developer  
**I want** all API endpoints to require authentication  
**So that** user data is secure and private

**Acceptance Criteria:**
- [ ] All `/api/*` endpoints require valid JWT token
- [ ] Unauthenticated requests return 401 Unauthorized
- [ ] Invalid/expired tokens return 401 Unauthorized
- [ ] Token includes user identity (Cognito User ID)
- [ ] Backend validates token with AWS Cognito
- [ ] Users can only access their own data

**Priority:** P0 (Must Have)  
**Story Points:** 8  
**Dependencies:** US-1, US-2

---

## Technical Requirements

### Frontend Requirements

#### Authentication Flow
1. **Login Page**
   - Display "Sign in with Google" button with Google branding
   - Optional: Display email/password form
   - Link to "Forgot Password" flow
   - Link to sign-up page if different from login

2. **Session Management**
   - Store JWT tokens securely (httpOnly cookies preferred, or secure localStorage)
   - Automatically refresh tokens before expiration
   - Handle token expiration gracefully (redirect to login)
   - Clear tokens on logout

3. **Protected Routes**
   - Implement route guards for authenticated pages
   - Redirect unauthenticated users to login page
   - Preserve intended destination for post-login redirect

4. **API Integration**
   - Include JWT token in all API requests (Authorization header)
   - Handle 401 responses (token expired/invalid)
   - Automatic logout on authentication errors

#### User Experience
- Loading states during authentication
- Clear error messages for authentication failures
- Responsive design for all authentication pages
- Accessibility compliance (WCAG 2.1 AA)

---

### Backend Requirements

#### AWS Cognito Configuration
1. **User Pool Setup**
   - Create AWS Cognito User Pool in appropriate region
   - Configure password policy:
     - Minimum 8 characters
     - Uppercase, lowercase, numbers, special characters required
   - Enable email verification
   - Configure email templates for verification and password reset
   - Set token expiration: Access (1 hour), Refresh (30 days)

2. **Google Identity Provider**
   - Register application with Google Cloud Console
   - Obtain OAuth 2.0 credentials (Client ID and Client Secret)
   - Configure authorized redirect URIs
   - Add Google as identity provider in Cognito
   - Map Google attributes to Cognito attributes:
     - `email` → `email`
     - `name` → `name`
     - `sub` → `preferred_username`

3. **App Client Configuration**
   - Create app client for web application
   - Enable OAuth 2.0 flows: Authorization code grant
   - Configure OAuth scopes: `openid`, `email`, `profile`
   - Set callback URLs for frontend application
   - Set logout URLs

#### API Authentication
1. **JWT Validation**
   - Validate JWT signature using Cognito public keys
   - Verify token expiration
   - Extract Cognito User ID (sub claim)
   - Verify token audience (app client ID)

2. **Authorization Middleware**
   - Create ASP.NET Core authentication middleware
   - Add `[Authorize]` attribute to protected endpoints
   - Extract user identity from token claims
   - Inject user context into request pipeline

3. **User Management**
   - Store Cognito User ID in database `User` table
   - Create user record on first successful authentication
   - Update user email if changed in Cognito
   - Support user lookup by Cognito User ID

#### API Endpoints
```
POST /api/auth/callback          - Handle OAuth callback
GET  /api/auth/user              - Get current user profile
POST /api/auth/logout            - Invalidate session
GET  /api/auth/status            - Check authentication status
```

---

### Database Requirements

#### User Table Updates
The existing `User` table already has `CognitoUserId` field. Ensure:
- `CognitoUserId` is indexed for fast lookups
- `CognitoUserId` is unique and required
- `Email` is indexed and unique
- Add `Name` field (nullable, from Google profile)
- Add `AuthProvider` field (enum: 'Google', 'EmailPassword')
- Add `LastLoginAt` field (DateTime, for analytics)

#### Data Isolation
- All queries must filter by `UserId`
- Implement middleware to automatically add user filter
- Prevent users from accessing other users' data

---

### Security Requirements

#### Authentication Security
1. **Token Security**
   - Use HTTPS for all authentication flows
   - Implement CSRF protection
   - Use secure, httpOnly cookies when possible
   - Implement token rotation

2. **Password Security (if email/password enabled)**
   - Use Cognito's password hashing (handled automatically)
   - Implement rate limiting on login attempts
   - Account lockout after 5 failed attempts
   - Require password reset for locked accounts

3. **OAuth Security**
   - Implement state parameter validation (CSRF protection)
   - Validate redirect URIs
   - Use PKCE for additional security
   - Implement nonce for replay attack prevention

4. **API Security**
   - Validate all JWT tokens
   - Implement rate limiting on API endpoints
   - Log all authentication events
   - Monitor for suspicious activity

#### Data Privacy
- Store minimal user data
- Comply with GDPR/privacy regulations
- Provide user data export capability
- Implement account deletion functionality

---

### Infrastructure Requirements

#### AWS Services
1. **Amazon Cognito**
   - User Pool for authentication
   - Identity Pool (optional, for AWS resource access)
   - Domain for hosted UI (optional)

2. **Monitoring & Logging**
   - CloudWatch for Cognito logs
   - Application logs for authentication events
   - Alerts for authentication failures

3. **Cost Considerations**
   - Cognito free tier: 50,000 MAUs
   - Additional users: $0.0055 per MAU
   - Budget for expected user growth

#### Configuration Management
- Store Cognito configuration in environment variables
- Keep secrets out of source control
- Use AWS Secrets Manager or Parameter Store
- Different Cognito pools for dev/staging/production

---

## Non-Functional Requirements

### Performance
- Authentication flow completes in < 3 seconds
- Token validation adds < 100ms to API requests
- Support 1000 concurrent authentication requests

### Availability
- Authentication service 99.9% uptime (leveraging AWS SLA)
- Graceful degradation if Cognito is unavailable
- Cache user data for temporary offline access

### Scalability
- Support up to 100,000 monthly active users
- Horizontal scaling of backend services
- Cognito handles authentication scaling automatically

### Usability
- Mobile-responsive authentication pages
- Accessible to users with disabilities (WCAG 2.1 AA)
- Support for major browsers (Chrome, Firefox, Safari, Edge)
- Clear error messages and user guidance

---

## Implementation Phases

### Phase 1: Foundation (Sprint 1-2)
1. Set up AWS Cognito User Pool
2. Configure Google as identity provider
3. Implement backend JWT validation
4. Update User model and database

**Deliverables:**
- Cognito User Pool configured
- Google OAuth integration working
- Backend authentication middleware
- Database schema updated

---

### Phase 2: Core Authentication (Sprint 3-4)
1. Build login/signup UI components
2. Implement OAuth flow in frontend
3. Add session management
4. Protect API endpoints
5. Add logout functionality

**Deliverables:**
- Working "Sign in with Google" flow
- User can log in and access protected pages
- Session persists across page refreshes
- User can log out

---

### Phase 3: Enhanced Features (Sprint 5)
1. Add email/password authentication (optional)
2. Implement forgot password flow
3. Add user profile page
4. Implement token refresh logic
5. Add loading states and error handling

**Deliverables:**
- Email/password authentication (if included)
- Password reset functionality
- User profile management
- Polished UX with proper error handling

---

### Phase 4: Security & Polish (Sprint 6)
1. Security audit and penetration testing
2. Implement rate limiting
3. Add authentication logging and monitoring
4. Performance optimization
5. Documentation and user guides

**Deliverables:**
- Security audit report
- Monitoring dashboards
- Performance benchmarks
- User documentation

---

## Dependencies

### External Dependencies
- AWS Account with appropriate permissions
- Google Cloud Console project for OAuth credentials
- SSL certificates for production domain

### Internal Dependencies
- Existing User model (already has CognitoUserId field)
- API infrastructure (already in place)
- Frontend routing (react-router-dom already installed)

---

## Risks & Mitigation

### Risk 1: Google OAuth Setup Complexity
**Impact:** High  
**Probability:** Medium  
**Mitigation:** Follow detailed AWS and Google documentation; allocate extra time for setup and testing

### Risk 2: Token Management Issues
**Impact:** High  
**Probability:** Medium  
**Mitigation:** Use well-tested libraries (AWS SDK, JWT libraries); implement comprehensive error handling

### Risk 3: User Data Migration
**Impact:** Low (new system, no existing users)  
**Probability:** Low  
**Mitigation:** N/A for new implementation; document migration strategy for future

### Risk 4: AWS Costs Exceed Budget
**Impact:** Medium  
**Probability:** Low  
**Mitigation:** Monitor usage; set up billing alerts; stay within free tier initially

---

## Testing Strategy

### Unit Tests
- JWT validation logic
- User repository methods
- Token refresh logic
- API middleware

### Integration Tests
- Full OAuth flow (end-to-end)
- API authentication
- User creation and lookup
- Session management

### Security Tests
- Token expiration handling
- Invalid token rejection
- CSRF protection
- XSS prevention
- SQL injection prevention

### User Acceptance Tests
- Sign up with Google
- Sign in with Google
- Logout and re-login
- Session persistence
- Error handling

---

## Success Criteria

This feature will be considered successful when:

1. ✅ Users can sign up using their Google/Gmail account
2. ✅ Users can sign in using their Google/Gmail account
3. ✅ User sessions persist across browser sessions
4. ✅ All API endpoints are protected and require authentication
5. ✅ Users can only access their own data
6. ✅ Users can log out successfully
7. ✅ Authentication flow is secure (passes security audit)
8. ✅ Authentication flow is fast (< 3 seconds)
9. ✅ Zero critical security vulnerabilities
10. ✅ User satisfaction > 4.5/5 in feedback surveys

---

## Open Questions

1. **Multi-Factor Authentication**: Should we support MFA in the initial release?
   - **Recommendation:** Not in MVP, add in Phase 2

2. **Social Sign-In Options**: Should we support additional providers (Facebook, Apple)?
   - **Recommendation:** Start with Google only, add others based on user demand

3. **Remember Me**: Should "remember me" be on by default or user-selectable?
   - **Recommendation:** On by default (30-day session), with option to log out

4. **Email/Password**: Is email/password authentication required, or Google-only?
   - **Recommendation:** Start with Google-only for MVP, add email/password in Phase 2 if needed

5. **User Profile Editing**: How much profile information should users be able to edit?
   - **Recommendation:** Minimal editing in MVP (name only), expand based on user feedback

---

## Appendix

### Reference Documentation
- [AWS Cognito Documentation](https://docs.aws.amazon.com/cognito/)
- [Google OAuth 2.0 Documentation](https://developers.google.com/identity/protocols/oauth2)
- [JWT Best Practices](https://tools.ietf.org/html/rfc8725)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)

### Architecture Diagrams
*(To be created during implementation)*
- Authentication flow diagram
- System architecture diagram
- Data flow diagram

---

**Document Approval:**
- Product Manager: _________________
- Technical Lead: _________________
- Security Lead: _________________
- Date: _________________
