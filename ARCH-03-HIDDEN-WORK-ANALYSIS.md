# Hidden Work & Dependencies Analysis

**Document:** ARCH-03  
**Version:** 1.0  
**Status:** Design Phase  
**Last Updated:** December 21, 2025  
**Author:** Software Architecture Team

---

## Table of Contents
1. [Executive Summary](#executive-summary)
2. [Infrastructure Hidden Work](#infrastructure-hidden-work)
3. [Database Migration Work](#database-migration-work)
4. [Data Flow Complexity](#data-flow-complexity)
5. [Non-Happy Path Scenarios](#non-happy-path-scenarios)
6. [Testing Requirements](#testing-requirements)
7. [Documentation Work](#documentation-work)
8. [Technical Debt](#technical-debt)
9. [Effort Estimation](#effort-estimation)

---

## Executive Summary

This document identifies work that is not immediately obvious from the product requirements but is essential for a successful implementation. This includes infrastructure setup, edge cases, error scenarios, testing, and technical debt.

### Key Findings

#### Hidden Work Categories
1. **Infrastructure Setup**: ~40 hours
2. **Database Schema & Migrations**: ~16 hours
3. **Error Handling & Edge Cases**: ~24 hours
4. **Testing Infrastructure**: ~32 hours
5. **Documentation**: ~16 hours
6. **Technical Debt**: ~12 hours

**Total Hidden Work**: ~140 hours (44% of total 320-hour estimate)

### Critical Insights

| Insight | Impact | Mitigation |
|---------|--------|------------|
| AWS account setup requires approval | 🔴 High | Start approval process immediately |
| Google OAuth requires domain verification | 🟡 Medium | Use Google Cloud organization account |
| Database migration in production needs downtime | 🟡 Medium | Use online migration strategy |
| Testing requires test Cognito users | 🟡 Medium | Create test user pool |
| Token expiration edge cases complex | 🟡 Medium | Comprehensive error handling |

---

## Infrastructure Hidden Work

### AWS Cognito Setup

#### 1. AWS Account & Permissions
**Effort**: 4 hours  
**Dependencies**: AWS administrator, procurement (if new account)

**Tasks:**
- [ ] Request AWS account (if not exists)
- [ ] Set up IAM roles and policies
- [ ] Configure billing alerts
- [ ] Set up MFA for admin users
- [ ] Document access procedures

**Complexity**: Low, but requires approvals

#### 2. Cognito User Pool Configuration
**Effort**: 8 hours  
**Dependencies**: AWS account

**Tasks:**
- [ ] Create User Pool (dev, staging, prod)
- [ ] Configure password policies
- [ ] Set up email templates (verification, password reset)
- [ ] Configure MFA settings
- [ ] Set up User Pool domain
- [ ] Configure app clients for each environment
- [ ] Test User Pool functionality

**Hidden Gotchas:**
- Email sending limits in Cognito (200/day in sandbox mode)
- Need to verify production email sender (SES verification)
- User Pool domain must be globally unique
- Cannot change User Pool ID after creation

#### 3. Google OAuth Provider Setup
**Effort**: 6 hours  
**Dependencies**: Google Cloud Console access

**Tasks:**
- [ ] Create Google Cloud project
- [ ] Enable Google+ API
- [ ] Configure OAuth consent screen
- [ ] Set up OAuth 2.0 credentials
- [ ] Configure authorized redirect URIs (all environments)
- [ ] Verify domain ownership (for production)
- [ ] Test OAuth flow in each environment

**Hidden Gotchas:**
- Google consent screen requires privacy policy URL
- Domain verification takes 24-48 hours
- OAuth consent screen review process (for production)
- Need separate credentials for dev/staging/prod

#### 4. Environment Configuration
**Effort**: 4 hours  
**Dependencies**: DevOps team, CI/CD pipeline

**Tasks:**
- [ ] Set up environment variables in dev
- [ ] Set up environment variables in staging
- [ ] Set up environment variables in production
- [ ] Configure secrets management (Azure Key Vault / AWS Secrets Manager)
- [ ] Update CI/CD pipelines
- [ ] Document configuration process

#### 5. SSL/TLS Certificates
**Effort**: 2 hours  
**Dependencies**: Domain name, certificate authority

**Tasks:**
- [ ] Ensure SSL certificates for all environments
- [ ] Configure HTTPS redirect
- [ ] Test certificate validation
- [ ] Set up certificate renewal automation

#### 6. Monitoring & Logging Infrastructure
**Effort**: 8 hours  
**Dependencies**: Azure/AWS account

**Tasks:**
- [ ] Set up Application Insights / CloudWatch
- [ ] Configure log aggregation
- [ ] Create monitoring dashboards
- [ ] Set up alerts for auth failures
- [ ] Configure error tracking (Sentry / AppInsights)
- [ ] Test monitoring pipeline

#### 7. CDN & Caching Setup (Production)
**Effort**: 8 hours  
**Dependencies**: Cloud provider

**Tasks:**
- [ ] Configure CDN for frontend assets
- [ ] Set up Redis cache (if using distributed cache)
- [ ] Configure cache invalidation strategy
- [ ] Test cache behavior
- [ ] Document cache configuration

**Total Infrastructure Work**: ~40 hours

---

## Database Migration Work

### Current Schema Analysis

**User Table (Existing):**
```sql
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CognitoUserId NVARCHAR(255) NULL, -- Already exists! ✅
    Email NVARCHAR(255) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

**Good News**: `CognitoUserId` field already exists! This was forward-thinking.

### Required Schema Changes

#### 1. Make CognitoUserId Required
**Effort**: 2 hours  
**Risk**: Medium (if existing users don't have CognitoUserId)

```sql
-- Migration script
ALTER TABLE Users ALTER COLUMN CognitoUserId NVARCHAR(255) NOT NULL;
CREATE UNIQUE INDEX IX_Users_CognitoUserId ON Users(CognitoUserId);
CREATE INDEX IX_Users_Email ON Users(Email);
```

**Hidden Gotchas:**
- What if existing Users have NULL CognitoUserId?
- Need backfill strategy for existing users
- Cannot make NOT NULL if NULL values exist

**Migration Strategy:**
1. Identify users with NULL CognitoUserId
2. Option A: Delete users (if test data)
3. Option B: Backfill with placeholder (migrate later)
4. Option C: Keep nullable, add NOT NULL check in code

#### 2. Add Audit Fields (Optional but Recommended)
**Effort**: 2 hours

```sql
ALTER TABLE Users ADD LastLoginAt DATETIME2 NULL;
ALTER TABLE Users ADD AuthProvider NVARCHAR(50) NOT NULL DEFAULT 'Cognito';
ALTER TABLE Users ADD IsActive BIT NOT NULL DEFAULT 1;
ALTER TABLE Users ADD UpdatedAt DATETIME2 NULL;
```

#### 3. Database Indexes for Performance
**Effort**: 2 hours

```sql
-- Performance optimization
CREATE INDEX IX_Expenses_UserId ON Expenses(UserId);
CREATE INDEX IX_Expenses_Date ON Expenses(Date DESC);
CREATE INDEX IX_Expenses_UserId_Date ON Expenses(UserId, Date DESC);
CREATE INDEX IX_Categories_UserId ON Categories(UserId);
```

#### 4. Migration Scripts & Rollback
**Effort**: 4 hours

**Tasks:**
- [ ] Write forward migration scripts
- [ ] Write rollback scripts
- [ ] Test migrations on copy of production data
- [ ] Document migration procedure
- [ ] Test rollback procedure

#### 5. Data Backup & Recovery
**Effort**: 4 hours

**Tasks:**
- [ ] Create backup before migration
- [ ] Test backup restoration
- [ ] Document recovery procedure
- [ ] Set up automated backups

#### 6. Production Migration Execution
**Effort**: 2 hours  
**Risk**: High

**Tasks:**
- [ ] Schedule maintenance window
- [ ] Communicate downtime to users
- [ ] Execute migration
- [ ] Verify migration success
- [ ] Monitor for issues

**Total Database Work**: ~16 hours

---

## Data Flow Complexity

### Authentication Data Flow

#### 1. First-Time User Registration
**Hidden Complexity**: Auto-provisioning user records

**Flow:**
1. User authenticates with Google via Cognito
2. Cognito returns JWT with `sub` claim (CognitoUserId)
3. Backend receives request with JWT
4. Backend looks up user by CognitoUserId
5. **User not found** → Create new user record
6. Return user data to frontend

**Edge Cases:**
- What if database is down during user creation?
- What if user creation fails midway?
- Concurrent requests for same new user?
- Email already exists but different CognitoUserId?

**Hidden Work**: 4 hours for robust error handling

#### 2. Token Refresh Flow
**Hidden Complexity**: Seamless token refresh without user awareness

**Scenarios:**
- Access token expires mid-request
- Refresh token expires (30 days of inactivity)
- Network failure during refresh
- Cognito unavailable during refresh

**Hidden Work**: 4 hours for comprehensive handling

#### 3. User Data Synchronization
**Hidden Complexity**: Keeping Cognito and local database in sync

**Scenarios:**
- User changes email in Google
- User name changes in Google profile
- User deletes Google account
- User account suspended in Cognito

**Hidden Work**: 4 hours for sync logic

#### 4. Multi-Tab/Multi-Device Scenarios
**Hidden Complexity**: Session management across multiple contexts

**Scenarios:**
- User logs out in one tab, other tabs still have tokens
- User logs in on mobile and desktop simultaneously
- Token refresh in one tab, other tabs have stale tokens

**Hidden Work**: 4 hours for synchronization

**Total Data Flow Work**: ~16 hours

---

## Non-Happy Path Scenarios

### Authentication Failures

#### 1. Google OAuth Failures
**Effort**: 2 hours

**Scenarios:**
- User denies Google consent
- Google account suspended
- Google OAuth temporarily down
- Invalid OAuth state parameter
- CSRF token mismatch

**Required Handling:**
```typescript
try {
  await signInWithGoogle();
} catch (error) {
  if (error.code === 'UserCancelledException') {
    // User cancelled login
    showMessage('Login cancelled');
  } else if (error.code === 'NotAuthorizedException') {
    // Account suspended or OAuth denied
    showError('Unable to authenticate with Google');
  } else if (error.code === 'NetworkError') {
    // Network issues
    showError('Network error. Please try again.');
  } else {
    // Unknown error
    logError(error);
    showError('Authentication failed. Please try again.');
  }
}
```

#### 2. Token Validation Failures
**Effort**: 2 hours

**Scenarios:**
- Token expired
- Token signature invalid
- Token issuer doesn't match
- Token audience doesn't match
- Malformed token
- Token from wrong User Pool

#### 3. User Repository Failures
**Effort**: 2 hours

**Scenarios:**
- Database connection failure
- User creation fails (duplicate email)
- Transaction rollback
- Timeout during user lookup

#### 4. Network Failures
**Effort**: 2 hours

**Scenarios:**
- Cognito API unavailable
- Database unreachable
- Slow network (timeouts)
- Partial responses

#### 5. Rate Limiting
**Effort**: 2 hours

**Scenarios:**
- Too many login attempts
- API rate limit exceeded
- Cognito throttling

#### 6. Session Expiration
**Effort**: 2 hours

**Scenarios:**
- User idle for 30 days (refresh token expired)
- User in middle of operation when token expires
- Multiple concurrent requests with expired token

#### 7. Cognito Downtime
**Effort**: 4 hours

**Scenarios:**
- Cognito service outage
- Degraded performance
- Fallback authentication strategy?

#### 8. Browser/Client Issues
**Effort**: 2 hours

**Scenarios:**
- LocalStorage disabled
- Cookies blocked
- Third-party cookies blocked
- JavaScript disabled
- Old browser version

**Total Non-Happy Path Work**: ~18 hours

---

## Testing Requirements

### Testing Infrastructure Setup

#### 1. Test Cognito User Pool
**Effort**: 4 hours

**Tasks:**
- [ ] Create separate User Pool for testing
- [ ] Configure test Google OAuth credentials
- [ ] Create test users with various states
- [ ] Document test account management

#### 2. Test Data Setup
**Effort**: 4 hours

**Tasks:**
- [ ] Create test users in database
- [ ] Create test categories and expenses
- [ ] Set up test data generation scripts
- [ ] Document test data scenarios

#### 3. Unit Tests
**Effort**: 12 hours

**Coverage Areas:**
- JWT validation logic
- User repository methods
- Token refresh logic
- Error handling
- Claims extraction
- Authorization logic

**Example Tests:**
```csharp
[Fact]
public async Task GetOrCreateUser_NewUser_CreatesUser()
{
    // Arrange
    var cognitoUserId = "test-cognito-id";
    var email = "test@example.com";
    var mockRepo = new Mock<IUserRepository>();
    mockRepo.Setup(r => r.GetByCognitoUserIdAsync(cognitoUserId))
        .ReturnsAsync((User?)null);
    
    // Act
    var result = await AuthEndpoints.GetOrCreateUser(context, mockRepo.Object, logger);
    
    // Assert
    mockRepo.Verify(r => r.CreateAsync(It.IsAny<User>()), Times.Once);
}
```

#### 4. Integration Tests
**Effort**: 8 hours

**Coverage Areas:**
- Full OAuth flow (end-to-end)
- API authentication
- User creation and lookup
- Token validation
- Protected endpoint access

#### 5. Security Tests
**Effort**: 4 hours

**Coverage Areas:**
- Token expiration handling
- Invalid token rejection
- CSRF protection
- XSS prevention
- SQL injection prevention
- Authorization bypass attempts

**Total Testing Work**: ~32 hours

---

## Documentation Work

### Required Documentation

#### 1. Developer Documentation
**Effort**: 4 hours

**Content:**
- Setup instructions (local dev)
- Environment configuration
- Testing procedures
- Debugging authentication issues

#### 2. Architecture Documentation
**Effort**: 4 hours

**Content:**
- Authentication flow diagrams (✅ In progress)
- System architecture diagrams (✅ In progress)
- Integration patterns
- Security model

#### 3. API Documentation
**Effort**: 2 hours

**Content:**
- Updated OpenAPI/Swagger docs
- Authentication requirements
- Error response formats
- Example requests/responses

#### 4. Deployment Documentation
**Effort**: 2 hours

**Content:**
- Infrastructure setup guide
- Configuration checklist
- Deployment procedures
- Rollback procedures

#### 5. User Documentation
**Effort**: 2 hours

**Content:**
- How to sign in
- Account management
- Troubleshooting
- Privacy and security information

#### 6. Operations Documentation
**Effort**: 2 hours

**Content:**
- Monitoring and alerts setup
- Incident response procedures
- Common issues and resolutions
- Maintenance procedures

**Total Documentation Work**: ~16 hours

---

## Technical Debt

### Current Technical Debt

#### 1. No Error Handling
**Effort to Fix**: 4 hours  
**Impact**: High

Current code has no error handling. Need to add:
- Global exception handler
- Specific error handlers
- Consistent error responses

#### 2. No Input Validation
**Effort to Fix**: 4 hours  
**Impact**: High

Current endpoints don't validate input. Need to add:
- FluentValidation
- Validation middleware
- Consistent validation errors

#### 3. No Logging
**Effort to Fix**: 2 hours  
**Impact**: Medium

Current code has minimal logging. Need to add:
- Structured logging (Serilog)
- Log correlation IDs
- Security event logging

#### 4. Direct Domain Entity Usage in API
**Effort to Fix**: 4 hours (for auth endpoints only)  
**Impact**: Medium

APIs return domain entities directly. Should use DTOs:
- Create request/response DTOs
- Map between entities and DTOs
- Control what data is exposed

**Decision**: Keep for MVP, but document as technical debt

**Total Technical Debt**: ~14 hours (or defer to Phase 2)

---

## Effort Estimation

### Detailed Breakdown

| Category | Hours | % of Total |
|----------|-------|------------|
| **Feature Development** | 180 | 56% |
| - Frontend (React + Amplify) | 80 | |
| - Backend (JWT + Endpoints) | 60 | |
| - Integration | 40 | |
| **Hidden Work** | 140 | 44% |
| - Infrastructure | 40 | |
| - Database | 16 | |
| - Data Flow Edge Cases | 16 | |
| - Non-Happy Paths | 18 | |
| - Testing | 32 | |
| - Documentation | 16 | |
| - Technical Debt | 2 | |
| **Total** | 320 | 100% |

### Risk Buffer

**Recommended Buffer**: 20% (64 hours)

**Reasons:**
- First time implementing OAuth in this stack
- Unknown unknowns in AWS Cognito
- Potential issues in production
- Additional testing needs

**Adjusted Total**: 384 hours (~10 weeks for 2 developers)

### Confidence Levels

| Estimate | Confidence | Risk |
|----------|-----------|------|
| Frontend Development | 80% | Low - well-known tech |
| Backend Development | 75% | Medium - new auth middleware |
| Infrastructure Setup | 60% | Medium-High - approvals needed |
| Testing | 70% | Medium - test environment setup |
| Documentation | 90% | Low - straightforward |

---

## Mitigation Strategies

### Infrastructure Risks

**Risk**: AWS account approval delays  
**Mitigation**: Start approval process immediately, use personal/test accounts for dev

**Risk**: Google domain verification delays  
**Mitigation**: Start verification early, use localhost for development

### Technical Risks

**Risk**: Token management complexity  
**Mitigation**: Use well-tested Amplify library, comprehensive error handling

**Risk**: Database migration issues  
**Mitigation**: Test on copy of production data, have rollback plan

### Process Risks

**Risk**: Underestimating hidden work  
**Mitigation**: This document! Also 20% buffer

**Risk**: Scope creep  
**Mitigation**: Strict MVP definition, Phase 2 for enhancements

---

## Recommendations

### Critical Actions (Week 1)

1. ✅ **Start AWS account approval** - longest lead time
2. ✅ **Begin Google Cloud setup** - domain verification takes time
3. ✅ **Review database schema** - identify migration needs
4. ✅ **Set up test environments** - needed for development
5. ✅ **Document current system** - understand what we're changing

### Phase 1 Focus (MVP)

**Include:**
- ✅ Basic authentication (Google OAuth)
- ✅ Protected API endpoints
- ✅ User auto-provisioning
- ✅ Essential error handling
- ✅ Basic logging

**Defer to Phase 2:**
- ⏸️ Comprehensive error handling
- ⏸️ Advanced monitoring
- ⏸️ DTOs (use entities for now)
- ⏸️ Email/password auth
- ⏸️ Profile management

### Success Criteria

This hidden work analysis succeeds if:
- ✅ No surprises during implementation
- ✅ Infrastructure ready before development starts
- ✅ Testing environment available from day 1
- ✅ Non-happy paths identified and handled
- ✅ Actual effort within 20% of estimate

---

## Related Documents

- [ARCH-01: AWS Cognito Architecture](./ARCH-01-AWS-COGNITO-ARCHITECTURE.md)
- [ARCH-02: Cross-Cutting Concerns](./ARCH-02-CROSS-CUTTING-CONCERNS.md)
- [ARCH-04: Scope & Phasing](./ARCH-04-SCOPE-PHASING.md)

---

**Document Status:** 🟢 Complete  
**Approved By:** Pending Review  
**Next Review Date:** January 2026
