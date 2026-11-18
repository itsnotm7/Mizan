# 2. Application: the rules

The Application layer holds the business rules of Mizan that are not about status changes: who may see which ticket, what a client is allowed to see, what goes in and out of the API, and which errors can happen. It references **only Domain**. Anything that needs a database or a token library is described here as an **interface**, and Infrastructure writes the real code behind it. This chapter covers everything in `TicketingSystem.Application` except the workflow (`TicketWorkflow.cs` and `TicketWorkflowService.cs`), which has its own chapter: [3. The workflow](03-workflow.md).

| File | One line |
|---|---|
| [TicketingSystem.Application.csproj](../../TicketingSystem.Application/TicketingSystem.Application.csproj) | Project file: references Domain only, plus the DI abstractions package. |
| [DependencyInjection.cs](../../TicketingSystem.Application/DependencyInjection.cs) | `AddApplication()`: registers the three Application services. |
| [DTOs/Auth/LoginRequest.cs](../../TicketingSystem.Application/DTOs/Auth/LoginRequest.cs) | What the login form sends, with validation attributes. |
| [DTOs/Auth/LoginResponse.cs](../../TicketingSystem.Application/DTOs/Auth/LoginResponse.cs) | What login returns: token, expiry, user. |
| [DTOs/Auth/UserDto.cs](../../TicketingSystem.Application/DTOs/Auth/UserDto.cs) | The safe public view of a user (no password hash). |
| [DTOs/Tickets/CreateTicketRequest.cs](../../TicketingSystem.Application/DTOs/Tickets/CreateTicketRequest.cs) | What a client sends to create a ticket, with validation attributes. |
| [DTOs/Tickets/WorkflowRequests.cs](../../TicketingSystem.Application/DTOs/Tickets/WorkflowRequests.cs) | Bodies for the route, assign and submit steps. |
| [DTOs/Tickets/TicketListItemDto.cs](../../TicketingSystem.Application/DTOs/Tickets/TicketListItemDto.cs) | One row in the ticket list. |
| [DTOs/Tickets/TicketDetailsDto.cs](../../TicketingSystem.Application/DTOs/Tickets/TicketDetailsDto.cs) | The full ticket page, plus small DTOs for users, snapshots, responses. |
| [DTOs/Tickets/AuditEntryDto.cs](../../TicketingSystem.Application/DTOs/Tickets/AuditEntryDto.cs) | One row in the ticket history. |
| [DTOs/Teams/TeamDto.cs](../../TicketingSystem.Application/DTOs/Teams/TeamDto.cs) | A team with its leader and members, for the pickers. |
| [Interfaces/IAuthService.cs](../../TicketingSystem.Application/Interfaces/IAuthService.cs) | Login and "who am I", implemented in Infrastructure. |
| [Interfaces/ITokenService.cs](../../TicketingSystem.Application/Interfaces/ITokenService.cs) | Creates a JWT for a user, implemented in Infrastructure. |
| [Interfaces/IUserRepository.cs](../../TicketingSystem.Application/Interfaces/IUserRepository.cs) | Reads users from the database. |
| [Interfaces/ITicketRepository.cs](../../TicketingSystem.Application/Interfaces/ITicketRepository.cs) | Reads and writes tickets, snapshots, responses, audit rows. |
| [Interfaces/ITeamRepository.cs](../../TicketingSystem.Application/Interfaces/ITeamRepository.cs) | Reads teams with their leader and members. |
| [Interfaces/ITicketService.cs](../../TicketingSystem.Application/Interfaces/ITicketService.cs) | Create, list, details, history: what the tickets controller calls. |
| [Interfaces/ITicketWorkflowService.cs](../../TicketingSystem.Application/Interfaces/ITicketWorkflowService.cs) | One method per workflow step (details in chapter 3). |
| [Interfaces/ITeamService.cs](../../TicketingSystem.Application/Interfaces/ITeamService.cs) | Lists teams for the current user. |
| [Exceptions/*.cs](../../TicketingSystem.Application/Exceptions/) | Five exceptions: Unauthorized, Forbidden, NotFound, Conflict, BadRequest. |
| [Services/ActingUserExtensions.cs](../../TicketingSystem.Application/Services/ActingUserExtensions.cs) | `GetActingUserAsync`: loads the caller fresh from the database. |
| [Services/TicketVisibility.cs](../../TicketingSystem.Application/Services/TicketVisibility.cs) | The ONE rule for who can see which ticket. |
| [Services/TicketService.cs](../../TicketingSystem.Application/Services/TicketService.cs) | Creates tickets, lists them, builds the details page and the history. |
| [Services/TeamService.cs](../../TicketingSystem.Application/Services/TeamService.cs) | Returns the teams a manager or leader may pick from. |

---

## [TicketingSystem.Application.csproj](../../TicketingSystem.Application/TicketingSystem.Application.csproj)

**What it is:** The project file. It says which .NET version the project targets, which NuGet packages it uses, and which other projects it references.

**The code:**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <!-- Only the registration interfaces for AddApplication(), not the ASP.NET Core framework -->
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.13" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\TicketingSystem.Domain\TicketingSystem.Domain.csproj" />
  </ItemGroup>

</Project>
```

**Line by line:**
- Line 1: `Microsoft.NET.Sdk` is the plain class-library SDK. Compare with the API project, which uses the Web SDK. Application is not a web project.
- Line 4: targets .NET 10.
- Line 5: `ImplicitUsings` adds common `using` lines (`System`, `System.Linq`, `System.Threading.Tasks`, ...) automatically. That is why many files here have no `using System.Linq;` but still call `.Where(...)`.
- Line 6: `Nullable` turns on nullable reference types. A `string?` may be null, a `string` should not be. The compiler warns when we might use a null by mistake. This is why the DTOs mark optional values with `?` (for example `string? TeamName`).
- Lines 9-12: one package, `Microsoft.Extensions.DependencyInjection.Abstractions`. It only contains the interfaces such as `IServiceCollection`, which `AddApplication()` needs. It does **not** bring in ASP.NET Core. The comment says this on purpose.
- Lines 14-16: the **only** project reference is Domain. Application cannot see Infrastructure (EF Core) or the API (HTTP). If someone tried to use `AppDbContext` in a service, the code would not compile. The architecture rule is enforced by the compiler, not just by discipline.

**Committee might ask:**
- *Q: How do you guarantee Application doesn't depend on EF Core?* A: The csproj has no reference to Infrastructure and no EF Core package. Using EF types here would be a compile error.
- *Q: Then how does Application read the database?* A: It defines interfaces like `ITicketRepository`. Infrastructure references Application and implements them. At runtime DI gives the service the real implementation. That is dependency inversion: the arrows point inward.

---

## [DependencyInjection.cs](../../TicketingSystem.Application/DependencyInjection.cs)

**What it is:** One method, `AddApplication()`, that registers the Application services in the DI container. `Program.cs` calls it with one line: `builder.Services.AddApplication();`.

**The code:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Application.Services;

namespace TicketingSystem.Application
{
    // Same idea as AddInfrastructure: Program.cs stays one line per layer
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<ITicketService, TicketService>();
            services.AddScoped<ITicketWorkflowService, TicketWorkflowService>();
            services.AddScoped<ITeamService, TeamService>();

            return services;
        }
    }
}
```

**Line by line:**
- Line 8: a **static class** is a class you never create with `new`. It only holds static methods. C# requires extension methods to live in a static class.
- Line 10: `this IServiceCollection services` makes this an **extension method**. The `this` keyword on the first parameter lets you call it as if it were a method of `IServiceCollection`: `builder.Services.AddApplication()` instead of `DependencyInjection.AddApplication(builder.Services)`.
- Line 12: `AddScoped<ITicketService, TicketService>()` means: "when someone asks for `ITicketService`, create a `TicketService`". **Scoped** means one instance per HTTP request. The services must be scoped because they use repositories, and the repositories use `AppDbContext`, which is scoped too (one database session per request).
- Line 13: same for the workflow service (chapter 3).
- Line 14: same for the team service.
- Line 16: returns the same collection so calls can be chained, which is the usual convention for `Add...` methods.

Notes:
- The repositories (`ITicketRepository`, `IUserRepository`, `ITeamRepository`), `IAuthService` and `ITokenService` are **not** registered here. Their implementations live in Infrastructure, so they are registered in `AddInfrastructure()` (see [chapter 4](04-infrastructure.md)).

**Committee might ask:**
- *Q: Why scoped and not singleton?* A: A singleton lives for the whole app. It would hold one `DbContext` shared by every request, which is not thread-safe. Scoped gives each request its own.
- *Q: Why an extension method?* A: Each layer registers its own classes, so `Program.cs` stays one line per layer and doesn't need to know every class.

---

## Auth DTOs: [LoginRequest.cs](../../TicketingSystem.Application/DTOs/Auth/LoginRequest.cs), [LoginResponse.cs](../../TicketingSystem.Application/DTOs/Auth/LoginResponse.cs), [UserDto.cs](../../TicketingSystem.Application/DTOs/Auth/UserDto.cs)

**What it is:** A **DTO** (Data Transfer Object) is a small class that only carries data in or out of the API. These three are what `POST /api/auth/login` receives and returns, and what `GET /api/auth/me` returns.

**The code:**

`LoginRequest.cs`:
```csharp
using System.ComponentModel.DataAnnotations;

namespace TicketingSystem.Application.DTOs.Auth
{
    // [ApiController] checks these attributes and returns a 400 before the action runs
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(128)] // Hashing is slow on purpose, so don't let huge passwords reach it
        public string Password { get; set; } = string.Empty;
    }
}
```

`LoginResponse.cs`:
```csharp
namespace TicketingSystem.Application.DTOs.Auth
{
    public record LoginResponse(string AccessToken, DateTime ExpiresAt, UserDto User);
}
```

`UserDto.cs`:
```csharp
namespace TicketingSystem.Application.DTOs.Auth
{
    // What the API shows about a user. PasswordHash is left out on purpose.
    public record UserDto(Guid Id, string Name, string Email, string Role);
}
```

**Line by line:**
- `LoginRequest` line 1: `System.ComponentModel.DataAnnotations` is where the validation attributes live.
- Line 5: **data annotations** are attributes like `[Required]` placed on properties. Because the controllers have `[ApiController]`, ASP.NET Core checks them automatically after reading the JSON body. If one fails, the action never runs and the client gets a 400 with a list of field errors.
- Line 6: request DTOs are normal classes with `{ get; set; }` so the JSON reader can fill them property by property.
- Lines 8-10: `[Required]` = must be present and not empty. `[EmailAddress]` = must look like an email. `[MaxLength(256)]` = at most 256 characters.
- Line 11: `= string.Empty` gives a default so the property is never null (matches `Nullable` being on).
- Lines 13-14: the password is limited to 128 characters. The comment explains why: password hashing is slow on purpose, so a 1 MB "password" would make the server do a lot of work for nothing.
- `LoginResponse` line 3: a **record** is a class meant for data. The short form `record X(A a, B b)` (a positional record) creates read-only properties, a constructor and value-based equality in one line. Perfect for output DTOs, which are built once and never changed.
- `LoginResponse` holds the JWT (`AccessToken`), when it expires (`ExpiresAt`), and the user, so the frontend knows the name and role without a second call.
- `UserDto` line 4: `Role` is a `string`, not the `UserRole` enum, so the JSON shows `"TeamLeader"` instead of a number. `PasswordHash` is not there at all (the comment says so).

**Committee might ask:**
- *Q: Why not return the `User` entity directly?* A: The entity has `PasswordHash`, `IsDeleted` and other internal fields. A DTO only contains what the client needs. If we add a field to the entity later, it doesn't leak to the API by accident.
- *Q: Why is Password limited to 128?* A: Hashing is deliberately slow. Without a limit, someone could send huge strings and keep the CPU busy.

---

## [CreateTicketRequest.cs](../../TicketingSystem.Application/DTOs/Tickets/CreateTicketRequest.cs)

**What it is:** The JSON body of `POST /api/tickets`. Its attributes are the first line of defence: bad input is refused with a 400 before any service code runs.

**The code:**

```csharp
using System.ComponentModel.DataAnnotations;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Application.DTOs.Tickets
{
    // The lengths match the column sizes in the EF configurations, so a value that
    // passes here can't fail at the database with a 500
    public class CreateTicketRequest
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(4000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        [RegularExpression(@"^https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/?$",
            ErrorMessage = "The repository link must look like https://github.com/owner/repo.")]
        public string RepositoryUrl { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Branch { get; set; } = string.Empty;

        // 7 is the shortest SHA git normally shows, 64 is a full SHA-256
        [Required]
        [RegularExpression("^[0-9a-fA-F]{7,64}$",
            ErrorMessage = "CommitSha must be 7 to 64 hexadecimal characters.")]
        public string CommitSha { get; set; } = string.Empty;

        // JSON accepts any number for an enum (even 99), so this checks the value really exists
        [EnumDataType(typeof(Priority))]
        public Priority Priority { get; set; } = Priority.Medium;
    }
}
```

**Line by line:**
- Lines 6-7: the `MaxLength` values are the same as the column sizes in the EF configurations. If they differed, a 300-character title could pass validation and then crash at the database with a 500. Matching them means the user gets a clear 400 instead.
- Lines 10-12: title required, at most 200 characters.
- Lines 14-16: description required, at most 4000.
- Lines 18-22: `[RegularExpression]` checks the text against a pattern. This one means: starts with `https://github.com/`, then an owner name, a `/`, a repo name, and an optional trailing `/`, then nothing else (`^` = start, `$` = end). Owner and repo may only use letters, digits, `_`, `.` and `-`. `ErrorMessage` replaces the default message, which would show the raw regex and confuse the user.
- Lines 24-26: branch required, at most 200.
- Lines 28-32: the commit SHA must be 7 to 64 hex characters (`0-9`, `a-f`, `A-F`). 7 is the short form git shows, 64 is a full SHA-256 hash (a SHA-1 is 40, which is in range too). There is no `MaxLength` here because `{7,64}` already limits the length.
- Lines 34-36: `[EnumDataType(typeof(Priority))]` checks that the value is a real `Priority` (Low, Medium, High, Critical). The comment explains why: JSON can send `99` and C# will happily put 99 into an enum variable. `Program.cs` adds a `JsonStringEnumConverter`, so the client can also send `"High"`. If `Priority` is missing, it defaults to `Medium`.
- Branch and CommitSha are not stored on the ticket. `TicketService.CreateAsync` uses them for the "Before" snapshot (see below).

**Committee might ask:**
- *Q: Where do these errors appear in the UI?* A: `[ApiController]` returns a 400 `ValidationProblemDetails` with an `errors` object per field. The React client reads it into `ApiError.fieldErrors` and shows each message under its field.
- *Q: Why validate here and not in the service?* A: Format checks (length, pattern) need no database, so the framework does them before the service runs. Checks that need the database (like "is this member in the team?") happen in the service and throw `BadRequestException`.

---

## [WorkflowRequests.cs](../../TicketingSystem.Application/DTOs/Tickets/WorkflowRequests.cs)

**What it is:** The three request bodies used by workflow steps: route (pick a team), assign (pick members), submit (the fix). The other seven steps have no body. How they are used is in [chapter 3](03-workflow.md).

**The code:**

```csharp
using System.ComponentModel.DataAnnotations;

namespace TicketingSystem.Application.DTOs.Tickets
{
    public class RouteTicketRequest
    {
        // Nullable so a missing teamId fails [Required] instead of arriving as an empty Guid
        [Required]
        public Guid? TeamId { get; set; }
    }

    public class AssignMembersRequest
    {
        // May be empty when continuing a reopened ticket with the members already assigned
        [Required]
        [MaxLength(20)]
        public List<Guid> MemberIds { get; set; } = [];
    }

    public class SubmitFixRequest
    {
        [Required]
        [MaxLength(200)]
        public string Branch { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^[0-9a-fA-F]{7,64}$",
            ErrorMessage = "CommitSha must be 7 to 64 hexadecimal characters.")]
        public string CommitSha { get; set; } = string.Empty;

        // What was fixed: this becomes the response the client receives once approved
        [Required]
        [MaxLength(4000)]
        public string Summary { get; set; } = string.Empty;
    }
}
```

**Line by line:**
- Lines 7-9: `Guid?` (nullable Guid). A plain `Guid` can never be null: if the client forgot `teamId`, it would silently become `00000000-...` and `[Required]` would pass. With `Guid?`, a missing value is `null`, so `[Required]` catches it and returns 400. The workflow service later reads it with `request.TeamId!.Value`.
- Lines 14-17: a list of member ids. `[MaxLength(20)]` on a list limits the **number of items** to 20. `= []` is a collection expression: an empty list.
- Note: the comment on line 14 says the list may be empty. `[Required]` on a list only fails when the list itself is `null` (for example `"memberIds": null`), not when it is empty, so an empty list passes. That matches the comment.
- Lines 22-34: the fix. Branch and CommitSha follow the same rules as in `CreateTicketRequest` (they become the "After" snapshot). `Summary` becomes the draft response that the client will see once the manager approves it.

**Committee might ask:**
- *Q: Why `Guid?` for a required field?* A: So that "missing" is detectable. A non-nullable Guid defaults to all zeros and would pass `[Required]`.

---

## Output ticket DTOs: [TicketListItemDto.cs](../../TicketingSystem.Application/DTOs/Tickets/TicketListItemDto.cs), [TicketDetailsDto.cs](../../TicketingSystem.Application/DTOs/Tickets/TicketDetailsDto.cs), [AuditEntryDto.cs](../../TicketingSystem.Application/DTOs/Tickets/AuditEntryDto.cs)

**What it is:** What the API returns for the ticket list, the ticket details page, and the history. All are records, built by `TicketService`.

**The code:**

`TicketListItemDto.cs`:
```csharp
namespace TicketingSystem.Application.DTOs.Tickets
{
    public record TicketListItemDto(
        Guid Id,
        string Title,
        string Status,
        string Priority,
        string ClientName,
        string? TeamName,
        DateTime CreatedAt);
}
```

`TicketDetailsDto.cs`:
```csharp
namespace TicketingSystem.Application.DTOs.Tickets
{
    public record TicketDetailsDto(
        Guid Id,
        string Title,
        string Description,
        string RepositoryUrl,
        string Status,
        string Priority,
        DateTime CreatedAt,
        DateTime? ClosedAt,
        TicketUserDto Client,
        TicketUserDto? Manager,
        Guid? TeamId,
        string? TeamName,
        IReadOnlyList<TicketUserDto> Assignees,
        IReadOnlyList<CodeSnapshotDto> Snapshots,
        IReadOnlyList<TicketResponseDto> Responses,
        IReadOnlyList<string> AllowedActions);

    // Only the id and name: a ticket screen has no reason to expose emails or roles
    public record TicketUserDto(Guid Id, string Name);

    public record CodeSnapshotDto(
        Guid Id,
        string Kind,
        string RepositoryUrl,
        string Branch,
        string CommitSha,
        DateTime CreatedAt);

    public record TicketResponseDto(
        Guid Id,
        string Summary,
        string Status,
        TicketUserDto Author,
        DateTime CreatedAt,
        DateTime? SentAt);
}
```

`AuditEntryDto.cs`:
```csharp
namespace TicketingSystem.Application.DTOs.Tickets
{
    public record AuditEntryDto(
        Guid Id,
        DateTime CreatedAt,
        string ActorName,
        string Action,
        string? OldValue,
        string? NewValue);
}
```

**Line by line:**
- `TicketListItemDto`: only what a list row shows. `Status` and `Priority` are strings (the enum name) so the JSON is readable. `ClientName` is flattened: the list shows a name, not a nested user object. `TeamName` is `string?` because a new ticket has no team until it is routed.
- `TicketDetailsDto` lines 3-19: everything the details page needs in one response, so the page makes one call.
  - `ClosedAt`, `Manager`, `TeamId`, `TeamName` are nullable: they are empty until the ticket reaches that stage.
  - `TeamId` is included (not just the name) so the frontend can load that team's members for the assign picker.
  - **IReadOnlyList** is a list you can read and loop over but not add to or remove from. Using it in the DTO says "this is a result, not something to edit".
  - `AllowedActions` (line 19) is the list of buttons this user may click right now, as strings like `"Assign"`. It comes from `TicketWorkflow.AllowedActions` ([chapter 3](03-workflow.md)), the same table the server uses to check the action, so the UI and the server can never disagree.
- Line 22: `TicketUserDto` has only `Id` and `Name`. The comment explains: the ticket screen has no reason to show emails or roles of other people.
- Lines 24-30: `CodeSnapshotDto`, one "Before" or "After" code state. `Kind` is `"Before"` or `"After"`.
- Lines 32-38: `TicketResponseDto`, the team's answer. `Status` is `Draft`, `Approved` or `Sent`. `SentAt` is null until the manager approves.
- Several records in one file: they are small and only used together, so they are kept next to `TicketDetailsDto`.
- `AuditEntryDto`: one history row. `ActorName` is a name, not an id, because the history screen shows "who". `OldValue`/`NewValue` are nullable: a "Created" row has no old value.

**Committee might ask:**
- *Q: Why are enums sent as strings?* A: `"InProgress"` is readable in the browser and in Swagger, and it doesn't break if someone reorders the enum.
- *Q: Why does the details DTO contain `AllowedActions`?* A: So the frontend doesn't copy the business rules. It just shows the buttons the server allows. If someone calls the endpoint anyway, the server checks again.

---

## [TeamDto.cs](../../TicketingSystem.Application/DTOs/Teams/TeamDto.cs)

**What it is:** A team with its leader and members, returned by `GET /api/teams`.

**The code:**

```csharp
using TicketingSystem.Application.DTOs.Tickets;

namespace TicketingSystem.Application.DTOs.Teams
{
    // Used by the manager to pick a team and by the leader to pick members
    public record TeamDto(Guid Id, string Name, TicketUserDto Leader, IReadOnlyList<TicketUserDto> Members);
}
```

**Line by line:**
- Line 1: reuses `TicketUserDto` (id + name) from the tickets DTOs instead of making a new user DTO.
- Line 5: the comment gives the two uses: the manager's "route to team" picker and the leader's "assign members" picker.
- Line 6: a positional record with the team id, name, leader and member list.

---

## Auth interfaces: [IAuthService.cs](../../TicketingSystem.Application/Interfaces/IAuthService.cs), [ITokenService.cs](../../TicketingSystem.Application/Interfaces/ITokenService.cs)

**What it is:** An **interface** is a contract: a list of methods with no body. A class that "implements" the interface must provide those methods. These two describe login and token creation. Their real code is in Infrastructure (`AuthService`, `JwtTokenService`).

**The code:**

`IAuthService.cs`:
```csharp
using TicketingSystem.Application.DTOs.Auth;

namespace TicketingSystem.Application.Interfaces
{
    public interface IAuthService
    {
        // Returns null when the login fails, without saying why:
        // the caller must not learn whether the email exists
        Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

        // Returns null when the user no longer exists or was deactivated
        Task<UserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
    }
}
```

`ITokenService.cs`:
```csharp
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Application.Interfaces
{
    public interface ITokenService
    {
        (string Token, DateTime ExpiresAt) CreateToken(User user);
    }
}
```

**Line by line:**
- `IAuthService` line 9: **`Task<T>` with async/await**: an async method returns a `Task`, which is a promise of a result later. While the database works, the thread is free to serve other requests. `await` waits for the result without blocking the thread.
- Same line: **CancellationToken** is a signal that says "stop, nobody needs this anymore" (for example the user closed the browser tab). We pass it down to EF so the database query can be cancelled too.
- Lines 7-9: `LoginResponse?` means it can return null. On a failed login it returns null without saying whether the email or the password was wrong. If the API said "unknown email", an attacker could use it to find which emails have accounts.
- Line 12: `GetCurrentUserAsync` is used by `GET /api/auth/me`. Null means the user was deleted or deactivated.
- `ITokenService` line 7: returns a **tuple**: two values in one, named `Token` and `ExpiresAt`. It takes the `User` entity, which is fine because this stays inside the server; the entity is never sent to the client.
- Note: the Infrastructure `AuthService` reads users through `AppDbContext` directly, not through `IUserRepository`. It is fully inside Infrastructure, so this doesn't break the dependency rule.

**Committee might ask:**
- *Q: Why is token creation an interface in Application?* A: Creating a JWT needs a crypto library and the signing key from configuration. Those are Infrastructure concerns. Application only says "I need a token for this user".

---

## [IUserRepository.cs](../../TicketingSystem.Application/Interfaces/IUserRepository.cs)

**What it is:** The contract for reading users. Implemented by `UserRepository` in Infrastructure.

**The code:**

```csharp
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);

        // Audit rows store plain ids (no foreign keys), so names are looked up separately
        Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);
    }
}
```

**Line by line:**
- Line 7: loads one user by id, or null. Used by `GetActingUserAsync`. The implementation does **not** filter out inactive or deleted users; `GetActingUserAsync` checks those itself.
- Line 10: takes many ids and returns a dictionary `id -> name`. The comment explains why it is needed: `AuditLog` has no navigation property to `User` (no foreign keys on purpose, see [chapter 1](01-domain.md)), so we can't `Include` the actor. Instead we collect the ids and fetch all names in one query. The implementation includes deleted users on purpose so old history still shows names.

---

## [ITicketRepository.cs](../../TicketingSystem.Application/Interfaces/ITicketRepository.cs)

**What it is:** The contract for everything stored about tickets. Implemented by `TicketRepository` in Infrastructure with EF Core.

**The code:**

```csharp
using System.Linq.Expressions;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Application.Interfaces
{
    // Application says what data it needs and Infrastructure decides how to get it,
    // so the business logic doesn't depend on EF Core
    public interface ITicketRepository
    {
        void Add(Ticket ticket);
        void AddAuditLog(AuditLog entry);

        // New rows for an existing ticket must be added explicitly. Our ids are set in C#
        // (Guid.NewGuid), so if they were only put in ticket.Assignments, EF would think they
        // already exist and send an UPDATE instead of an INSERT.
        void AddAssignment(TicketAssignment assignment);
        void AddSnapshot(CodeSnapshot snapshot);
        void AddResponse(TicketResponse response);

        // Throws ConflictException if someone else saved the same ticket in the meantime
        Task SaveChangesAsync(CancellationToken cancellationToken);

        // The filter is an Expression (not a Func) so EF can turn it into SQL
        // instead of loading every ticket and filtering in memory
        Task<IReadOnlyList<Ticket>> ListAsync(Expression<Func<Ticket, bool>> filter, CancellationToken cancellationToken);
        Task<Ticket?> GetDetailsAsync(Guid ticketId, Expression<Func<Ticket, bool>> filter, CancellationToken cancellationToken);
        Task<bool> IsVisibleAsync(Guid ticketId, Expression<Func<Ticket, bool>> filter, CancellationToken cancellationToken);

        // Tracked (unlike the reads above), so changes made to it are saved by SaveChangesAsync
        Task<Ticket?> GetForUpdateAsync(Guid ticketId, Expression<Func<Ticket, bool>> filter, CancellationToken cancellationToken);

        Task<IReadOnlyList<AuditLog>> GetAuditLogsAsync(Guid ticketId, CancellationToken cancellationToken);
    }
}
```

**Line by line:**
- Lines 6-7: the whole idea of the repository in one comment. The service says *what* it needs; Infrastructure decides *how* (EF Core, SQL).
- Lines 10-11: `Add` and `AddAuditLog` only mark rows to be inserted. Nothing is written until `SaveChangesAsync`. They return `void` because no database call happens yet.
- Lines 13-18: adding rows that belong to an **existing** (already loaded) ticket. The comment explains a real EF trap: our ids are created in C# (`Guid.NewGuid()` in `BaseEntity`), so a new row with an id already set, found only through a loaded ticket's collection, looks to EF like an existing row. EF would send an `UPDATE` that changes nothing. Adding it explicitly tells EF "this is new, INSERT it". These three are used by the workflow ([chapter 3](03-workflow.md)).
- Line 21: saves everything marked so far in **one transaction**. The implementation catches EF's `DbUpdateConcurrencyException` (the ticket's `RowVersion` changed since it was loaded) and throws our `ConflictException`, so the API never needs to know about EF.
- Lines 23-27: the three read methods take a `filter` of type `Expression<Func<Ticket, bool>>`. This is where `TicketVisibility.VisibleTo(user)` is passed in (explained in the TicketVisibility section). The comment says why it is an `Expression` and not a plain `Func`: EF can read an expression and turn it into SQL `WHERE`, so the database does the filtering.
  - `ListAsync`: tickets for the list, with Client and Team loaded, deleted ones excluded, newest first.
  - `GetDetailsAsync`: one ticket with everything the details page needs (client, manager, team, non-deleted assignments with users, snapshots, responses with authors), or null.
  - `IsVisibleAsync`: just "does this ticket exist and can this user see it?" (`AnyAsync`, a cheap `EXISTS` query). Used by the history endpoint.
- Lines 29-30: `GetForUpdateAsync` returns a **tracked** ticket: EF remembers it, so when the workflow changes `ticket.Status`, `SaveChangesAsync` writes the change. The read methods above use `AsNoTracking()`, which is faster but not saveable.
- Line 32: audit rows for one ticket, oldest first.

**Committee might ask:**
- *Q: Why a repository and not the DbContext directly in the service?* A: The DbContext is an EF Core class. Using it in Application would make Application depend on EF and break the inward arrows. The interface keeps business logic independent of the data access technology.
- *Q: Why do read methods need the visibility filter as a parameter?* A: So "can this user see this ticket?" is part of the same SQL query that loads it. There's no second step where someone could forget to check.

---

## [ITeamRepository.cs](../../TicketingSystem.Application/Interfaces/ITeamRepository.cs)

**What it is:** The contract for reading teams. Implemented by `TeamRepository` in Infrastructure.

**The code:**

```csharp
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Application.Interfaces
{
    public interface ITeamRepository
    {
        // Both load the leader and the members with their users
        Task<IReadOnlyList<Team>> ListWithMembersAsync(CancellationToken cancellationToken);
        Task<Team?> GetWithMembersAsync(Guid teamId, CancellationToken cancellationToken);
    }
}
```

**Line by line:**
- Line 8: all non-deleted teams (sorted by name in the implementation), with `Leader`, `Members` and each member's `User` loaded. Used by `TeamService`.
- Line 9: one team with the same data, or null. Used by the workflow to check that a team exists (route) and that the chosen members belong to it (assign). See [chapter 3](03-workflow.md).

---

## Service interfaces: [ITicketService.cs](../../TicketingSystem.Application/Interfaces/ITicketService.cs), [ITicketWorkflowService.cs](../../TicketingSystem.Application/Interfaces/ITicketWorkflowService.cs), [ITeamService.cs](../../TicketingSystem.Application/Interfaces/ITeamService.cs)

**What it is:** The contracts the controllers call. Controllers depend on these interfaces, not on the classes.

**The code:**

`ITicketService.cs`:
```csharp
using TicketingSystem.Application.DTOs.Tickets;

namespace TicketingSystem.Application.Interfaces
{
    // Each method takes only the caller's id: the service loads the user from the database,
    // so the role and active status are never taken from a possibly old token
    public interface ITicketService
    {
        Task<TicketDetailsDto> CreateAsync(Guid userId, CreateTicketRequest request, CancellationToken cancellationToken);
        Task<IReadOnlyList<TicketListItemDto>> GetVisibleTicketsAsync(Guid userId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> GetByIdAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<IReadOnlyList<AuditEntryDto>> GetHistoryAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
    }
}
```

`ITicketWorkflowService.cs`:
```csharp
using TicketingSystem.Application.DTOs.Tickets;

namespace TicketingSystem.Application.Interfaces
{
    // One method per workflow step. Each returns the updated ticket.
    public interface ITicketWorkflowService
    {
        Task<TicketDetailsDto> RouteAsync(Guid userId, Guid ticketId, RouteTicketRequest request, CancellationToken cancellationToken);
        Task<TicketDetailsDto> RejectAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> AssignAsync(Guid userId, Guid ticketId, AssignMembersRequest request, CancellationToken cancellationToken);
        Task<TicketDetailsDto> SubmitAsync(Guid userId, Guid ticketId, SubmitFixRequest request, CancellationToken cancellationToken);
        Task<TicketDetailsDto> LeaderApproveAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> LeaderSendBackAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> ManagerApproveAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> ManagerSendBackAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> CloseAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
        Task<TicketDetailsDto> ReopenAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken);
    }
}
```

`ITeamService.cs`:
```csharp
using TicketingSystem.Application.DTOs.Teams;

namespace TicketingSystem.Application.Interfaces
{
    public interface ITeamService
    {
        Task<IReadOnlyList<TeamDto>> GetTeamsAsync(Guid userId, CancellationToken cancellationToken);
    }
}
```

**Line by line:**
- `ITicketService` lines 5-6: the most important design comment here. Every method takes only `Guid userId`, never a role or a `User` object from the controller. The service loads the user from the database itself, so the role and "is active" are always current. (See `GetActingUserAsync` below.)
- Lines 9-12: create (returns the new ticket's details), list, details, history. Every method returns DTOs, never entities.
- `ITicketWorkflowService`: one method per step (10 steps = 10 methods). Each returns the updated `TicketDetailsDto`, so after clicking a button the frontend has the new status and new `AllowedActions` without another call. Explained in [chapter 3](03-workflow.md).
- `ITeamService`: one method, teams for the current user.

**Committee might ask:**
- *Q: Why does the controller pass only the user id?* A: The id comes from the JWT's `sub` claim and never changes. The role could change after login, and the account could be deactivated. Passing only the id forces the service to read the current state.

---

## Exceptions: [UnauthorizedException](../../TicketingSystem.Application/Exceptions/UnauthorizedException.cs), [ForbiddenException](../../TicketingSystem.Application/Exceptions/ForbiddenException.cs), [NotFoundException](../../TicketingSystem.Application/Exceptions/NotFoundException.cs), [ConflictException](../../TicketingSystem.Application/Exceptions/ConflictException.cs), [BadRequestException](../../TicketingSystem.Application/Exceptions/BadRequestException.cs)

**What it is:** Five tiny exception classes. A service throws one when a request must be refused. The API turns each into an HTTP status code.

**The code:**

`UnauthorizedException.cs`:
```csharp
namespace TicketingSystem.Application.Exceptions
{
    // Becomes a 401 in GlobalExceptionHandler: the token is valid but the account no longer is
    public class UnauthorizedException(string message) : Exception(message);
}
```

`ForbiddenException.cs`:
```csharp
namespace TicketingSystem.Application.Exceptions
{
    // Becomes a 403 in GlobalExceptionHandler: we know who you are, but you may not do this
    public class ForbiddenException(string message) : Exception(message);
}
```

`NotFoundException.cs`:
```csharp
namespace TicketingSystem.Application.Exceptions
{
    // Becomes a 404 in GlobalExceptionHandler
    public class NotFoundException(string message) : Exception(message);
}
```

`ConflictException.cs`:
```csharp
namespace TicketingSystem.Application.Exceptions
{
    // Becomes a 409 in GlobalExceptionHandler: the request is fine, but the ticket's current
    // state doesn't allow it (wrong status, or someone else saved it at the same moment)
    public class ConflictException(string message) : Exception(message);
}
```

`BadRequestException.cs`:
```csharp
namespace TicketingSystem.Application.Exceptions
{
    // Becomes a 400 in GlobalExceptionHandler: input that passes the DTO checks but is still
    // wrong once compared with the database (e.g. a member who isn't in the ticket's team)
    public class BadRequestException(string message) : Exception(message);
}
```

**Line by line:**
- `public class UnauthorizedException(string message) : Exception(message);` uses a **primary constructor**: the parameters are written right after the class name, and `: Exception(message)` passes the message to the base `Exception` class. The `;` at the end means the class has no body. It is the short way to write a class whose only job is to have its own name.
- Each class has a different **name**, and the name is what matters. `GlobalExceptionHandler` in the API (see [chapter 5](05-api.md)) uses a switch on the exception type: `UnauthorizedException` -> 401, `ForbiddenException` -> 403, `NotFoundException` -> 404, `ConflictException` -> 409, `BadRequestException` -> 400, anything else -> 500 with a generic message.
- The comments explain when each one is used:
  - **401**: the token is valid, but the account is now inactive or deleted (thrown by `GetActingUserAsync`).
  - **403**: we know who you are, but your role can't do this (for example a leader calling create ticket).
  - **404**: the ticket doesn't exist **or** you can't see it.
  - **409**: the ticket's state doesn't allow this step, or someone else saved it at the same moment.
  - **400**: the input looked fine, but is wrong compared with the database.
- Why no status codes inside the exceptions: Application doesn't know about HTTP. It has no reference to ASP.NET Core. The same services could be called from a console app or a background job, where "403" means nothing. The mapping to HTTP is the API's job. The exception messages are written to be safe to show to the user, and the handler shows them as the ProblemDetails `title`.

**Committee might ask:**
- *Q: Why not return `Forbid()` or `NotFound()` from the service?* A: Those are ASP.NET Core results. The service can't reference ASP.NET Core. It throws a plain exception, and the API maps it in one place.
- *Q: Aren't exceptions slow for normal flow?* A: They cost a little, but refusals are rare compared to normal requests, and it keeps every service simple: check, throw, done. One handler gives every error the same ProblemDetails shape.
- *Q: 401 vs 403?* A: 401 = we don't accept who you are (no valid login, or the account is no longer active). 403 = we know who you are, but you're not allowed.

---

## [ActingUserExtensions.cs](../../TicketingSystem.Application/Services/ActingUserExtensions.cs)

**What it is:** One extension method, `GetActingUserAsync`, that loads the caller from the database and refuses inactive or deleted accounts. Every ticket, workflow and team service method starts with it.

**The code:**

```csharp
using TicketingSystem.Application.Exceptions;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Domain.Entities;

namespace TicketingSystem.Application.Services
{
    public static class ActingUserExtensions
    {
        // JwtBearer only checks the token, so a deactivated user's token still passes it.
        // Reading the user here means the role and status are always the current ones.
        // Every ticket, workflow and team service starts with this.
        public static async Task<User> GetActingUserAsync(this IUserRepository users, Guid userId, CancellationToken cancellationToken)
        {
            var user = await users.GetByIdAsync(userId, cancellationToken);

            if (user is null || user.IsDeleted || !user.IsActive)
                throw new UnauthorizedException("This account is no longer active.");

            return user;
        }
    }
}
```

**Line by line:**
- Line 7: a static class, because it holds an extension method.
- Lines 9-11: the reason this exists. JwtBearer only checks the token's signature and expiry. A token lives for 120 minutes by default (`JwtOptions.ExpiryMinutes`). If an admin deactivates a user or changes their role during that time, the old token still says the old thing. Reading the user from the database on every call means the current role and status always win.
- Line 12: `this IUserRepository users` makes it an extension method on the repository interface, so services write `users.GetActingUserAsync(userId, ct)`, which reads naturally. `async Task<User>` means it is asynchronous and gives back a `User`.
- Line 14: `await` the database read. `GetByIdAsync` returns null if no user has that id.
- Line 16: three reasons to refuse: no such user, soft-deleted, or deactivated. `user is null` is pattern matching for "is null". The `||` stops at the first true condition, so `user.IsDeleted` is never read when `user` is null.
- Line 17: throws `UnauthorizedException`, which becomes a 401. The frontend logs the user out on any 401.
- Line 19: returns the fresh `User` entity. Services use its `Role` and `Id` for every decision.

**Committee might ask:**
- *Q: Isn't one extra database query per request expensive?* A: It's a single lookup by primary key, which is very fast. In exchange, deactivation and role changes apply immediately instead of up to 2 hours later.
- *Q: Why not `[Authorize(Roles = "Client")]` on the controller?* A: That reads the role from the token, which can be old. The comment at the top of `TicketsController` says exactly this.
- *Q: Why an extension method and not a base class?* A: Three services need it. An extension on `IUserRepository` shares it without forcing an inheritance hierarchy.

---

## [TicketVisibility.cs](../../TicketingSystem.Application/Services/TicketVisibility.cs)

**What it is:** The **one place** that decides who can see which ticket. It returns a filter that EF turns into SQL. The list, the details, the history and every workflow step all use it.

**The code:**

```csharp
using System.Linq.Expressions;
using TicketingSystem.Domain.Entities;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Application.Services
{
    // The one place that decides who can see which ticket. The list and details endpoints both use it.
    public static class TicketVisibility
    {
        public static Expression<Func<Ticket, bool>> VisibleTo(User user)
        {
            // Capture just the id, so EF sends one simple SQL parameter
            var userId = user.Id;

            return user.Role switch
            {
                UserRole.Client => t => t.ClientId == userId,
                UserRole.TeamsManager or UserRole.Admin => t => true,
                // No team until the manager routes it, so a new ticket isn't visible to any leader yet
                UserRole.TeamLeader => t => t.Team != null && t.Team.LeaderId == userId,
                // Being on the team isn't enough: the member must be assigned to this ticket
                UserRole.TeamMember => t => t.Assignments.Any(a => a.UserId == userId && !a.IsDeleted),
                // A role added later sees nothing until someone writes its rule
                _ => t => false
            };
        }
    }
}
```

**First, three concepts:**
- A **lambda** is a small function written inline: `t => t.ClientId == userId` means "given a ticket `t`, return whether its ClientId equals userId".
- `Func<Ticket, bool>` is the type of such a function: takes a `Ticket`, returns a `bool`. If you pass a `Func` to a query, C# can only *run* it, so EF would have to load every ticket into memory and test them one by one.
- **`Expression<Func<Ticket, bool>>`** is the same lambda, but stored as a **description** (a small tree of "property ClientId", "equals", "variable userId") instead of compiled code. The C# compiler builds this tree automatically when the target type is `Expression<...>`. EF Core reads the tree and translates it into SQL, for example `WHERE [t].[ClientId] = @userId`. So the database filters, and only the visible tickets ever leave SQL Server.

**Line by line:**
- Line 8: static class: no state, just a rule.
- Line 10: takes the (fresh) `User`, returns the filter.
- Line 13: copies the id into a local variable. The comment explains why: the lambdas then capture a single `Guid`, which EF sends as one SQL parameter. If the lambda used `user.Id`, it would capture the whole `User` object.
- Line 15: a **switch expression**: `value switch { pattern => result, ... }` picks one result based on the value. Here each result is a different lambda.
- Line 17: **Client**: only their own tickets. SQL: `WHERE ClientId = @userId`.
- Line 18: **TeamsManager or Admin**: everything (`t => true`). `or` combines two patterns in one arm. The manager must see every ticket to route new ones.
- Lines 19-20: **TeamLeader**: tickets routed to a team they lead. `t.Team != null` is needed because a new ticket has no team yet. EF turns `t.Team.LeaderId` into a join with the Teams table.
- Lines 21-22: **TeamMember**: only tickets where they have a non-deleted assignment. `Any(...)` is LINQ for "is there at least one item that matches?". EF turns it into `EXISTS (SELECT ... FROM TicketAssignments WHERE ...)`. The comment makes the point: being on the team isn't enough.
- Lines 23-24: `_` is the "anything else" arm. A new role added to the enum later sees nothing until someone writes its rule. This is a **secure default** (deny unless allowed).

**How it is used:** the result is always passed to a repository method, which adds it to its own query with `.Where(filter)`. In `TicketRepository.GetDetailsAsync` the query becomes "this id AND not deleted AND visible to this user", all in one SQL statement.

Note: the TeamLeader rule does not check `Team.IsDeleted`. Teams are seeded and there's no screen to delete them in the MVP.

**Committee might ask:**
- *Q: Why an `Expression` and not a `Func`?* A: A `Func` can only run in C#, so EF would load every ticket and filter in memory. An `Expression` is data that EF translates to SQL, so the database filters and returns only visible rows.
- *Q: Why one class for this?* A: If the list, details, history and workflow each had their own check, one day they'd disagree, and a user might see a ticket in the list but get an error opening it, or worse, the opposite. One rule, used everywhere.
- *Q: Can a team member see all their team's tickets?* A: No, only tickets they are assigned to. A leader sees all tickets of the teams they lead.

---

## [TicketService.cs](../../TicketingSystem.Application/Services/TicketService.cs)

**What it is:** The service behind `TicketsController`. It creates tickets, lists them, builds the details page and the history, and hides team-internal data from clients. It is long, so it is shown in eight parts.

### Part 1: class header and `ClientVisibleActions` (lines 1-22)

```csharp
using TicketingSystem.Application.DTOs.Tickets;
using TicketingSystem.Application.Exceptions;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Domain.Entities;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Application.Services
{
    public class TicketService(ITicketRepository tickets, IUserRepository users) : ITicketService
    {
        public const string CreatedAction = "Created";

        // Clients see the outcome of their ticket, not the team's internal back-and-forth
        private static readonly HashSet<string> ClientVisibleActions =
        [
            CreatedAction,
            nameof(WorkflowAction.Route),
            nameof(WorkflowAction.Reject),
            nameof(WorkflowAction.ManagerApprove),
            nameof(WorkflowAction.Close),
            nameof(WorkflowAction.Reopen)
        ];
```

- Line 9: a **primary constructor** again: `TicketService(ITicketRepository tickets, IUserRepository users)`. DI sees these parameters and passes in the registered implementations. The parameters `tickets` and `users` can be used anywhere in the class, so we don't need to write fields and a constructor by hand. `: ITicketService` means this class implements that interface.
- Line 11: the audit action name for a new ticket. `const` = a value fixed at compile time.
- Lines 13-22: the audit actions a **client** may see in the history. A **HashSet** is a collection with no duplicates that answers "is X in here?" very fast. `static readonly` means one shared copy for the whole app, set once. `[ ... ]` is a collection expression that fills it.
- `nameof(WorkflowAction.Route)` gives the string `"Route"`. Using `nameof` instead of typing `"Route"` means a rename of the enum value would be caught by the compiler. It works because the workflow writes `action.ToString()` as the audit action (see [chapter 3](03-workflow.md)), which is the same text.
- What a client does **not** see: `Assign`, `Submit`, `LeaderApprove`, `LeaderSendBack`, `ManagerSendBack`, and the detail rows `TeamAssigned` and `MemberAssigned` that the workflow also writes. Those are the team's internal back-and-forth (the comment on line 13).

Note: `CreatedAction` is `public`, but in the current code it is only used inside this class.

### Part 2: `CreateAsync` (lines 24-64)

```csharp
        public async Task<TicketDetailsDto> CreateAsync(Guid userId, CreateTicketRequest request, CancellationToken cancellationToken)
        {
            var user = await users.GetActingUserAsync(userId, cancellationToken);

            // The role comes from the database, so a role changed after login applies right away.
            // 403, not 404: there's nothing secret here, everyone knows this endpoint exists.
            if (user.Role != UserRole.Client)
                throw new ForbiddenException("Only clients can create tickets.");

            var ticket = new Ticket
            {
                Title = request.Title,
                Description = request.Description,
                RepositoryUrl = request.RepositoryUrl,
                Priority = request.Priority,
                ClientId = user.Id
            };

            // Added through the ticket, so one SaveChanges writes both rows in one transaction:
            // a ticket can never exist without its "Before" snapshot
            ticket.Snapshots.Add(new CodeSnapshot
            {
                Kind = SnapshotKind.Before,
                RepositoryUrl = request.RepositoryUrl,
                Branch = request.Branch,
                CommitSha = request.CommitSha,
                SubmittedById = user.Id
            });

            tickets.Add(ticket);
            tickets.AddAuditLog(new AuditLog
            {
                TicketId = ticket.Id,
                ActorId = user.Id,
                Action = CreatedAction,
                NewValue = ticket.Status.ToString()
            });
            await tickets.SaveChangesAsync(cancellationToken);

            return await GetDetailsDtoAsync(user, ticket.Id, cancellationToken);
        }
```

- Line 26: load the caller fresh (401 if inactive).
- Lines 28-31: only clients may create tickets. The role is the one in the database. It throws **403, not 404**: the comment explains that there's nothing secret about this endpoint, so "you're not allowed" is the honest answer. (Compare with tickets, where 404 hides whether the ticket exists.)
- Lines 33-40: build the `Ticket` entity from the DTO. This is manual mapping: we copy only the fields a client may set. The client can't set `Status`, `TeamId` or `ManagerId`, because those fields are not even in `CreateTicketRequest`. `ClientId` comes from the database user, not from the request. `Id`, `CreatedAt` and `Status = Submitted` come from the defaults in `BaseEntity` and `Ticket`.
- Lines 42-51: the "Before" snapshot, from the request's repo, branch and commit. It is added to `ticket.Snapshots`. Because the ticket itself is new, EF inserts its children too, in the same `SaveChanges`, in one transaction. The comment states the guarantee: a ticket can never exist without its Before snapshot. (This is different from the trap in `ITicketRepository` lines 13-15, which is about adding rows to an **existing**, already-loaded ticket.)
- Line 53: mark the ticket for insert.
- Lines 54-60: the audit row. `ticket.Id` is already known because ids are generated in C# (`Guid.NewGuid()` in `BaseEntity`), not by the database. `NewValue` is `"Submitted"`. `OldValue` stays null because there was no previous status.
- Line 61: one `SaveChangesAsync` writes the ticket, the snapshot and the audit row together, or nothing.
- Line 63: reloads the ticket through `GetDetailsDtoAsync` and returns it. This is needed because the in-memory `ticket` only has `ClientId`, not the `Client` navigation object, and the DTO needs the client's name. It also produces the correct `AllowedActions` for the new ticket. The controller returns it with `201 Created`.

### Part 3: `GetVisibleTicketsAsync` and `GetByIdAsync` (lines 66-79)

```csharp
        public async Task<IReadOnlyList<TicketListItemDto>> GetVisibleTicketsAsync(Guid userId, CancellationToken cancellationToken)
        {
            var user = await users.GetActingUserAsync(userId, cancellationToken);
            var visibleTickets = await tickets.ListAsync(TicketVisibility.VisibleTo(user), cancellationToken);

            return visibleTickets.Select(ToListItemDto).ToList();
        }

        public async Task<TicketDetailsDto> GetByIdAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken)
        {
            var user = await users.GetActingUserAsync(userId, cancellationToken);

            return await GetDetailsDtoAsync(user, ticketId, cancellationToken);
        }
```

- Line 68: fresh user.
- Line 69: build the visibility filter for this user and pass it to the repository. The SQL returns only the tickets this user may see (newest first, deleted ones excluded, with Client and Team loaded).
- Line 71: **LINQ** is the set of methods like `Where`, `Select`, `Any`, `OrderBy`, `ToList` for working with collections. `Select(ToListItemDto)` turns each `Ticket` into a `TicketListItemDto`. Passing the method name `ToListItemDto` is a shortcut for `t => ToListItemDto(t)`. `ToList()` runs it and makes a list. This `Select` runs in memory, on tickets already loaded.
- Lines 74-79: `GetByIdAsync` loads the user and delegates to `GetDetailsDtoAsync`. The workflow service also calls this method at the end of every step, so a workflow response is built exactly like a normal details response.

### Part 4: `GetHistoryAsync` (lines 81-104)

```csharp
        public async Task<IReadOnlyList<AuditEntryDto>> GetHistoryAsync(Guid userId, Guid ticketId, CancellationToken cancellationToken)
        {
            var user = await users.GetActingUserAsync(userId, cancellationToken);

            // Same rule as the details page: no history for a ticket you can't see
            if (!await tickets.IsVisibleAsync(ticketId, TicketVisibility.VisibleTo(user), cancellationToken))
                throw new NotFoundException("Ticket not found.");

            var entries = await tickets.GetAuditLogsAsync(ticketId, cancellationToken);
            if (user.Role == UserRole.Client)
                entries = entries.Where(e => ClientVisibleActions.Contains(e.Action)).ToList();

            var names = await users.GetNamesAsync(entries.Select(e => e.ActorId).Distinct(), cancellationToken);

            return entries
                .Select(e => new AuditEntryDto(
                    e.Id,
                    e.CreatedAt,
                    names.GetValueOrDefault(e.ActorId, "Unknown user"), // The log outlives users on purpose
                    e.Action,
                    e.OldValue,
                    e.NewValue))
                .ToList();
        }
```

- Line 83: fresh user.
- Lines 85-87: the audit log has no foreign key to the ticket, so we can't filter it through the ticket. Instead we first ask "can this user see this ticket?" with the same `TicketVisibility` rule. If not, 404. Same answer as the details page, so the history can't be used to probe ticket ids.
- Line 89: all audit rows of this ticket, oldest first.
- Lines 90-91: **clients only** get the rows whose `Action` is in `ClientVisibleActions`. `Where` keeps the items that match the condition. `Contains` on a `HashSet` is a fast lookup. The result is a `List`, which is also an `IReadOnlyList`, so it can be assigned back to `entries`.
- Line 93: collect the actor ids (`Select`), remove duplicates (`Distinct`), and load all names in one query. This avoids one query per row.
- Lines 95-103: build one DTO per row. `GetValueOrDefault(e.ActorId, "Unknown user")` returns the name, or `"Unknown user"` if the id isn't in the dictionary. The comment explains: the log is designed to outlive users, so a missing user must not crash the history.

### Part 5: `GetDetailsDtoAsync` and `ToListItemDto` (lines 106-123)

```csharp
        private async Task<TicketDetailsDto> GetDetailsDtoAsync(User user, Guid ticketId, CancellationToken cancellationToken)
        {
            var ticket = await tickets.GetDetailsAsync(ticketId, TicketVisibility.VisibleTo(user), cancellationToken);

            // Same answer for "doesn't exist" and "not yours", so nobody can probe which ticket ids exist
            return ticket is null
                ? throw new NotFoundException("Ticket not found.")
                : ToDetailsDto(ticket, user);
        }

        private static TicketListItemDto ToListItemDto(Ticket ticket) => new(
            ticket.Id,
            ticket.Title,
            ticket.Status.ToString(),
            ticket.Priority.ToString(),
            ticket.Client.Name,
            ticket.Team?.Name,
            ticket.CreatedAt);
```

- Line 106: `private`, shared by `CreateAsync` and `GetByIdAsync`.
- Line 108: one query loads the ticket **only if** it exists, is not deleted, and is visible to this user. The visibility filter is part of the SQL `WHERE`.
- Lines 110-113: if null, throw `NotFoundException` (404). The comment is the key security idea: **404 for both "doesn't exist" and "not yours"**. If we returned 403 for "not yours", anyone could try random ids and learn which tickets exist. `condition ? a : b` is the conditional operator; `throw` can be used inside it (a throw expression).
- Line 116: `=> new(...)` is an expression-bodied method: the method is just one expression. `new(...)` without a type name creates the return type (`TicketListItemDto`), because C# knows it from the method signature.
- Lines 119-120: enums become strings with `ToString()`.
- Line 121: `ticket.Client.Name` is safe because `ListAsync` includes `Client`.
- Line 122: **`?.`** is the null-conditional operator: if `ticket.Team` is null, the whole thing is null instead of crashing. A new ticket has no team.

### Part 6: `ToDetailsDto` (lines 125-149)

```csharp
        private static TicketDetailsDto ToDetailsDto(Ticket ticket, User user)
        {
            // A client only sees the response once the manager has sent it, never the team's drafts
            var responses = user.Role == UserRole.Client
                ? ticket.Responses.Where(r => r.Status == ResponseStatus.Sent)
                : ticket.Responses;

            return new TicketDetailsDto(
                ticket.Id,
                ticket.Title,
                ticket.Description,
                ticket.RepositoryUrl,
                ticket.Status.ToString(),
                ticket.Priority.ToString(),
                ticket.CreatedAt,
                ticket.ClosedAt,
                ToUserDto(ticket.Client),
                ticket.Manager is null ? null : ToUserDto(ticket.Manager),
                ticket.TeamId,
                ticket.Team?.Name,
                ticket.Assignments.Select(a => ToUserDto(a.User)).ToList(),
                SnapshotsVisibleTo(ticket, user).OrderBy(s => s.CreatedAt).Select(ToSnapshotDto).ToList(),
                responses.OrderBy(r => r.CreatedAt).Select(ToResponseDto).ToList(),
                TicketWorkflow.AllowedActions(user, ticket).Select(a => a.ToString()).ToList());
        }
```

- Line 125: `static` because it uses only its parameters, no instance data.
- Lines 127-130: **hide drafts from clients.** A client only gets responses with status `Sent`. Draft and Approved responses are the team's work in progress. Everyone else (manager, leader, assigned member) sees all non-deleted responses. (Responses that were sent back are soft-deleted by the workflow and already excluded by the repository.)
- Line 141: client as `TicketUserDto` (id + name only).
- Line 142: manager may be null (before routing), so we check first.
- Line 145: the assigned members. The repository already excluded deleted assignments.
- Line 146: snapshots, filtered by `SnapshotsVisibleTo` (Part 7), sorted oldest first (`OrderBy`), mapped to DTOs.
- Line 147: the responses chosen above, oldest first.
- Line 148: `TicketWorkflow.AllowedActions(user, ticket)` asks the workflow rules table which actions this user may do on this ticket right now, and converts them to strings. See [chapter 3](03-workflow.md).

### Part 7: `SnapshotsVisibleTo` (lines 151-168)

```csharp
        // Same idea as responses: a client sees the original code and only the fixes that were
        // actually sent to them, not attempts still in review or sent back by the team
        private static IEnumerable<CodeSnapshot> SnapshotsVisibleTo(Ticket ticket, User user)
        {
            if (user.Role != UserRole.Client)
                return ticket.Snapshots;

            var fixes = ticket.Snapshots.Where(s => s.Kind == SnapshotKind.After).ToList();

            // Submit saves the After snapshot and its response with the same CreatedAt,
            // so each sent response's fix is the latest snapshot not newer than it
            var sentFixes = ticket.Responses
                .Where(r => r.Status == ResponseStatus.Sent)
                .Select(r => fixes.Where(s => s.CreatedAt <= r.CreatedAt).MaxBy(s => s.CreatedAt))
                .OfType<CodeSnapshot>();

            return ticket.Snapshots.Where(s => s.Kind == SnapshotKind.Before).Concat(sentFixes).Distinct();
        }
```

- Lines 151-152: the goal. A client sees the original code, plus only the fixes that reached them. Not attempts still in review, and not attempts the team sent back.
- Lines 155-156: non-clients see every snapshot.
- Line 158: all "After" snapshots (every fix attempt).
- Lines 160-165: the tricky part. A snapshot has no direct link to its response. But when a member submits, the workflow gives the After snapshot and the draft response the **same** `CreatedAt` (`submittedAt` in `TicketWorkflowService`). So for each **sent** response, its fix is "the newest After snapshot whose `CreatedAt` is not later than the response's `CreatedAt`". `MaxBy(s => s.CreatedAt)` picks the item with the largest date, or null if there are none. `OfType<CodeSnapshot>()` keeps only real snapshots, which drops any nulls.
- Line 167: the Before snapshot(s), joined (`Concat`) with the sent fixes. `Distinct()` removes duplicates in case two sent responses point to the same snapshot (the objects come from the same loaded collection, so the same snapshot is the same object).

Note: this works by matching timestamps, so it depends on the workflow's Submit step keeping the same `CreatedAt` on both rows. If that step changes, this method must change with it.

### Part 8: the small mappers (lines 170-188)

```csharp
        private static TicketUserDto ToUserDto(User user) => new(user.Id, user.Name);

        private static CodeSnapshotDto ToSnapshotDto(CodeSnapshot snapshot) => new(
            snapshot.Id,
            snapshot.Kind.ToString(),
            snapshot.RepositoryUrl,
            snapshot.Branch,
            snapshot.CommitSha,
            snapshot.CreatedAt);

        private static TicketResponseDto ToResponseDto(TicketResponse response) => new(
            response.Id,
            response.Summary,
            response.Status.ToString(),
            ToUserDto(response.Author),
            response.CreatedAt,
            response.SentAt);
    }
}
```

- Line 170: entity `User` -> `TicketUserDto` (id and name only).
- Lines 172-178: entity `CodeSnapshot` -> `CodeSnapshotDto`. `Kind` becomes `"Before"`/`"After"`.
- Lines 180-186: entity `TicketResponse` -> `TicketResponseDto`. `response.Author` is loaded by the repository (`ThenInclude(r => r.Author)`).
- This is the "never return entities" rule in practice: every entity is converted to a DTO before it leaves the service. Mapping is written by hand (no AutoMapper), so every field that goes out is visible in the code.

**Committee might ask:**
- *Q: Why 404 and not 403 when a user opens someone else's ticket?* A: A 403 would confirm the ticket exists. With 404 for both cases, nobody can probe which ticket ids exist. For create ticket we use 403 because there's nothing secret about that endpoint.
- *Q: How do you stop a client from seeing the team's drafts?* A: In `ToDetailsDto`, a client only gets responses with status `Sent`, and `SnapshotsVisibleTo` gives a client only the Before snapshot and the fixes that belong to sent responses. In the history, `ClientVisibleActions` hides internal steps like Assign and LeaderSendBack.
- *Q: Why is creating the ticket, the snapshot and the audit row one save?* A: One `SaveChangesAsync` = one transaction. Either all three rows exist or none do. A ticket without its Before snapshot or without an audit row is impossible.
- *Q: Why reload the ticket after creating it?* A: The new entity doesn't have the `Client` object loaded, only `ClientId`, and the DTO needs the client's name and the allowed actions. Reusing `GetDetailsDtoAsync` guarantees the response looks exactly like `GET /api/tickets/{id}`.

---

## [TeamService.cs](../../TicketingSystem.Application/Services/TeamService.cs)

**What it is:** The service behind `GET /api/teams`. A manager gets every team (to route tickets). A leader gets only their own team (to assign members).

**The code:**

```csharp
using TicketingSystem.Application.DTOs.Teams;
using TicketingSystem.Application.DTOs.Tickets;
using TicketingSystem.Application.Exceptions;
using TicketingSystem.Application.Interfaces;
using TicketingSystem.Domain.Enums;

namespace TicketingSystem.Application.Services
{
    public class TeamService(ITeamRepository teams, IUserRepository users) : ITeamService
    {
        // The manager needs every team to route tickets; a leader only needs their own team to assign
        public async Task<IReadOnlyList<TeamDto>> GetTeamsAsync(Guid userId, CancellationToken cancellationToken)
        {
            var user = await users.GetActingUserAsync(userId, cancellationToken);

            if (user.Role is not (UserRole.TeamsManager or UserRole.Admin or UserRole.TeamLeader))
                throw new ForbiddenException("You can't view teams.");

            var all = await teams.ListWithMembersAsync(cancellationToken);
            var visible = user.Role == UserRole.TeamLeader
                ? all.Where(t => t.LeaderId == user.Id)
                : all;

            return visible
                .Select(t => new TeamDto(
                    t.Id,
                    t.Name,
                    new TicketUserDto(t.Leader.Id, t.Leader.Name),
                    t.Members
                        .Where(m => !m.IsDeleted && m.User.IsActive && !m.User.IsDeleted)
                        .Select(m => new TicketUserDto(m.User.Id, m.User.Name))
                        .ToList()))
                .ToList();
        }
    }
}
```

**Line by line:**
- Line 9: primary constructor with the two repositories it needs.
- Line 11: the comment states the two uses.
- Line 14: fresh user (401 if inactive).
- Lines 16-17: **pattern matching** with `is not (A or B or C)`: true when the role is none of those three. Clients and team members get 403. They have no picker that needs teams. 403 (not 404) because the endpoint itself isn't secret.
- Line 19: load all non-deleted teams with leader and members, sorted by name.
- Lines 20-22: a leader keeps only teams where `LeaderId` is their own id. Managers and admins keep all.
- Lines 24-33: map each `Team` entity to a `TeamDto`. Members are filtered: the membership must not be deleted, and the user must be active and not deleted. So the assign picker never offers someone who can't log in anymore.

Note: the leader filter on lines 20-22 runs in memory, after all teams are loaded. This is fine for the small number of teams in the MVP. The workflow still checks on the server that assigned members belong to the ticket's team, so this list is only for the UI.

**Committee might ask:**
- *Q: Why can't a team member call `/api/teams`?* A: No screen for a member needs it. Showing less is safer by default.
- *Q: If the frontend only shows active members, can someone still assign an inactive one by calling the API directly?* A: The assign step checks the members against the team on the server (see [chapter 3](03-workflow.md)). The picker is for convenience, not security.

---

## A request through this layer

Here is `GET /api/tickets/{id}` for a **TeamLeader**, step by step through the code in this chapter.

1. **Controller** (API, [chapter 5](05-api.md)): JwtBearer has already validated the token. `TicketsController.GetById` reads the user id from the token's `sub` claim with `User.GetUserId()` and calls `ticketService.GetByIdAsync(userId, id, cancellationToken)`. It passes no role.
2. **[TicketService.GetByIdAsync](../../TicketingSystem.Application/Services/TicketService.cs)** (line 74) calls `users.GetActingUserAsync(userId, ...)`.
3. **[ActingUserExtensions.GetActingUserAsync](../../TicketingSystem.Application/Services/ActingUserExtensions.cs)** calls `IUserRepository.GetByIdAsync`, which Infrastructure runs as a lookup by primary key. If the leader was deactivated since logging in, it throws `UnauthorizedException`, and the API answers **401**. Otherwise it returns the `User` with `Role = TeamLeader`, straight from the database.
4. Back in `GetByIdAsync` (line 78), the service calls the private **`GetDetailsDtoAsync(user, ticketId, ...)`**.
5. `GetDetailsDtoAsync` calls **[TicketVisibility.VisibleTo(user)](../../TicketingSystem.Application/Services/TicketVisibility.cs)**. The switch expression picks the TeamLeader arm and returns the expression `t => t.Team != null && t.Team.LeaderId == userId`, with the leader's id captured as one value.
6. That expression is passed to **`ITicketRepository.GetDetailsAsync`** ([ITicketRepository.cs](../../TicketingSystem.Application/Interfaces/ITicketRepository.cs)). Infrastructure adds it to its query, and EF turns everything into SQL: "ticket with this id, not deleted, whose team's leader is this user", plus the includes for client, manager, team, assignments, snapshots and responses.
7. **If no row comes back** (the ticket doesn't exist, or it belongs to another team, or it hasn't been routed yet), `GetDetailsDtoAsync` throws `NotFoundException`. The API's `GlobalExceptionHandler` maps it to **404 "Ticket not found."**. The leader can't tell "doesn't exist" from "not yours".
8. **If the ticket is found**, `ToDetailsDto(ticket, user)` builds the DTO:
   - The role is not Client, so **all** non-deleted responses are included, drafts too.
   - `SnapshotsVisibleTo` returns **all** snapshots for a non-client, sorted by `CreatedAt`.
   - Client, manager and assignees become `TicketUserDto` (id + name only).
   - `TicketWorkflow.AllowedActions(user, ticket)` returns the buttons this leader may press now. For example, `["Assign"]` if the ticket is `Routed`, or `["LeaderApprove", "LeaderSendBack"]` if it is in `LeaderReview` (see [chapter 3](03-workflow.md) for the rules).
9. The `TicketDetailsDto` goes back up to the controller, which returns **200 OK**. ASP.NET Core serializes the record to JSON. No entity ever leaves the Application layer.

What never happened along the way: no role was read from the token, no entity was returned, and no HTTP code was decided inside Application. Application only threw named exceptions; the API turned them into status codes.
