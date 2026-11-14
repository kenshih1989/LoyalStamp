# LoyalStamp - YouTube Tutorial Series Plan

## Series Positioning & Hook
- **Series Title Idea:** *Build a Full-Stack Digital Stamp Card App with Blazor & SQL Server (Vertical Slice Architecture)*
- **Target Audience:** C# / .NET developers learning modern Blazor Web Apps, Vertical Slice Architecture, EF Core, and real-time SignalR integration.
- **Demo Highlight:** Split-screen live demo showing a Merchant issuing stamps on the left browser and the Customer's mobile stamp card updating in real-time with stamp animation on the right browser.

## Episode Breakdown (5-Part Series)

### Episode 1: Project Setup, Vertical Slice Architecture & SQL Server Schema
- **Goal:** Introduce the real-world problem (replacing paper loyalty cards), showcase the finished app demo, and set up the project foundation.
- **Tasks Covered:**
  - Create Blazor Web App in Visual Studio Community (.NET 8/9, Interactive Server + Individual Accounts).
  - Refactor default template into Vertical Slice folder structure (`Features/`, `Data/`, `Shared/`).
  - Build the 5 EF Core Entities (`ApplicationUser`, `Merchants`, `StampCampaigns`, `CustomerCards`, `StampTransactions`), configure `IDbContextFactory`, and run SQL Server migrations.
  - Seed initial `Customer` and `Merchant` roles/accounts for fast testing.

### Episode 2: Merchant Onboarding & Stamp Campaign Management
- **Goal:** Enable merchants to set up their store profile and create customizable stamp rules.
- **Tasks Covered:**
  - Implement `Features/Auth` store profile setup for `Merchant` users.
  - Build `Features/Campaigns/CreateCampaign` & `ListCampaigns` slices.
  - Form validation for `MinSpendAmount`, `TargetStamps`, and `RewardItem`, plus toggling `IsActive` status.

### Episode 3: Customer Wallet, Stamp Card UI & Dynamic QR Codes
- **Goal:** Build the mobile-friendly customer experience to discover campaigns, join cards, and display checkout QR codes.
- **Tasks Covered:**
  - Build `Features/CustomerWallet/ExploreCampaigns` with duplicate-join prevention.
  - Design the visual Stamp Card grid component (`CurrentStamps / TargetStamps`) in `MyWalletPage.razor`.
  - Integrate `QRCoder` in `Shared/QrCodeHelper.cs` to generate scannable QR codes + fallback short codes (`CardCode`).

### Episode 4: Issuing Stamps, Reward Redemption & Real-Time SignalR Sync
- **Goal:** Complete the core business transaction loop with concurrency protection and live cross-window updates.
- **Tasks Covered:**
  - Build `Features/StampOperations/IssueStamp` (lookup card by code, calculate `Floor(SpendAmount / MinSpendAmount)`, record `Earn` transaction).
  - Build `Features/StampOperations/RedeemReward` with `[Timestamp]` optimistic concurrency guard against double-clicks.
  - Configure `StampNotificationHub` (SignalR) so the customer's stamp card animates immediately upon merchant confirmation.

### Episode 5: Transaction History, Merchant Dashboard & Polish
- **Goal:** Add reporting visibility for both roles and wrap up the series.
- **Tasks Covered:**
  - Build `Features/HistoryAndDashboard/CustomerHistory` (audit log of earned/redeemed stamps).
  - Build `Features/HistoryAndDashboard/MerchantDashboard` (KPI cards: total stamps issued, rewards redeemed, active customers).
  - Final end-to-end walkthrough and GitHub repository overview.