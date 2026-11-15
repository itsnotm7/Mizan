# 5. API: the front door

`TicketingSystem.API` is the only project that speaks HTTP. It receives every request, decides who the caller is (JWT), sends the request through a fixed chain of middleware, and hands the work to an Application service. It never contains business rules: controllers are a few lines each, and every "no" (401, 403, 404, 409) is either produced by the framework or by an Application exception that one handler turns into a status code. This chapter goes through every file in the project, in the order a request meets them.

| File | One line |
|---|---|
| [Program.cs](../../TicketingSystem.API/Program.cs) | Startup: registers services, then builds the middleware pipeline in a deliberate order. |
| [Extensions/AuthExtensions.cs](../../TicketingSystem.API/Extensions/AuthExtensions.cs) | `AddJwtAuth`: configures JwtBearer to validate our tokens. |
| [Extensions/ClaimsPrincipalExtensions.cs](../../TicketingSystem.API/Extensions/ClaimsPrincipalExtensions.cs) | `User.GetUserId()`: reads the user id from the token's `sub` claim. |
| [Middleware/GlobalExceptionHandler.cs](../../TicketingSystem.API/Middleware/GlobalExceptionHandler.cs) | Turns exceptions into ProblemDetails responses (401/403/404/409/400/500). |
| [OpenApi/BearerSecurityTransformer.cs](../../TicketingSystem.API/OpenApi/BearerSecurityTransformer.cs) | Adds the bearer-token scheme to the OpenAPI document, so Swagger UI shows "Authorize". |
| [Controllers/AuthController.cs](../../TicketingSystem.API/Controllers/AuthController.cs) | `POST /api/auth/login` (public) and `GET /api/auth/me`. |
| [Controllers/TicketsController.cs](../../TicketingSystem.API/Controllers/TicketsController.cs) | Create, list, details and history of tickets. |
| [Controllers/TicketWorkflowController.cs](../../TicketingSystem.API/Controllers/TicketWorkflowController.cs) | The 10 workflow steps, one `POST` each. |
| [Controllers/TeamsController.cs](../../TicketingSystem.API/Controllers/TeamsController.cs) | `GET /api/teams` for the route and assign pickers. |
| [appsettings.json](../../TicketingSystem.API/appsettings.json) | Connection string, JWT issuer/audience/expiry, logging. No secrets. |
| [appsettings.Development.json](../../TicketingSystem.API/appsettings.Development.json) | Logging overrides for Development. |
| [Properties/launchSettings.json](../../TicketingSystem.API/Properties/launchSettings.json) | The `http` (5199) and `https` (7005 + 5199) run profiles. |
| [TicketingSystem.API.csproj](../../TicketingSystem.API/TicketingSystem.API.csproj) | Project file: packages, project references, user-secrets id. |

---

## [Program.cs](../../TicketingSystem.API/Program.cs)

**What it is:** The entry point of the whole backend. The first half registers services in the DI container; the second half builds the HTTP pipeline (the chain of middleware every request passes through).

The file uses **top-level statements**: there is no `class Program` and no `Main` method. The C# compiler generates them for you, and the code simply runs from top to bottom.

### Part 1: usings and the builder

```csharp
using System.Text.Json.Serialization;
using TicketingSystem.API.Extensions;
using TicketingSystem.API.Middleware;
using TicketingSystem.API.OpenApi;
using TicketingSystem.Application;
using TicketingSystem.Infrastructure;
using TicketingSystem.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
```

**Line by line:**
- Lines 1-7: the namespaces we need. `TicketingSystem.Application` and `TicketingSystem.Infrastructure` give us `AddApplication()` and `AddInfrastructure()`. `Infrastructure.Persistence` gives `AppDbContext` and `DbSeeder` for seeding.
- Line 9: `WebApplication.CreateBuilder(args)` creates the **WebApplication builder**. It loads configuration (appsettings.json, then appsettings.{Environment}.json, then user-secrets in Development, then environment variables, then command-line args; later sources override earlier ones), sets up logging, and gives us `builder.Services`, the DI container we fill next.

### Part 2: service registrations

```csharp
// Add services to the container.
builder.Services.AddApplication(); // Business logic services
builder.Services.AddInfrastructure(builder.Configuration); // DbContext + SQL Server + auth services + repositories
builder.Services.AddJwtAuth(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Lets requests send enums by name ("High") instead of by number
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecurityTransformer>());

var app = builder.Build();
```

**Line by line:**
- Line 12, `AddApplication()`: an extension method defined in the Application project. It registers `TicketService`, `TicketWorkflowService` and `TeamService` as scoped (one instance per HTTP request). Program.cs stays one line per layer.
- Line 13, `AddInfrastructure(builder.Configuration)`: defined in Infrastructure. It registers `AppDbContext` with SQL Server, `JwtOptions` (as a singleton), the password hasher, the token service, `AuthService` and the three repositories. It needs the configuration to read the connection string.
- Line 14, `AddJwtAuth(builder.Configuration)`: our own extension in [AuthExtensions.cs](#extensionsauthextensionscs). It turns on JWT bearer authentication and authorization.
- Line 16, `AddProblemDetails()`: registers `IProblemDetailsService`. **ProblemDetails** is a standard JSON shape for HTTP errors (RFC 7807, updated by RFC 9457, with status meanings from RFC 9110): `type`, `title`, `status`, plus extras such as `traceId`. After this line, the exception handler, `UseStatusCodePages` and `ControllerBase.Problem` all produce that same shape, so the React app reads every error the same way.
- Line 17, `AddExceptionHandler<GlobalExceptionHandler>()`: registers our [GlobalExceptionHandler](#middlewareglobalexceptionhandlercs). It does nothing alone; `app.UseExceptionHandler()` (line 28) is what calls it.
- Lines 20-21, `AddControllers()`: registers MVC controllers (finding them, model binding, validation, JSON). `.AddJsonOptions(...)` adds `JsonStringEnumConverter`, so a request can send `"priority": "High"` instead of `2`. Note: numbers are still accepted by this converter, which is why `CreateTicketRequest.Priority` has `[EnumDataType(typeof(Priority))]` to reject values like `99`. The response DTOs already convert enums to strings themselves (`Status.ToString()`), so this mostly matters for input.
- Line 23, `AddOpenApi(...)`: the built-in .NET OpenAPI generator. It describes every endpoint in a JSON document. `AddDocumentTransformer<BearerSecurityTransformer>()` lets our class edit that document after it is generated (see [BearerSecurityTransformer.cs](#openapibearersecuritytransformercs)).
- Line 25, `builder.Build()`: freezes the service list and creates `app`. From here on we configure the pipeline, not services.

### Part 3: the middleware pipeline

**Middleware** is a component that sits in the request path. Each one can do work, then call the next one, then do more work on the way back out. They run in the order they are added, which is why the order below is deliberate.

```csharp
// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages(); // Gives empty responses like 401 and 403 a ProblemDetails body too

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Mizan API"));

    // Fill an empty database with demo users and a team (Development only)
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(db);
}

// In Development the React app reaches the API over plain http through the Vite proxy,
// and a redirect to https would break those calls. Everywhere else, force https.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

// Order matters: first find out who the caller is, then check what they may do
app.UseAuthentication();
app.UseAuthorization();

// Every controller requires a login unless it opts out with [AllowAnonymous],
// so forgetting [Authorize] on a new endpoint can't make it public
app.MapControllers().RequireAuthorization();

app.Run();
```

**Line by line:**
- Line 28, `UseExceptionHandler()`: added first so it wraps everything after it. If any later middleware, controller or service throws, the exception travels back up to here, and this middleware asks the registered `IExceptionHandler` (our `GlobalExceptionHandler`) to write the response. Without arguments it relies on `AddProblemDetails()` being registered.
- Line 29, `UseStatusCodePages()`: some responses leave the pipeline with an error status but an empty body. Examples: JwtBearer's 401 when there is no token, or a 404 for a URL that matches no route. Because `AddProblemDetails()` is registered, this middleware fills such responses with a ProblemDetails body. Responses that already have a body (like those from `Problem(...)` or the exception handler) are left alone.
- Line 31, `if (app.Environment.IsDevelopment())`: the environment comes from `ASPNETCORE_ENVIRONMENT`, which launchSettings.json sets to `Development`. Everything inside this block never runs in production.
- Line 33, `MapOpenApi()`: serves the generated document at `/openapi/v1.json`. Note: this endpoint is not a controller, so `RequireAuthorization()` on line 53 does not cover it; it is public, but only exists in Development.
- Line 34, `UseSwaggerUI(...)`: serves the Swagger UI page at `/swagger` and points it at our OpenAPI document. Only the UI package is used (`Swashbuckle.AspNetCore.SwaggerUI`); the document itself comes from Microsoft's generator.
- Lines 37-39: seeding. At this point no HTTP request exists, so there is no request scope. `AppDbContext` is scoped, so we create a scope by hand (`CreateScope()`), get the DbContext from it, and run `DbSeeder.SeedAsync`. `using var` disposes the scope (and the DbContext) at the end of the `if` block. `await` works here because top-level statements can be async.
- Lines 44-45, `UseHttpsRedirection()` only outside Development. In Development, React runs on the Vite dev server and Vite's proxy forwards `/api` calls to `http://localhost:5199` (plain http). If the API answered with a redirect to https, those proxied calls would break, and on the `http` profile there is no https port to redirect to anyway (the API would log "Failed to determine the https port"). In production, forcing https protects the JWT in the `Authorization` header from being read on the network.
- Line 48, `UseAuthentication()`: reads the `Authorization: Bearer <token>` header and, if the token is valid, fills `HttpContext.User` (a **ClaimsPrincipal**: the caller's identity as a list of claims such as `sub`, `email`, `role`). An invalid or missing token does NOT stop the request here; it just leaves the user anonymous.
- Line 49, `UseAuthorization()`: checks the endpoint's rules (here: "must be logged in"). If the user is anonymous and the endpoint needs a login, it returns 401 (a "challenge"). It must come after `UseAuthentication`, because you can only check what someone may do after you know who they are. Swapped, every user would look anonymous and every protected call would get 401.
- Line 53, `MapControllers().RequireAuthorization()`: registers every controller action as an endpoint (using **attribute routing**: the `[Route]` and `[HttpGet]`/`[HttpPost]` attributes on controllers decide the URLs). `.RequireAuthorization()` adds "must be authenticated" to all of them at once. This is secure by default: a new controller is protected even if someone forgets `[Authorize]`. The only way to make an endpoint public is to write `[AllowAnonymous]` on purpose, which only `Login` does.
- Line 55, `app.Run()`: starts Kestrel (the built-in web server) and blocks until the app shuts down.

Note: `UseRouting()` and `UseEndpoints()` are not called by hand. `WebApplication` adds routing at the start of the pipeline and the endpoint execution at the end automatically. That is why `UseAuthorization` already knows which endpoint (and which `[AllowAnonymous]` or `RequireAuthorization` rules) applies to the request.

Note: in Development, `WebApplication` also adds the developer exception page automatically, outside our pipeline. Because `UseExceptionHandler()` handles exceptions first, clients still get our ProblemDetails, not the developer page.

**Committee might ask:**
- *Q: Why does the order of `UseAuthentication` and `UseAuthorization` matter?* A: Authentication finds out who the caller is from the token; authorization then checks if that caller may use the endpoint. Reversed, authorization would see an anonymous user every time and return 401.
- *Q: Why is `UseExceptionHandler` first?* A: Middleware only catches exceptions from what comes after it. First means it covers everything: auth, controllers, services, the database.
- *Q: What happens if a new endpoint forgets `[Authorize]`?* A: Nothing bad. `RequireAuthorization()` on `MapControllers()` protects every controller. Making something public requires `[AllowAnonymous]`, which is a visible, deliberate choice.

---

## [Extensions/AuthExtensions.cs](../../TicketingSystem.API/Extensions/AuthExtensions.cs)

**What it is:** One extension method, `AddJwtAuth`, that configures how incoming JWTs are validated. Tokens are created in Infrastructure (`JwtTokenService`); this file is the checking side.

**The code:**

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TicketingSystem.Infrastructure.Auth;

namespace TicketingSystem.API.Extensions
{
    public static class AuthExtensions
    {
        public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration configuration)
        {
            var jwt = JwtOptions.Load(configuration);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // Keep claim names as written in the token ("sub", "role").
                    // By default .NET renames them to long URIs, which breaks FindFirst("sub").
                    options.MapInboundClaims = false;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = jwt.CreateSigningKey(),
                        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], // Accept only the algorithm we sign with
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1), // The default allows 5 extra minutes after expiry

                        // [Authorize(Roles = ...)] looks for roles in the claim named here
                        RoleClaimType = JwtOptions.RoleClaimType,
                        NameClaimType = JwtRegisteredClaimNames.Sub
                    };
                });

            services.AddAuthorization();

            return services;
        }
    }
}
```

**Line by line:**
- Line 10, `this IServiceCollection services`: the `this` makes it an extension method, so Program.cs can write `builder.Services.AddJwtAuth(...)`. Returning `services` allows chaining.
- Line 12, `JwtOptions.Load(configuration)`: this is the **startup check**. `JwtOptions` lives in Infrastructure ([JwtOptions.cs](../../TicketingSystem.Infrastructure/Auth/JwtOptions.cs)). `Load` reads the `Jwt` section and throws `InvalidOperationException` right at startup if the issuer or audience is empty, if `Jwt:SigningKey` is shorter than 32 bytes (HMAC-SHA256 needs at least 256 bits), or if `ExpiryMinutes` is not positive. The error message tells you the `dotnet user-secrets set` command to run. So a machine without the key fails immediately with a clear message, instead of failing on the first login. The key itself is never in a tracked file; it comes from user-secrets.
- Line 14, `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)`: registers authentication and makes "Bearer" the default scheme, so `UseAuthentication()` uses JwtBearer for every request.
- Line 15, `AddJwtBearer(...)`: the JwtBearer handler reads the `Authorization: Bearer ...` header, validates the token with the rules below, and builds `HttpContext.User` from its claims.
- Line 19, `MapInboundClaims = false`: by default .NET renames short JWT claim names to long Microsoft URIs (for example `sub` becomes `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`). Turning that off keeps `sub` and `role` as written, so `FindFirstValue("sub")` in `GetUserId` works.
- Lines 23-24: the token's `iss` (issuer) must equal `Mizan.Api` (from appsettings.json).
- Lines 25-26: the token's `aud` (audience) must equal `Mizan.Web`. A token issued for another app is rejected.
- Lines 27-28: the signature must be valid with our signing key. `CreateSigningKey()` is the same method `JwtTokenService` uses to sign, so signing and checking can't use different keys. This is what stops anyone from editing the token (for example changing `role` to `Admin`): the signature would no longer match.
- Line 29, `ValidAlgorithms = [SecurityAlgorithms.HmacSha256]`: only HS256 is accepted. This blocks attacks that try to switch the algorithm (for example `alg: none`).
- Line 30, `ValidateLifetime = true`: expired tokens are rejected (`exp` claim; tokens last 120 minutes, from `Jwt:ExpiryMinutes`).
- Line 31, `ClockSkew = 1 minute`: allows for small clock differences between servers. The default is 5 minutes, which would keep an expired token working for 5 extra minutes.
- Line 34, `RoleClaimType = "role"`: tells .NET which claim holds the role, for `[Authorize(Roles = ...)]` and `User.IsInRole`. Note: the project does not use `[Authorize(Roles)]` (see TicketsController), so this is set for correctness, not relied on.
- Line 35, `NameClaimType = sub`: makes `User.Identity.Name` return the user id.
- Line 39, `AddAuthorization()`: registers the authorization services used by `UseAuthorization()` and `RequireAuthorization()`.

Note: `JwtOptions.Load` runs twice at startup, once here and once in `AddInfrastructure` (which registers the result as a singleton for `JwtTokenService`). It is harmless (same input, same checks), just a small duplication.

**Committee might ask:**
- *Q: Where is the signing key stored?* A: In .NET user-secrets on the developer machine, outside the repository. appsettings.json is tracked by git, so it only holds the issuer, audience and expiry. In production it would come from an environment variable or a secret store.
- *Q: What if someone changes their role inside the token?* A: The signature check fails and JwtBearer rejects the token with 401. And even with a valid token, the services re-read the role from the database.
- *Q: Why `MapInboundClaims = false`?* A: So claim names stay short (`sub`, `role`) and match what we wrote into the token.

---

## [Extensions/ClaimsPrincipalExtensions.cs](../../TicketingSystem.API/Extensions/ClaimsPrincipalExtensions.cs)

**What it is:** A one-method helper so every controller can write `User.GetUserId()` to get the caller's id as a `Guid`.

**The code:**

```csharp
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace TicketingSystem.API.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        // Only call this on authenticated endpoints. Our tokens always carry a valid sub,
        // so a failure here means a bug, and it should surface as a 500.
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? throw new InvalidOperationException("The authenticated user has no 'sub' claim.");

            return Guid.Parse(sub);
        }
    }
}
```

**Line by line:**
- Line 10: an extension method on `ClaimsPrincipal`. In a controller, `User` (from `ControllerBase`) is the `ClaimsPrincipal` that JwtBearer built.
- Line 12: `JwtRegisteredClaimNames.Sub` is the string `"sub"` (subject). `JwtTokenService` writes the user's id into it when it creates the token. This only works because `MapInboundClaims = false` keeps the name `sub`.
- Line 13: if there is no `sub`, something is broken in our own code (every token we issue has one), so it throws `InvalidOperationException`. The global handler turns that into a 500 and logs it, which is right for a bug.
- Line 15: `Guid.Parse` converts the string to a `Guid`. A bad value would also throw and become a 500, for the same reason.

The controller only passes this id to the service. The service then loads the user fresh from the database (`GetActingUserAsync`), so the token is only trusted for "who", never for "what role" or "still active".

**Committee might ask:**
- *Q: Why not send the user id in the request body or URL?* A: Then a caller could pretend to be someone else. The id comes only from the signed token.

---

## [Middleware/GlobalExceptionHandler.cs](../../TicketingSystem.API/Middleware/GlobalExceptionHandler.cs)

**What it is:** The one place that turns exceptions into HTTP responses. Application throws meaningful exceptions (`NotFoundException`, `ConflictException`, and so on) because it does not know about HTTP; this class translates them into status codes.

**The code:**

```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TicketingSystem.Application.Exceptions;

namespace TicketingSystem.API.Middleware
{
    // Any unhandled exception becomes the same ProblemDetails response.
    // The details go to the log only: clients must never see stack traces.
    public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            // Application throws these on purpose, so their messages are safe to show.
            // Application can't use status codes itself: it doesn't know about HTTP.
            var (status, title) = exception switch
            {
                UnauthorizedException => (StatusCodes.Status401Unauthorized, exception.Message),
                ForbiddenException => (StatusCodes.Status403Forbidden, exception.Message),
                NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                ConflictException => (StatusCodes.Status409Conflict, exception.Message),
                BadRequestException => (StatusCodes.Status400BadRequest, exception.Message),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
            };

            // A refused request is normal behaviour. Only real failures are logged as errors.
            if (status == StatusCodes.Status500InternalServerError)
                logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

            httpContext.Response.StatusCode = status;

            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = status,
                    Title = title
                }
            });
        }
    }
}
```

**Line by line:**
- Line 9: a primary constructor (C# 12): the logger and `IProblemDetailsService` are injected by DI and usable anywhere in the class. **IExceptionHandler** is the .NET 8+ interface for exception handlers: `UseExceptionHandler()` calls `TryHandleAsync` for every unhandled exception.
- Line 11, `ValueTask<bool>`: `ValueTask` is a lighter version of `Task` for methods that often finish without real async work. Returning `true` means "handled, the response is written"; `false` would let the next handler (or the default behaviour) try.
- Lines 15-23: a switch expression on the exception type. Each Application exception maps to one status, and its message becomes the `title`. Those messages are written by us for the client (for example "Ticket not found."), so they are safe to show.
- Line 22, `_ =>`: anything else (a database outage, a null reference, a bug) becomes 500 with a fixed, generic title. The real message and stack trace are never sent to the client; they could reveal table names, file paths or code.
- Lines 26-27: only 500s are logged as errors. A 403 or 409 is the system working correctly (someone tried something they may not do), so logging it as an error would bury real problems. The log line includes the method and path for context.
- Line 29: sets the status before writing the body.
- Lines 31-40: `TryWriteAsync` writes the ProblemDetails as JSON. We set only `Status` and `Title`. The ProblemDetails service adds the standard `type` link (for example the RFC 9110 section for 409) and a `traceId`, which you can match against the server logs. The `Exception` is passed for context, but our `ProblemDetails` has no `detail` field, so the exception text is not added.

Where each status is produced in this project:

| Status | Produced by |
|---|---|
| 400 | `[ApiController]` automatic validation (field errors), or `BadRequestException` (for example "Team not found." in Route, "not an active member" in Assign) |
| 401 | JwtBearer + `UseAuthorization` (missing/invalid/expired token, empty body filled by `UseStatusCodePages`), `UnauthorizedException` from `GetActingUserAsync` (account deactivated), or `Problem(401)` in `AuthController` |
| 403 | `ForbiddenException` from `TicketWorkflow.Apply`, `TicketService.CreateAsync` (non-clients), `TeamService.GetTeamsAsync` |
| 404 | `NotFoundException` when the ticket does not exist or is not visible to the caller; also a URL that matches no route (for example a non-Guid id) |
| 409 | `ConflictException` from `TicketWorkflow.Apply` (wrong status), from `TicketRepository.SaveChangesAsync` (RowVersion concurrency), or a missing draft in ManagerApprove |
| 500 | Anything else, logged |

**Committee might ask:**
- *Q: Why not return status codes from the services directly?* A: Application must not depend on ASP.NET Core (Clean Architecture). It throws exceptions that describe the problem; the API layer decides what that means in HTTP.
- *Q: Why are 403/404/409 not logged?* A: They are expected outcomes, not failures. Only unexpected 500s need a developer's attention.
- *Q: Can a client see a stack trace?* A: No. A 500 always has the same generic title. The details go to the log only, linked by `traceId`.

---

## [OpenApi/BearerSecurityTransformer.cs](../../TicketingSystem.API/OpenApi/BearerSecurityTransformer.cs)

**What it is:** A small class that edits the generated OpenAPI document to say "this API uses bearer tokens". That is what makes Swagger UI show its Authorize button.

**The code:**

```csharp
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TicketingSystem.API.OpenApi
{
    // Tells the OpenAPI document that the API uses bearer tokens,
    // which is what makes Swagger UI show its Authorize button
    public class BearerSecurityTransformer : IOpenApiDocumentTransformer
    {
        private const string SchemeName = "Bearer";

        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            };

            document.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(SchemeName, document)] = []
                }
            ];

            return Task.CompletedTask;
        }
    }
}
```

**Line by line:**
- Line 8: an **OpenAPI document transformer** is a class that gets the finished OpenAPI document and can change it before it is served. It is registered in Program.cs line 23.
- Lines 14-15: `??=` creates the `Components` and `SecuritySchemes` objects only if they are null, so we never overwrite something already there.
- Lines 16-21: defines a security scheme named "Bearer": HTTP authentication, scheme `bearer`, token format JWT. Swagger UI turns this into the Authorize dialog where you paste the token.
- Lines 23-29: a document-level security requirement: every operation uses the "Bearer" scheme, so Swagger UI sends the `Authorization` header on every call. The empty list `[]` means "no scopes" (scopes are an OAuth idea we don't use).
- Line 31: nothing async happens, so it returns a completed task.

Note: the requirement is applied to the whole document, including `POST /api/auth/login`, which is `[AllowAnonymous]`. That is harmless: sending a token to login does not matter. Clicking Authorize in Swagger UI in a browser is still listed as "not tested" in CLAUDE.md.

**Committee might ask:**
- *Q: Is this needed for security?* A: No. It only describes the API for Swagger UI. Security is enforced by JwtBearer and `RequireAuthorization()`.

---

## [Controllers/AuthController.cs](../../TicketingSystem.API/Controllers/AuthController.cs)

**What it is:** Login (the only public endpoint) and `/me`, which returns the current user.

**The code:**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketingSystem.API.Extensions;
using TicketingSystem.Application.DTOs.Auth;
using TicketingSystem.Application.Interfaces;

namespace TicketingSystem.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController(IAuthService authService) : ControllerBase
    {
        [AllowAnonymous] // The one public endpoint: Program.cs protects everything else
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
        {
            var response = await authService.LoginAsync(request, cancellationToken);

            return response is null
                ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.")
                : Ok(response);
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
        {
            var user = await authService.GetCurrentUserAsync(User.GetUserId(), cancellationToken);

            return user is null
                ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "This account is no longer active.")
                : Ok(user);
        }
    }
}
```

**Line by line:**
- Line 9, `[ApiController]`: turns on API behaviours. The two that matter here: (1) automatic model validation: before the action runs, the data-annotation attributes on the request class are checked, and if any fail, the framework returns **400** with a `ValidationProblemDetails` body (an `errors` object listing each field's messages) and the action never runs; (2) binding inference: complex parameters like `LoginRequest` are read from the JSON body automatically, as if they had **`[FromBody]`** (the attribute that says "deserialize this parameter from the request body").
- Line 10, `[Route("api/auth")]`: attribute routing. Every action's route is added to this prefix.
- Line 11: `ControllerBase` is the base class for API controllers (no views). It gives `User`, `Ok()`, `Problem()`, `CreatedAtAction()`. `IAuthService` is injected through the primary constructor; its implementation is in Infrastructure, but the controller only knows the Application interface.
- Line 13, `[AllowAnonymous]`: overrides the global `RequireAuthorization()`. You can't require a token to get a token.
- Line 14, `[HttpPost("login")]`: so the URL is `POST /api/auth/login`.
- Line 15, `ActionResult<LoginResponse>`: **`ActionResult<T>`** means "either a `T` (sent as 200 JSON) or any other result, like a ProblemDetails error". It also tells OpenAPI the success type. `CancellationToken cancellationToken`: ASP.NET Core binds this automatically to `HttpContext.RequestAborted`. If the client disconnects, the token is cancelled, and EF Core stops the database query. It is passed down through every layer.
- Line 15, `LoginRequest request`: `[ApiController]` already validated `[Required]`, `[EmailAddress]` and `[MaxLength]` on it. A bad email format never reaches the service.
- Line 17: the service checks the email and password hash. It returns `null` for both "unknown email" and "wrong password", and it also spends hashing time on unknown emails, so attackers can't tell which emails exist.
- Lines 19-21: `null` becomes 401 through **`ControllerBase.Problem`**, which builds a ProblemDetails response with the given status and title. The same message for both cases is deliberate. Otherwise 200 with `{ token, expiresAt, user }`.
- Line 24, `[HttpGet("me")]`: no `[AllowAnonymous]`, so a token is required. Without one, JwtBearer returns 401 before this code runs.
- Line 27: `User.GetUserId()` reads the id from the token; the service then re-reads the user from the database.
- Lines 29-31: if the user was deactivated or deleted after logging in, their token is still valid (JwtBearer only checks the token), so the controller returns 401 itself. The React app uses `/me` after a page refresh, and logs out on any 401.

**Committee might ask:**
- *Q: Why the same message for wrong email and wrong password?* A: A different message would let an attacker find out which emails have accounts.
- *Q: Where is the password checked?* A: In `AuthService` (Infrastructure) with `PasswordHasher<User>`, which compares against a salted hash. The plain password is never stored.
- *Q: What does `[ApiController]` give you?* A: Automatic 400 responses for invalid input (in ProblemDetails format), body binding without `[FromBody]`, and attribute routing is required.

---

## [Controllers/TicketsController.cs](../../TicketingSystem.API/Controllers/TicketsController.cs)

**What it is:** The four read/create endpoints for tickets. Each action is: get the user id, call `ITicketService`, return the DTO.

**The code:**

```csharp
using Microsoft.AspNetCore.Mvc;
using TicketingSystem.API.Extensions;
using TicketingSystem.Application.DTOs.Tickets;
using TicketingSystem.Application.Interfaces;

namespace TicketingSystem.API.Controllers
{
    // No [Authorize(Roles=...)] here: that reads the role from the token, which can be up to
    // 2 hours old. TicketService checks the current role in the database instead.
    [ApiController]
    [Route("api/tickets")]
    public class TicketsController(ITicketService ticketService) : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<TicketDetailsDto>> Create(CreateTicketRequest request, CancellationToken cancellationToken)
        {
            var ticket = await ticketService.CreateAsync(User.GetUserId(), request, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TicketListItemDto>>> List(CancellationToken cancellationToken)
        {
            var tickets = await ticketService.GetVisibleTicketsAsync(User.GetUserId(), cancellationToken);

            return Ok(tickets);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<TicketDetailsDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var ticket = await ticketService.GetByIdAsync(User.GetUserId(), id, cancellationToken);

            return Ok(ticket);
        }

        [HttpGet("{id:guid}/history")]
        public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> History(Guid id, CancellationToken cancellationToken)
        {
            var history = await ticketService.GetHistoryAsync(User.GetUserId(), id, cancellationToken);

            return Ok(history);
        }
    }
}
```

**Line by line:**
- Lines 8-9: the reason there is no `[Authorize(Roles = "Client")]`. The role inside a token is fixed when the token is created and lives 120 minutes. If a manager changes someone's role or deactivates them, the old token would still pass a role attribute. Instead, every service method starts with `GetActingUserAsync`, which loads the user from the database (401 if inactive) and uses the current role. So the rule "only clients create tickets" is in `TicketService.CreateAsync`, and it throws `ForbiddenException` (403).
- Line 14-15, `Create`: `POST /api/tickets`. `CreateTicketRequest` is validated by `[ApiController]` first (title, description, GitHub URL regex, branch, commit SHA regex, priority). Invalid input means 400 with field errors, which the React form shows via `ApiError.fieldErrors`.
- Line 19, `CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket)`: returns **201 Created** instead of 200. It also adds a `Location` header with the URL of the new ticket (`/api/tickets/{id}`), built from the `GetById` route, and puts the ticket DTO in the body. That is the REST convention for "a new resource was created". `nameof` avoids a typo in the action name.
- Lines 22-28, `List`: `GET /api/tickets`. The service filters by role using `TicketVisibility` (a client sees their own tickets, a leader sees their team's tickets, and so on). Returns 200 with a list of `TicketListItemDto`.
- Line 30, `[HttpGet("{id:guid}")]`: `{id:guid}` is a **route constraint**: the route only matches if that URL segment is a valid Guid. `/api/tickets/abc` matches no route and returns 404 (with a ProblemDetails body from `UseStatusCodePages`), before any code runs. It also keeps `/api/tickets/{id}` from clashing with other paths.
- Lines 31-35, `GetById`: if the ticket doesn't exist OR the caller can't see it, the service throws `NotFoundException` (404) in both cases, so nobody can probe which ticket ids exist. The DTO includes `allowedActions`, which the React details page uses to decide which buttons to show.
- Lines 38-44, `History`: `GET /api/tickets/{id}/history`, the audit log. Same visibility rule (404 if not visible). Clients only see the client-facing entries.

Every action returns a DTO (`TicketDetailsDto`, `TicketListItemDto`, `AuditEntryDto`), never an EF entity. Entities have navigation properties (could loop forever in JSON) and fields like password hashes that must never leave the server.

**Committee might ask:**
- *Q: Why no `[Authorize(Roles = ...)]`?* A: It trusts the role in the token, which can be up to 2 hours old. The services check the current role in the database on every call, so a role change or deactivation takes effect immediately.
- *Q: Why 201 and not 200 for create?* A: 201 means "created", and the `Location` header tells the client where the new ticket lives.
- *Q: Why 404 and not 403 for someone else's ticket?* A: 403 would confirm the ticket exists. 404 reveals nothing.

---

## [Controllers/TicketWorkflowController.cs](../../TicketingSystem.API/Controllers/TicketWorkflowController.cs)

**What it is:** The 10 workflow steps, each a `POST /api/tickets/{id}/<step>`. Each action is one line: pass the user id, the ticket id and the body (if any) to `ITicketWorkflowService`, return 200 with the updated ticket. All the rules are in the Application layer; see [03-workflow.md](03-workflow.md) for `TicketWorkflow` and `TicketWorkflowService.RunAsync`.

**The code:**

```csharp
using Microsoft.AspNetCore.Mvc;
using TicketingSystem.API.Extensions;
using TicketingSystem.Application.DTOs.Tickets;
using TicketingSystem.Application.Interfaces;

namespace TicketingSystem.API.Controllers
{
    // One POST per workflow step. Who may call each one is decided in TicketWorkflow,
    // not here, so the rules live in one place. Each returns the updated ticket.
    [ApiController]
    [Route("api/tickets/{id:guid}")]
    public class TicketWorkflowController(ITicketWorkflowService workflow) : ControllerBase
    {
        [HttpPost("route")]
        public async Task<ActionResult<TicketDetailsDto>> RouteToTeam(Guid id, RouteTicketRequest request, CancellationToken cancellationToken) =>
            Ok(await workflow.RouteAsync(User.GetUserId(), id, request, cancellationToken));

        [HttpPost("reject")]
        public async Task<ActionResult<TicketDetailsDto>> Reject(Guid id, CancellationToken cancellationToken) =>
            Ok(await workflow.RejectAsync(User.GetUserId(), id, cancellationToken));

        [HttpPost("assign")]
        public async Task<ActionResult<TicketDetailsDto>> Assign(Guid id, AssignMembersRequest request, CancellationToken cancellationToken) =>
            Ok(await workflow.AssignAsync(User.GetUserId(), id, request, cancellationToken));

        [HttpPost("submit")]
        public async Task<ActionResult<TicketDetailsDto>> Submit(Guid id, SubmitFixRequest request, CancellationToken cancellationToken) =>
            Ok(await workflow.SubmitAsync(User.GetUserId(), id, request, cancellationToken));

        [HttpPost("leader-approve")]
        public async Task<ActionResult<TicketDetailsDto>> LeaderApprove(Guid id, CancellationToken cancellationToken) =>
            Ok(await workflow.LeaderApproveAsync(User.GetUserId(), id, cancellationToken));

        [HttpPost("leader-send-back")]
        public async Task<ActionResult<TicketDetailsDto>> LeaderSendBack(Guid id, CancellationToken cancellationToken) =>
            Ok(await workflow.LeaderSendBackAsync(User.GetUserId(), id, cancellationToken));

        [HttpPost("manager-approve")]
        public async Task<ActionResult<TicketDetailsDto>> ManagerApprove(Guid id, CancellationToken cancellationToken) =>
            Ok(await workflow.ManagerApproveAsync(User.GetUserId(), id, cancellationToken));

        [HttpPost("manager-send-back")]
        public async Task<ActionResult<TicketDetailsDto>> ManagerSendBack(Guid id, CancellationToken cancellationToken) =>
            Ok(await workflow.ManagerSendBackAsync(User.GetUserId(), id, cancellationToken));

        [HttpPost("close")]
        public async Task<ActionResult<TicketDetailsDto>> Close(Guid id, CancellationToken cancellationToken) =>
            Ok(await workflow.CloseAsync(User.GetUserId(), id, cancellationToken));

        [HttpPost("reopen")]
        public async Task<ActionResult<TicketDetailsDto>> Reopen(Guid id, CancellationToken cancellationToken) =>
            Ok(await workflow.ReopenAsync(User.GetUserId(), id, cancellationToken));
    }
}
```

**Summary:**
- Line 11: the route prefix already contains `{id:guid}`, so every action gets `Guid id` from the URL, and a non-Guid id is a 404.
- Each action uses `=>` (expression body) because it is a single expression.
- Only three steps have a body: `route` (`RouteTicketRequest`: `teamId`), `assign` (`AssignMembersRequest`: `memberIds`), `submit` (`SubmitFixRequest`: branch, commit SHA, summary). `[ApiController]` validates them (400). The other seven need nothing but the URL.
- Line 15: the action is called `RouteToTeam`, not `Route`, because `Route` is also the name of the `[Route]` attribute class; the URL is still `/route`.
- No role checks here at all. `TicketWorkflow.Apply` decides who may do each step (403) and from which status (409). Putting them in the controller would split the rules across two places.

**Committee might ask:**
- *Q: Why one endpoint per step instead of `PUT /tickets/{id}` with a new status?* A: Each step has its own permission and its own side effects (assignments, snapshots, responses). A separate endpoint makes each one explicit, and the client can't jump to an arbitrary status.

---

## [Controllers/TeamsController.cs](../../TicketingSystem.API/Controllers/TeamsController.cs)

**What it is:** `GET /api/teams`, used by the React route picker (manager) and assign picker (leader).

**The code:**

```csharp
using Microsoft.AspNetCore.Mvc;
using TicketingSystem.API.Extensions;
using TicketingSystem.Application.DTOs.Teams;
using TicketingSystem.Application.Interfaces;

namespace TicketingSystem.API.Controllers
{
    [ApiController]
    [Route("api/teams")]
    public class TeamsController(ITeamService teamService) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TeamDto>>> List(CancellationToken cancellationToken)
        {
            var teams = await teamService.GetTeamsAsync(User.GetUserId(), cancellationToken);

            return Ok(teams);
        }
    }
}
```

**Line by line:**
- Line 15: the service decides who may see teams: TeamsManager and Admin see all teams, a TeamLeader sees only their own team, anyone else gets `ForbiddenException` (403). Inactive members are left out of the list.
- Line 17: 200 with a list of `TeamDto` (id, name, leader, members as id + name only).

**Committee might ask:**
- *Q: Why can't a client list teams?* A: They don't need it. Clients never pick a team; the manager routes tickets.

---

## [appsettings.json](../../TicketingSystem.API/appsettings.json), [appsettings.Development.json](../../TicketingSystem.API/appsettings.Development.json)

**What it is:** Configuration files. Both are tracked by git, so they contain no secrets.

**The code (appsettings.json):**

```json
{
  "ConnectionStrings": {
    "Default": "Server=(localdb)\\MSSQLLocalDB;Database=MizanDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  },
  "Jwt": {
    "Issuer": "Mizan.Api",
    "Audience": "Mizan.Web",
    "ExpiryMinutes": 120
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

**The code (appsettings.Development.json):**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**Line by line:**
- `ConnectionStrings:Default`: read by `AddInfrastructure`. LocalDB, database `MizanDb`, Windows login (`Trusted_Connection=True`), so there is no password in it. `TrustServerCertificate=True` accepts LocalDB's self-signed certificate. `\\` is just an escaped backslash in JSON.
- `Jwt`: issuer, audience and token lifetime (120 minutes). These are not secret; they are checked by `JwtOptions.Load` at startup and used both to create and to validate tokens. `Jwt:SigningKey` is deliberately missing here: it is set with `dotnet user-secrets` (stored outside the repo, linked by the `UserSecretsId` in the csproj) and merged into the same `Jwt` section at runtime, in Development.
- `Logging`: `Information` for our code, `Warning` for ASP.NET Core internals so the console is not flooded with per-request logs.
- `AllowedHosts: "*"`: accepts any host name. Fine for development; production would list the real domain.
- appsettings.Development.json: loaded on top of appsettings.json when the environment is Development. Note: it currently repeats the same logging values, so it changes nothing. It is the default file from the template.

**Committee might ask:**
- *Q: Where is the JWT signing key?* A: In user-secrets, never in these files, because they are committed to git. The app refuses to start without it.

---

## [Properties/launchSettings.json](../../TicketingSystem.API/Properties/launchSettings.json)

**What it is:** Run profiles used by Visual Studio and `dotnet run` on the developer machine. It is not used in production.

**The code:**

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5199",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "https://localhost:7005;http://localhost:5199",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

**Line by line:**
- `http` profile: listens on `http://localhost:5199` only. This is the port the Vite proxy in `mizan-web` forwards `/api` to.
- `https` profile: listens on `https://localhost:7005` and also `http://localhost:5199`, so the Vite proxy still works.
- `ASPNETCORE_ENVIRONMENT: Development`: this is what makes `app.Environment.IsDevelopment()` true, which turns on Swagger, the seeder and user-secrets, and turns off the https redirect.
- `launchBrowser: false`: no browser tab opens on start; Swagger is at `/swagger` when needed.

---

## [TicketingSystem.API.csproj](../../TicketingSystem.API/TicketingSystem.API.csproj)

**What it is:** The project file: target framework, packages and project references.

**The code:**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId>ddff6247-6dc2-47fe-9540-7dae968bcd8e</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.13" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.13" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.13">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Swashbuckle.AspNetCore.SwaggerUI" Version="10.3.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\TicketingSystem.Application\TicketingSystem.Application.csproj" />
    <ProjectReference Include="..\TicketingSystem.Infrastructure\TicketingSystem.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

**Line by line:**
- Line 1, `Microsoft.NET.Sdk.Web`: the web SDK, which includes ASP.NET Core and Kestrel.
- Line 5, `Nullable`: the compiler warns about possible null references (that's why some DTO fields are `Guid?` or `string?`).
- Line 6, `ImplicitUsings`: common namespaces (`System`, `Microsoft.AspNetCore.Http`, `Microsoft.Extensions.DependencyInjection`, ...) are imported automatically, which is why files like `AuthExtensions.cs` use `IServiceCollection` without a `using` for it.
- Line 7, `UserSecretsId`: links this project to its user-secrets file on the developer's machine (outside the repository). This id is not a secret; it is just a folder name.
- Line 11, `JwtBearer 10.0.13`: validates tokens.
- Line 12, `OpenApi 10.0.13`: generates the OpenAPI document (`AddOpenApi`, `MapOpenApi`).
- Lines 13-16, `EntityFrameworkCore.Design 10.0.13`: needed by `Add-Migration` / `Update-Database`, because the API is the startup project. `PrivateAssets=all` means it is used at design time only and is not passed on to other projects.
- Line 17, `Swashbuckle.AspNetCore.SwaggerUI 10.3.1`: only the UI page. It versions separately from .NET, which is why it is not 10.0.x.
- Lines 21-22: the API references Application (interfaces, DTOs) and Infrastructure (only so `AddInfrastructure` and the seeder can be called at startup). This is the composition root: the one place where all layers meet. Controllers still use only Application interfaces.

**Committee might ask:**
- *Q: The API references Infrastructure. Doesn't that break Clean Architecture?* A: Only the startup code (DI registration and seeding) uses it. Something has to wire the implementations to the interfaces, and that is the API's job. Controllers depend only on Application interfaces.

---

## One request, end to end

Trace: a Team Leader clicks "Approve" in React, which sends:

```
POST /api/tickets/3f2a...-.../leader-approve
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

In Development the browser sends it to Vite (`localhost:5173` by default), and the Vite proxy forwards it over plain http to `http://localhost:5199`.

1. **Kestrel** receives the bytes and creates an `HttpContext`. `HttpContext.RequestAborted` becomes the `CancellationToken` that will reach the database.
2. **Routing** (added automatically by `WebApplication`) matches the URL to `TicketWorkflowController.LeaderApprove`: `api/tickets/{id:guid}` + `leader-approve`, with `{id}` a valid Guid. If the id were not a Guid, no endpoint would match and the result would be a 404.
3. **`UseExceptionHandler`** does nothing yet; it just wraps the rest in a try/catch.
4. **`UseStatusCodePages`** also waits for the way back.
5. **`UseHttpsRedirection`** is skipped in Development (not in the pipeline).
6. **`UseAuthentication`** runs JwtBearer: it reads the header, checks the signature with our key, the algorithm (HS256 only), issuer `Mizan.Api`, audience `Mizan.Web`, and expiry (1 minute skew). Valid: `HttpContext.User` gets the claims `sub`, `email`, `role`. Invalid or missing: the user stays anonymous.
7. **`UseAuthorization`** sees the endpoint has `RequireAuthorization()` and no `[AllowAnonymous]`. Anonymous user: it stops here with **401** and an empty body plus a `WWW-Authenticate: Bearer` header; on the way back `UseStatusCodePages` fills in a ProblemDetails body (`"title": "Unauthorized"`). React's `api()` wrapper logs the user out.
8. **The controller.** `[ApiController]` binds `id` from the URL. There is no body to validate for this step. The action runs one line: `workflow.LeaderApproveAsync(User.GetUserId(), id, cancellationToken)`. `GetUserId()` reads `sub` from the token.
9. **The service** (details in [03-workflow.md](03-workflow.md)). `LeaderApproveAsync` calls `RunAsync`:
   - `GetActingUserAsync` loads the user from the database. Deactivated or deleted: `UnauthorizedException` (401).
   - `GetForUpdateAsync` loads the ticket with the visibility filter (a leader only sees tickets of their own team). Not found or not visible: `NotFoundException` (404).
   - `TicketWorkflow.Apply(user, ticket, LeaderApprove)`: first "who": must be a TeamLeader and the leader of this ticket's team, else `ForbiddenException` (403). In practice a 403 here means a user who can see the ticket but is not its leader, such as the TeamsManager; a leader of another team never gets this far (404 above). Then "when": status must be `LeaderReview`, else `ConflictException` (409). If both pass, status becomes `ManagerReview`.
   - An audit row is added (`LeaderApprove`, `LeaderReview` → `ManagerReview`).
   - `SaveChangesAsync` writes the status and the audit row in one transaction. If someone else saved this ticket in the meantime, the `RowVersion` check fails, and `TicketRepository` turns `DbUpdateConcurrencyException` into a `ConflictException` (409).
   - It returns `ticketService.GetByIdAsync(...)`: the fresh `TicketDetailsDto`, including the new `allowedActions` for this user.
10. **Back in the controller**, `Ok(dto)` produces 200, and `System.Text.Json` serializes the DTO with camelCase names.
11. **Back out through the middleware**: `UseStatusCodePages` sees a body and does nothing; `UseExceptionHandler` saw no exception. Kestrel sends the response.

**On success (200 OK)** the body is the updated ticket (values shortened):

```json
{
  "id": "3f2a...",
  "title": "Fix login null check",
  "description": "...",
  "repositoryUrl": "https://github.com/owner/repo",
  "status": "ManagerReview",
  "priority": "High",
  "createdAt": "2026-11-15T09:12:00Z",
  "closedAt": null,
  "client": { "id": "...", "name": "..." },
  "manager": { "id": "...", "name": "..." },
  "teamId": "...",
  "teamName": "Backend Team",
  "assignees": [ { "id": "...", "name": "..." } ],
  "snapshots": [ { "id": "...", "kind": "Before", "repositoryUrl": "...", "branch": "main", "commitSha": "...", "createdAt": "..." } ],
  "responses": [ { "id": "...", "summary": "...", "status": "Draft", "author": { "id": "...", "name": "..." }, "createdAt": "...", "sentAt": null } ],
  "allowedActions": []
}
```

`allowedActions` is now empty for the leader, because the next step belongs to the manager. React hides the buttons.

**On a 409** (for example the leader clicks Approve twice, so the second call finds the ticket already in `ManagerReview`): `TicketWorkflow.Apply` throws `ConflictException("Can't LeaderApprove a ticket that is ManagerReview.")`. Nothing is saved. The exception travels back up through the service and the controller to `UseExceptionHandler`, which calls `GlobalExceptionHandler`. It maps the exception to 409, does not log it (it's not a failure), and writes:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Can't LeaderApprove a ticket that is ManagerReview.",
  "status": 409,
  "traceId": "00-..."
}
```

with `Content-Type: application/problem+json`. React's `api()` wrapper throws an `ApiError` with that title, and the page can show it and reload the ticket.

Summary of where each "no" comes from in this one request:

| Status | Where | Why |
|---|---|---|
| 404 | Routing | id is not a Guid |
| 401 | JwtBearer + `UseAuthorization` | no token, bad signature, expired |
| 401 | `GetActingUserAsync` | account deactivated after login |
| 404 | `RunAsync` (visibility filter) | ticket missing, or not this leader's team |
| 403 | `TicketWorkflow.Apply` | caller can see the ticket but is not its team leader (for example the TeamsManager, who sees every ticket) |
| 409 | `TicketWorkflow.Apply` | ticket not in `LeaderReview` |
| 409 | `TicketRepository.SaveChangesAsync` | someone else saved the ticket at the same time |
| 500 | `GlobalExceptionHandler` | anything unexpected (logged, generic message) |
