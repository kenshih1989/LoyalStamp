### LoyalStamp - Architecture Convention & Database Schema

#### 1. Tech Stack & Development Environment
*   **Framework:** Blazor Web App (.NET 11, Interactive Server Render Mode)
*   **IDE:** Visual Studio Community 2026
*   **Database:** Microsoft SQL Server + Entity Framework Core (Code-First)
*   **Authentication & Authorization:** ASP.NET Core Identity (Roles: Customer, Merchant)
*   **Real-Time Communication:** ASP.NET Core SignalR (for instant stamp card UI updates)
*   **QR Code Generation:** QRCoder (NuGet package for generating Base64 QR images on the server)

#### 2. Architecture Pattern: Vertical Slice Architecture (VSA)
*   **Core Principle:** Organize code by **Feature (Vertical Slice)** rather than technical layers (do NOT create global Services/, Repositories/, or DTOs/ folders).
*   **Self-Contained Slices:** Each feature folder encapsulates its Blazor UI (.razor), request/response records, validation rules, and direct database access via `IDbContextFactory<ApplicationDbContext>` (recommended for Blazor Interactive Server concurrency safety).
*   **Folder Structure:**
```text
LoyalStamp/
├── Components/                  # Shared Layout, NavMenu, App.razor, Routes.razor, reusable UI widgets
├── Data/                        # ApplicationDbContext, Entities, EF Core Migrations, DbSeeder
├── Features/                    # Self-contained business slices
│   ├── Auth/                    # Role selection & Merchant store profile setup
│   ├── Campaigns/
│   │   ├── CreateCampaign/      # CreateCampaignPage.razor + CreateCampaignHandler.cs
│   │   ├── EditCampaign/        # EditCampaignPage.razor + EditCampaignHandler.cs
│   │   └── ListCampaigns/       # MerchantCampaignsPage.razor + ListCampaignsQuery.cs
│   ├── CustomerWallet/
│   │   ├── ExploreCampaigns/    # ExplorePage.razor + JoinCampaignCommand.cs
│   │   └── MyCards/             # MyWalletPage.razor + CardQrModal.razor + MyCardsQuery.cs
│   ├── StampOperations/
│   │   ├── IssueStamp/          # IssueStampPage.razor + IssueStampCommand.cs
│   │   └── RedeemReward/        # RedeemRewardPage.razor + RedeemRewardCommand.cs
│   └── HistoryAndDashboard/
│       ├── CustomerHistory/     # TransactionHistoryPage.razor + HistoryQuery.cs
│       └── MerchantDashboard/   # DashboardPage.razor + DashboardMetricsQuery.cs
└── Shared/                      # Cross-cutting helpers (QrCodeHelper.cs, StampNotificationHub.cs)
```

#### 3. SQL Server Database Schema & EF Core Configurations
##### Table 1: ApplicationUser (Extends IdentityUser)
*   **Id** (string, PK, **MaxLength 12**, **FixedLength 12**) — 12-character ID (Sequential serial number starting from 1):
    *   **Customer Prefix:** Starts with `1` followed by an 11-digit sequential number (e.g., `100000000001`, `100000000002`)
    *   **Merchant Prefix:** Starts with `2` followed by an 11-digit sequential number (e.g., `200000000001`, `200000000002`)
*   Email (string, Unique)
*   FullName (string, Required, MaxLength 100)
*   CreatedAt (DateTime, DateTime.Now)
*   *Navigation:* Merchant? MerchantProfile, ICollection<CustomerCard> CustomerCards

##### Table 2: Merchants
*   Id (int, PK, Identity)
*   UserId (string, FK -> ApplicationUser.Id, Unique, MaxLength 12)
*   StoreName (string, Required, MaxLength 100)
*   Description (string?, MaxLength 500)
*   LogoUrl (string?, MaxLength 300)
*   CreatedAt (DateTime, DateTime.Now)
*   *Navigation:* ApplicationUser User, ICollection<StampCampaign> Campaigns

##### Table 3: StampCampaigns
*   Id (int, PK, Identity)
*   MerchantId (int, FK -> Merchants.Id)
*   Title (string, Required, MaxLength 100) — e.g., "Coffee Lover Stamp Card"
*   MinSpendAmount (decimal(18,2), Required) — Minimum spend to earn 1 stamp
*   TargetStamps (int, Required) — Stamps required to redeem reward (e.g., 8 or 10)
*   RewardItem (string, Required, MaxLength 150) — e.g., "1 Free Handcrafted Latte"
*   IsActive (bool, Default true)
*   CreatedAt (DateTime, DateTime.Now)
*   *Navigation:* Merchant Merchant, ICollection<CustomerCard> CustomerCards

##### Table 4: CustomerCards
*   Id (Guid, PK) — Used directly as the unique payload inside the QR code
*   UserId (string, FK -> ApplicationUser.Id, MaxLength 12)
*   CampaignId (int, FK -> StampCampaigns.Id)
*   CardCode (string, Required, Unique, MaxLength 12) — Short alphanumeric fallback code (e.g., LS-8A92B)
*   CurrentStamps (int, Default 0)
*   TotalRedeemedCount (int, Default 0)
*   LastUpdated (DateTime, DateTime.Now)
*   RowVersion (byte[], [Timestamp]) — Optimistic concurrency token to prevent double-redemption
*   *Constraint:* Composite Unique Index on (UserId, CampaignId) so a customer holds only one card per campaign.

##### Table 5: StampTransactions
*   Id (int, PK, Identity)
*   CustomerCardId (Guid, FK -> CustomerCards.Id)
*   TransactionType (string, Required, MaxLength 20) — "Earn" or "Redeem"
*   StampsChanged (int, Required) — Positive for Earn (e.g., +2), negative for Redeem (e.g., -10)
*   SpendAmount (decimal(18,2)?) — Recorded when TransactionType == "Earn"
*   Notes (string?, MaxLength 200)
*   CreatedAt (DateTime, DateTime.Now)
