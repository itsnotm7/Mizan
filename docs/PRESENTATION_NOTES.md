# Mizan: Presentation Notes

## Auth (login, JWT, roles)

- **What it does:** `POST /api/auth/login` checks the email and password and returns a signed JWT holding the user's id (`sub`), `email` and `role`. The client sends that token with each request. `GET /api/auth/me` returns the logged-in user.
- **Where it sits:** Application defines `IAuthService` and `ITokenService`. Infrastructure implements them because they need the database and crypto libraries. The controller only calls the interface, so the dependency arrows still point inward.
- **Secure by default:** `MapControllers().RequireAuthorization()` protects every controller. Login is the only endpoint marked `[AllowAnonymous]`, so forgetting `[Authorize]` on a new endpoint can't make it public.
- **Signing key:** it lives in user-secrets, outside the repo. The app refuses to start if the key is missing or shorter than 32 bytes. A new machine needs `dotnet user-secrets set "Jwt:SigningKey" "<key>" --project TicketingSystem.API`.
- **Errors:** JSON clients get errors in the standard `ProblemDetails` format (validation errors add an `errors` field), and a global handler logs exceptions without sending stack traces to the client.

### Likely committee questions

- **Why JWT instead of sessions?** The API stays stateless, so it doesn't store login state, and a React client can send the token in a header. The tradeoff is that a token can't easily be cancelled before it expires, so tokens last only 2 hours.
- **Can a user edit their token to become Admin?** No. The signature covers the contents, so any change makes it invalid and the API returns 401. We tested this, along with `alg:none` tokens.
- **Is the data in the token secret?** No. A JWT is signed, not encrypted, and anyone can read it. That's why it holds no password or other sensitive data.
- **How are passwords stored?** As salted hashes from `PasswordHasher<User>`, never as plain text. Login hashes the submitted password and compares the result.
- **Why the same error for a wrong password and an unknown email?** To make it hard to use the login form to find out which emails exist. Unknown emails also go through a password hash check, so both cases take about the same time (191 ms vs 173 ms in our test).
- **What's the difference between 401 and 403?** 401 means we don't know who you are (no valid token). 403 means we know who you are, but your role isn't allowed.
- **Why does `/me` read the database instead of the token?** The token could be up to 2 hours old. Reading the database means a deactivated user is refused right away on this endpoint.

### Future work

Refresh tokens, logout and token revocation, and rate limiting on login.
