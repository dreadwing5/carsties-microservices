# OAuth 2.0 and OpenID Connect

This document explains how OAuth 2.0 and OpenID Connect (OIDC) work, how tokens and application sessions differ, and how a browser client should handle expiration, inactivity, multiple tabs, MSAL, and XSS risk.

The examples use Carsties as the client application and its Auction API as the protected resource. The protocol concepts apply whether the identity provider is IdentityServer, Microsoft Entra ID, Auth0, Keycloak, or another standards-compliant provider. Carsties currently describes IdentityServer as its centralized identity provider; the MSAL sections below are implementation guidance for a Microsoft Entra-based client, not a statement that MSAL is currently wired into this repository.

## Authentication and Authorization

These terms answer different questions:

- **Authentication:** Who is the user?
- **Authorization:** What is this caller allowed to do?

OAuth 2.0 is an authorization framework. It lets a client obtain limited access to a protected API. OAuth alone is not a login protocol.

OIDC is an identity layer built on OAuth 2.0. It adds a standard way to authenticate users and communicate the result to the client. An OAuth authorization request becomes an OIDC request when it includes the `openid` scope.

## Participants

| Protocol term | Meaning | Carsties example |
|---|---|---|
| Resource owner | User who owns or controls the data | The signed-in auction user |
| Client | Application requesting access | Carsties web application |
| Authorization server / identity provider | Authenticates users and issues tokens | IdentityServer or Microsoft Entra ID |
| Resource server | API protecting data or operations | Auction API |
| User agent | Software navigating the login redirects | The user's browser |

The authorization server and resource server have separate responsibilities even when one organization operates both.

## Credentials and Tokens

### Authorization code

An authorization code is a short-lived, one-time value returned to the client's registered callback URL after authentication. The client exchanges it for tokens through the token endpoint. It is not sent to an API.

### Access token

An access token authorizes a call to a particular API:

```http
GET /api/auctions
Authorization: Bearer <access-token>
```

The API validates the token's issuer, audience, expiry, signature when applicable, and required permissions. An access token may be a JWT, but OAuth does not require that format.

### ID token

OIDC adds the ID token. It describes an authentication event and is intended for the client:

```json
{
  "iss": "https://identity.carsties.example",
  "sub": "user-123",
  "aud": "carsties-web",
  "exp": 1781960000,
  "nonce": "random-login-value"
}
```

The client validates the signature and claims before creating a local application session. The ID token is not an access token and should not be used to call an API.

### Refresh token

A refresh token allows the client to request a new access token without asking the user to authenticate again. It is sent only to the authorization server's token endpoint, never to a resource API.

With refresh-token rotation, every successful refresh issues a new refresh token and invalidates the old one:

```text
Before refresh: access A1, refresh R1
After refresh:  access A2, refresh R2

Store A2 and R2 atomically; do not use A1 or R1 again.
```

Tokens are immutable strings. A previously issued access token never changes itself. The client must replace its stored value and use the new access token on subsequent requests.

### Session cookie

An application session cookie is not an OAuth token. It represents the browser's session with the application:

```text
Browser --session cookie--> Carsties backend --access token--> Auction API
```

In a backend-for-frontend (BFF) design, the browser receives an opaque `HttpOnly` cookie while OAuth tokens remain on the server.

## Important Claims

| Claim | Meaning | Validation rule |
|---|---|---|
| `iss` | Issuer that created the token | Must be the configured trusted issuer |
| `sub` | Stable identifier for the subject | Use `iss` plus `sub` as the external identity key |
| `aud` | Intended recipient | Must identify this client or API, as appropriate |
| `exp` | Expiration time | Must still be in the future, allowing limited clock skew |
| `iat` | Time the token was issued | Must be reasonable |
| `scope` / `scp` | Delegated permissions | Must contain the permission required by the endpoint |
| `nonce` | Value tying an ID token to a login attempt | Must match the value stored before the OIDC redirect |

Email addresses can change and should not normally be used as the durable external user key.

## Authorization Code Flow with PKCE

Authorization Code with Proof Key for Code Exchange (PKCE) is the normal interactive flow for browser, mobile, desktop, and server applications.

```text
User          Carsties client       Authorization server       Auction API
 |                  |                        |                       |
 | Open application |                        |                       |
 |----------------->|                        |                       |
 |                  | Create state, nonce, PKCE values              |
 |                  |                        |                       |
 |                  | Redirect /authorize   |                       |
 |<-----------------|----------------------->|                       |
 |                  |   User signs in and grants consent            |
 |                  |                        |                       |
 |<---------------- Redirect with code ------|                       |
 |                  |                        |                       |
 |                  | Exchange code + PKCE verifier                 |
 |                  |----------------------->|                       |
 |                  | Access token + ID token (+ refresh token)     |
 |                  |<-----------------------|                       |
 |                  |                                                |
 |                  | Call API with access token                     |
 |                  |----------------------------------------------->|
 |                  |<-----------------------------------------------|
```

### 1. Create request correlation values

Before redirecting the browser, the client creates and temporarily stores:

- `state`, which correlates the authorization response with the request and helps defend the redirect flow against CSRF and response mix-up.
- `nonce`, which ties a returned ID token to the OIDC login attempt.
- `code_verifier`, a high-entropy PKCE secret.
- `code_challenge`, normally `BASE64URL(SHA256(code_verifier))`.

### 2. Send the authorization request

```http
GET https://identity.carsties.example/authorize?
    response_type=code&
    client_id=carsties-web&
    redirect_uri=https%3A%2F%2Fapp.carsties.example%2Fcallback&
    scope=openid%20profile%20auction.read&
    state=<random-state>&
    nonce=<random-nonce>&
    code_challenge=<hashed-verifier>&
    code_challenge_method=S256
```

The authorization server authenticates the user, applies MFA or Conditional Access when configured, and obtains consent when required. The client never receives the user's password.

### 3. Validate the callback

The authorization server redirects the browser to the exact registered callback URI:

```http
https://app.carsties.example/callback?code=<one-time-code>&state=<returned-state>
```

The client first verifies `state`, then exchanges the code promptly. Authorization codes should be short-lived and usable only once.

### 4. Exchange the code

```http
POST https://identity.carsties.example/token
Content-Type: application/x-www-form-urlencoded

grant_type=authorization_code&
code=<one-time-code>&
client_id=carsties-web&
redirect_uri=https%3A%2F%2Fapp.carsties.example%2Fcallback&
code_verifier=<original-verifier>
```

The authorization server hashes the verifier and compares it with the original challenge. A stolen authorization code is not sufficient without the verifier.

### 5. Validate and use the result

The client validates the ID token and establishes its application session. It sends the access token—not the ID token—to the API. The API separately validates the access token and enforces endpoint and business authorization.

## Token Refresh and API Calls

The client must obtain the current access token before a protected API call. It must not capture a token once at startup and continue sending that old string forever.

```text
Request needs a token
        |
        v
Cached access token valid? --yes--> Call API
        |
        no
        v
Refresh token valid? ------yes--> Obtain and store new tokens --> Call API
        |
        no
        v
Provider login session can satisfy silent authorization? --yes--> New tokens
        |
        no
        v
Interactive authentication required
```

A client may renew shortly before expiration and should also handle a `401 Unauthorized` fallback. Retry an API request at most once after obtaining a fresh token. A `403 Forbidden` normally means the authenticated caller lacks permission; refreshing generally does not fix it.

When several requests discover expiration simultaneously, use a single-flight refresh lock. All requests should await one refresh operation rather than each trying to rotate the same refresh token.

If the refresh token itself is expired, revoked, or rejected, it cannot refresh itself. The client must start a new authorization flow. That flow may still complete without a credential prompt when the identity provider's own SSO session remains valid.

## Four Independent Lifetimes

Do not treat all expiration as one clock:

| Lifetime | What happens at expiry |
|---|---|
| Access-token lifetime | Obtain a new access token, usually silently |
| Refresh-token lifetime | Start a new authorization flow |
| Idle-session timeout | End the application session after inactivity |
| Absolute-session timeout | Require reauthentication after the maximum session duration |

An illustrative security-sensitive configuration might use a 10-15 minute access token, a 15-30 minute idle timeout, and an 8-12 hour absolute session. These values are examples, not protocol or industry mandates; the correct settings depend on the data, threat model, regulatory requirements, and desired user experience.

There is an important product decision:

- If an idle timeout is intended to force the user to prove their presence again, do not silently log them straight back in.
- If inactivity should merely allow tokens to expire, silently renew when the user returns and prompt only when the identity provider requires interaction.

Silently signing a user back in immediately after an intentional security logout defeats most of the value of that idle timeout.

## Browser Architecture Choices

### Browser-only SPA

```text
Browser SPA --bearer access token--> API
```

The SPA uses Authorization Code with PKCE and keeps tokens in a browser-accessible cache. This is simpler to deploy but any JavaScript running under the application's origin may be able to access or use those credentials.

### Backend for frontend

```text
Browser --HttpOnly session cookie--> BFF --bearer access token--> APIs
```

The BFF performs the OAuth code exchange, stores tokens server-side, refreshes them, and calls downstream APIs. The browser holds only an opaque session identifier, for example:

```http
Set-Cookie: carsties_session=<opaque-id>; HttpOnly; Secure; SameSite=Lax; Path=/
```

The BFF pattern provides natural cross-tab session sharing and prevents browser JavaScript from directly reading OAuth tokens. Cookie-authenticated endpoints must also implement CSRF defenses and appropriate origin checks.

Do not place an access token directly into an ordinary application cookie and assume that this creates a BFF. The preferred cookie contains only an opaque session identifier; the token cache is server-side.

## MSAL and Microsoft Entra ID

MSAL Browser is a public-client library. It manages its token cache and normally does not expose refresh tokens to application code. Before each protected API call, the client should call `acquireTokenSilent()` and use the returned access token:

```ts
async function getAccessToken(scopes: string[]): Promise<string> {
  const account =
    msal.getActiveAccount() ??
    msal.getAllAccounts()[0];

  if (!account) {
    throw new Error("No signed-in account");
  }

  const result = await msal.acquireTokenSilent({ account, scopes });
  return result.accessToken;
}
```

`acquireTokenSilent()` first uses a valid cached access token and then attempts silent renewal when necessary. If silent acquisition throws `InteractionRequiredAuthError`, an active tab may begin `acquireTokenRedirect()` or `acquireTokenPopup()`.

Microsoft Entra maintains its own SSO cookie on the Microsoft login origin. The application and MSAL cannot read that cookie. When an MSAL refresh token is no longer usable, a new authorization flow may still complete without a credential prompt if the Entra SSO session and browser policy permit it. If that session has expired or Conditional Access, MFA, consent, or browser privacy rules require interaction, the application cannot safely bypass the prompt.

MSAL Browser's cache choices include memory, `sessionStorage`, and `localStorage`. Its cookie compatibility options are not an `HttpOnly` OAuth token store. Using application-owned `HttpOnly` cookies requires a server-side web application or BFF architecture.

## Page Refresh and Multiple Tabs with MSAL

Initialize MSAL and process a redirect response before rendering protected UI or calling an API:

```ts
await msal.initialize();

const response = await msal.handleRedirectPromise();
if (response?.account) {
  msal.setActiveAccount(response.account);
}

const account = msal.getActiveAccount() ?? msal.getAllAccounts()[0];
if (account) {
  msal.setActiveAccount(account);
}
```

Do not make every new tab call `loginRedirect()` during startup. The preferred sequence is:

1. Initialize MSAL and process a possible redirect.
2. Look for a cached account.
3. If an account exists, call `acquireTokenSilent()`.
4. If interaction is required, allow only one visible tab to begin it.
5. Let the other tabs wait and then reread the shared cache.

For cross-tab behavior:

- `localStorage` provides a shared MSAL cache across same-origin tabs but increases credential exposure if XSS occurs.
- `sessionStorage` survives a refresh in the same tab but is not a reliable shared cache across independently opened tabs.
- `BroadcastChannel` can communicate `LOGIN_STARTED`, `LOGIN_COMPLETED`, and `LOGOUT_COMPLETED` events.
- Never broadcast access or refresh tokens. Broadcast an event, then let each tab call MSAL.
- Use a cross-tab lock or leader election so only one tab starts interactive authentication.
- Hidden tabs should wait; only the active, visible tab should navigate to login.

The same coordination applies when multiple tabs receive `401` responses. They should not all trigger redirects.

## XSS and Browser Token Storage

Tokens in `localStorage` cannot be made completely safe from XSS. If malicious JavaScript executes on the application's origin, it can read browser-accessible storage or invoke APIs as the user. `sessionStorage` and memory storage reduce persistence but do not stop active injected JavaScript.

The strongest way to remove OAuth tokens from JavaScript is a BFF with an `HttpOnly` session cookie. This reduces token exfiltration risk but does not eliminate XSS; malicious code may still perform actions through the current browser session.

When a browser-only SPA is required, use defense in depth:

- Let MSAL manage its own cache; do not copy tokens into Redux, logs, analytics, or extra storage keys.
- Apply a strict Content Security Policy without `unsafe-inline` or `unsafe-eval` where practical.
- Prefer framework text rendering and avoid `innerHTML` or React's `dangerouslySetInnerHTML` with untrusted input.
- Sanitize genuinely required HTML with a maintained sanitizer.
- Validate user-controlled URLs and reject dangerous schemes such as `javascript:`.
- Minimize and audit third-party scripts and dependencies.
- Use short-lived, narrowly scoped access tokens and appropriate revocation and monitoring.

## Logout

Logout can involve several different actions:

1. Clear the application's local session.
2. Revoke or discard refresh capability when appropriate.
3. Clear browser-side application state in every tab.
4. Optionally initiate OIDC provider logout.

Logging out of Carsties does not automatically mean the user is logged out of every application using the same identity provider. Conversely, clearing only the UI state while leaving a refresh token or server session valid is not a complete application logout.

## Common Mistakes

- Treating OAuth as proof that a user authenticated instead of using OIDC.
- Sending an ID token to an API instead of an access token.
- Decoding a JWT without verifying its signature and claims.
- Accepting a token issued for another audience.
- Using an email address as the durable user identifier.
- Capturing one access token at startup and never replacing it.
- Letting simultaneous requests rotate the same refresh token.
- Retrying `401` responses indefinitely.
- Treating `403` as an expired-token problem.
- Starting interactive login from every browser tab.
- Storing tokens in `localStorage` while assuming they are protected from XSS.
- Automatically restoring a session immediately after an intentional idle-security logout.

## Compact Mental Model

```text
Authorization code -> token endpoint -> complete this login securely
ID token           -> client         -> who authenticated?
Access token       -> API            -> what may this caller access?
Refresh token      -> token endpoint -> issue replacement tokens
Session cookie     -> application    -> which browser session is this?
```

The client validates the ID token. The API validates the access token. The authorization server handles refresh tokens. The application controls its own session policy.

## Further Reading

- [OAuth 2.0 Authorization Framework (RFC 6749)](https://www.rfc-editor.org/rfc/rfc6749)
- [OAuth 2.0 Security Best Current Practice (RFC 9700)](https://www.rfc-editor.org/rfc/rfc9700)
- [Proof Key for Code Exchange (RFC 7636)](https://www.rfc-editor.org/rfc/rfc7636)
- [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html)
- [Microsoft: Token lifetimes in MSAL.js](https://learn.microsoft.com/en-us/entra/msal/javascript/browser/token-lifetimes)
- [Microsoft: Acquire tokens in a single-page application](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-spa-acquire-token)
- [Microsoft: SSO with MSAL.js](https://learn.microsoft.com/en-us/entra/msal/javascript/browser/single-sign-on)
- [OWASP Cross-Site Scripting Prevention Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Cross_Site_Scripting_Prevention_Cheat_Sheet.html)

