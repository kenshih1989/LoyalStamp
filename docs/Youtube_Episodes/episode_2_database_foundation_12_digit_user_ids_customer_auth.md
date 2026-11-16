# Episode 2: Database Foundation, Custom 12-Digit User IDs & Customer Auth

## Episode Metadata
* **Series:** Build a Full-Stack Digital Stamp Card App with Blazor & SQL Server (Vertical Slice Architecture)
* **Framework & Tools:** .NET 11, EF Core (Code-First), ASP.NET Core Identity, SQL Server Express / SSMS
* **Episode Goal:** Configure SQL Server and EF Core, build the database entities and concurrency-safe 12-digit User ID sequences, run initial migrations, seed system roles, and implement the complete Customer Registration, SSR Cookie Login, and Home/Logout flow.

---

## Segment Breakdown & Teaching Script Outline

### 1. NuGet Packages & SQL Server Connection Setup (~3 mins)
1. Install the required EF Core and Identity NuGet packages into `LoyalStamp`:
   * `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
   * `Microsoft.EntityFrameworkCore.SqlServer`
   * `Microsoft.EntityFrameworkCore.Tools`
   * `Microsoft.EntityFrameworkCore.Design`
2. Configure `DefaultConnection` in `appsettings.json` (clean web-app connection string with `MultipleActiveResultSets=True` and `TrustServerCertificate=True`):
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=YOUR_SERVER\\SQLEXPRESS;Database=LoyalStampDb;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True"
     }
   }
   ```

---

### 2. Core Database Entities & Custom Identity Schema (~6 mins)
1. **Create `Data/Entities/User.cs`:**
   * Extend `IdentityUser` with `FullName` (`Required`, `MaxLength(100)`) and `CreatedAt` (`DateTime.UtcNow`).
   * Explain why `Id`, `Email`, and `PasswordHash` are inherited automatically from `IdentityUser`.
2. **Add the 4 Business Entities in `Data/Entities/` (or prepare schema):**
   * `Merchant.cs` (`Id`, `UserId` unique FK, `StoreName`, `Description`, `LogoUrl`, `CreatedAt`)
   * `StampCampaign.cs` (`Id`, `MerchantId` FK, `Title`, `MinSpendAmount`, `TargetStamps`, `RewardItem`, `IsActive`, `CreatedAt`)
   * `CustomerCard.cs` (`Id` as `Guid`, `UserId` FK, `CampaignId` FK, `CardCode`, `CurrentStamps`, `TotalRedeemedCount`, `LastUpdated`, `RowVersion` `[Timestamp]`)
   * `StampTransaction.cs` (`Id`, `CustomerCardId` FK, `TransactionType`, `StampsChanged`, `SpendAmount`, `Notes`, `CreatedAt`)
3. **Configure `Data/ApplicationDbContext.cs`:**
   * Inherit from `IdentityDbContext<User>`.
   * Rename the 7 default `AspNet*` tables to clean plural names (`Users`, `Roles`, `UserRoles`, `UserClaims`, `UserLogins`, `RoleClaims`, `UserTokens`) in `OnModelCreating` after `base.OnModelCreating(builder)`.
   * Add unique index on `User.Email` and set `User.Id` max length to `12`.
4. **Create `Data/ApplicationDbContextFactory.cs`:**
   * Implement `IDesignTimeDbContextFactory<ApplicationDbContext>` so EF Core CLI / Package Manager Console commands (`Add-Migration`, `Update-Database`) work reliably at design time.

---

### 3. Concurrency-Safe 12-Digit Custom User IDs & Migrations (~5 mins)
Explain why human-readable IDs (`100000000001` for Customers, `200000000001` for Merchants) are helpful for support/admin lookup while keeping `Guid` for `CustomerCards.Id` (unpredictable QR payloads).

1. **Define SQL Server Sequences in `ApplicationDbContext.OnModelCreating`:**
   * `CustomerIdSeq` (starts at `1`, increments by `1`)
   * `MerchantIdSeq` (starts at `1`, increments by `1`)
2. **Create `Shared/UserIdGenerator.cs`:**
   * Query `SELECT NEXT VALUE FOR CustomerIdSeq` / `MerchantIdSeq` via `IDbContextFactory<ApplicationDbContext>` and format as `$"1{nextVal:D11}"` and `$"2{nextVal:D11}"`.
3. **Create `Data/DbSeeder.cs`:**
   * Idempotent `SeedRolesAsync` method to create `"Customer"` and `"Merchant"` roles on startup.
4. **Configure `Program.cs` (Before vs. After `builder.Build()`):**
   * **Before `builder.Build()`:** Register `IDbContextFactory<ApplicationDbContext>`, `Scoped` `ApplicationDbContext`, `AddIdentity<User, IdentityRole>`, `ConfigureApplicationCookie` (`LoginPath = "/login"`), `AddCascadingAuthenticationState()`, `UserIdGenerator`, and `RegisterUserHandler`.
   * **After `builder.Build()`:** Run `DbSeeder.SeedRolesAsync`, `UseAuthentication()`, `UseAuthorization()`, `UseAntiforgery()`, and `MapPost("/logout", ...)`.
5. **Run Migrations into `Data/Migrations`:**
   ```powershell
   Add-Migration InitialSchema -OutputDir Data\Migrations
   Update-Database
   ```

---

### 4. Customer Registration Slice (`Features/Auth/UserRegistration`) (~5 mins)
1. **Create `RegisterUserCommand.cs`:**
   * `RegisterUserRequest` with DataAnnotations validation (`FullName`, `Email`, `Password`, `ConfirmPassword`).
   * `RegisterUserHandler` generating the 12-digit Customer ID (`100000000001`), creating the `User`, and assigning the `"Customer"` role.
2. **Create `RegisterUserPage.razor` (`@page "/register-as-user"`):**
   * Interactive Server form (`@rendermode InteractiveServer`) with validation messages, error alert banner, loading spinner, and cross-link to `/register-as-merchant`.

---

### 5. SSR Cookie Login (`Features/Auth/Login`) & Home / Logout Shell (~6 mins)
1. **Explain the Blazor Interactive Server Cookie Rule:**
   * Why `LoginPage.razor` omits `@rendermode InteractiveServer` (HTTP cookies must be written during a standard HTTP POST before a SignalR circuit starts).
2. **Create `Features/Auth/Login/LoginPage.razor` (`@page "/login"`):**
   * Handle `[SupplyParameterFromForm]` cleanly (`Input ??= new();` in `OnInitialized()` to avoid warning `BL0008`).
   * Verify credentials with `CheckPasswordSignInAsync` and issue the cookie with a custom `"FullName"` claim via `SignInManager.SignInWithClaimsAsync`.
   * Redirect by role (`Merchant` -> `/merchant/dashboard`, `Customer` -> `/explore`).
3. **Update `Components/Routes.razor` & `Components/App.razor`:**
   * Wrap `<Router>` in `<CascadingAuthenticationState>` and use `<AuthorizeRouteView>`.
   * Add Bootstrap Icons CDN link to `App.razor`.
4. **Build `Components/Pages/Home.razor` & Update `NavMenu.razor`:**
   * `<NotAuthorized>` view: Display **Register as Merchant**, **Register as Customer**, and **Login** buttons.
   * `<Authorized>` view: Greet the user by their `"FullName"` claim (`Welcome back, Alex Tan!`), show role-specific CTA button, and provide a CSRF-protected `<form action="/logout" method="post">` button (also mirrored in `NavMenu.razor`).
5. **Live Test Walkthrough:**
   * Register sample customer (`Alex Tan`), inspect `Users` and `UserRoles` in SSMS (`100000000001`), log in, verify personalized greeting on Home, and test Logout.

---

## Episode 2 Wrap-Up & Teaser for Episode 3
* **Summary:** Our database foundation, custom 12-digit ID generator, role seeding, and Customer Auth loop are fully operational.
* **Next Episode Preview:** In Episode 3, we will build **Merchant Registration (`/register-as-merchant`)** with atomic Store Profile creation and implement **Merchant Stamp Campaign Management** (`CreateCampaign`, `EditCampaign`, and `ListCampaigns`).