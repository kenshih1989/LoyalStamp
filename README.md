# LoyalStamp (v1.0)

**LoyalStamp** is a full-stack digital loyalty stamp card web application built with **Blazor Web App (.NET 11, Interactive Server)** and **Microsoft SQL Server**. Designed using **Vertical Slice Architecture (VSA)**, it allows local merchants to launch and manage customizable loyalty stamp campaigns while enabling customers to collect digital stamps, generate checkout QR codes, and redeem rewards with real-time UI updates via **ASP.NET Core SignalR**.

---

## ✨ Key Features (v1.0)

### 🔐 Authentication & Role Management (`Features/Auth`)
* **Dual-Role Registration & Login:** Powered by ASP.NET Core Identity with custom 12-digit sequential user IDs (`10000000000X` for Customers, `20000000000X` for Merchants).
* **Merchant Onboarding:** Atomic registration flow that provisions both the merchant user account and their 1-to-1 store profile (`StoreName`, `Description`, `LogoUrl`) with rollback protection.
* **Role-Based Navigation:** Dynamic sidebar (`NavMenu.razor`) and route authorization isolating Merchant and Customer workflows.

### 🏪 Merchant Portal (`Features/Campaigns`, `Features/HistoryAndDashboard`, `Features/StampOperations`)
* **Merchant Dashboard (`/merchant/dashboard`):** Displays store profile details and real-time operational KPIs (Active Campaigns, Active Customers, Total Stamps Issued, and Total Rewards Redeemed).
* **Campaign Management (`/merchant/campaigns`):** Create, edit, and toggle active/inactive status for stamp campaigns with configurable minimum spend per stamp, target stamp count, and reward items.
* **Checkout Terminal (`/merchant/issue-stamp`):**
  * Look up customer cards via scanned **QR Code (GUID)** or fallback short alphanumeric **Card Code** (e.g., `LS-8A92B`).
  * Automatic stamp calculation (`Floor(SpendAmount / MinSpendAmount)`) with stamp overflow/carryover support.
  * Reward redemption validation with optimistic concurrency control (`RowVersion`) to prevent double-redemption.
  * Strict merchant ownership guards to prevent cross-store tampering.

### 🎟️ Customer Wallet & Discovery (`Features/CustomerWallet`, `Features/HistoryAndDashboard`)
* **Explore Campaigns (`/explore`):** Browse active loyalty campaigns across participating merchants and add digital stamp cards to the wallet (enforced by composite unique constraints to prevent duplicate joins).
* **Digital Stamp Wallet (`/my-wallet`):** Interactive visual stamp grids showing progress and overflow stamps, paired with a checkout QR code modal (`CardQrModal.razor`) generated via **QRCoder**.
* **Real-Time UI Sync (`StampNotificationHub`):** Powered by **SignalR** so customers see stamp additions, reward redemptions, and toast notifications instantaneously without refreshing their browser.
* **Transaction History (`/history`):** Filterable ledger of all earned stamps and redeemed rewards across stores, complete with lifetime spend and redemption summaries.

---

## 🛠️ Tech Stack

* **Framework:** .NET 11 / ASP.NET Core Blazor Web App (Interactive Server Render Mode)
* **Database:** Microsoft SQL Server + Entity Framework Core (Code-First)
* **Authentication:** ASP.NET Core Identity (Cookie Authentication + Role-Based Authorization)
* **Real-Time Communication:** ASP.NET Core SignalR (`/hubs/stamps`)
* **QR Code Generation:** `QRCoder` (Base64 PNG rendering)
* **UI & Styling:** Bootstrap 5 & Bootstrap Icons

---

## 📂 Project Architecture (Vertical Slice)

```text
LoyalStamp/
├── Components/
│   ├── Layout/                  # MainLayout, role-aware NavMenu
│   └── Pages/                   # Landing Home page & Error page
├── Data/
│   ├── Entities/                # User, Merchant, StampCampaign, CustomerCard, StampTransaction
│   ├── Migrations/              # EF Core Code-First database migrations
│   ├── ApplicationDbContext.cs  # EF Core context, sequences, indexes & relationships
│   ├── ApplicationDbContextFactory.cs
│   └── DbSeeder.cs              # Automatic Customer & Merchant role seeding
├── Features/
│   ├── Auth/                    # Login, UserRegistration, MerchantsRegistration
│   ├── Campaigns/               # ListCampaigns, CreateCampaign, EditCampaign
│   ├── CustomerWallet/          # ExploreCampaigns, MyCards (Wallet + QR Modal)
│   ├── HistoryAndDashboard/     # MerchantDashboard, CustomerHistory
│   └── StampOperations/         # IssueStamp, RedeemReward (Checkout Terminal)
├── Shared/
│   ├── QrCodeHelper.cs          # Server-side Base64 QR code generator
│   ├── StampNotificationHub.cs  # SignalR hub for user-targeted real-time updates
│   └── UserIdGenerator.cs       # Custom sequential ID generator
└── Program.cs                   # Service registration, Identity, SignalR & middleware pipeline
```

---

## 🗄️ Database Schema Overview

1. **`Users` (Identity):** Stores customer and merchant credentials, `FullName`, and custom 12-digit `Id`.
2. **`Merchants`:** 1-to-1 store profile linked to `Users.Id` (`StoreName`, `Description`, `LogoUrl`).
3. **`StampCampaigns`:** Loyalty rules defined by a merchant (`Title`, `MinSpendAmount`, `TargetStamps`, `RewardItem`, `IsActive`).
4. **`CustomerCards`:** Junction table linking a customer to a campaign (`Id` GUID for QR, unique `CardCode` like `LS-8A92B`, `CurrentStamps`, `TotalRedeemedCount`, and `RowVersion` concurrency token).
5. **`StampTransactions`:** Audit log of every `"Earn"` and `"Redeem"` event (`StampsChanged`, `SpendAmount`, `Notes`, `CreatedAt`).

---

## 🚀 Getting Started

### Prerequisites
* [.NET 11 SDK](https://dotnet.microsoft.com/) (or compatible .NET SDK configured in your project)
* [Microsoft SQL Server](https://www.microsoft.com/en-us/sql-server) (LocalDB, Express, or Developer Edition)
* Visual Studio 2026 (or VS Code / Rider)

### Installation & Local Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/<your-username>/LoyalStamp.git
   cd LoyalStamp
   ```

2. **Configure the Database Connection**
   Open `appsettings.json` and update `"DefaultConnection"` to point to your local SQL Server instance:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=LoyalStampDb;Trusted_Connection=True;MultipleActiveResultSets=true"
   }
   ```

3. **Apply Entity Framework Core Migrations**
   In Visual Studio's **Package Manager Console** (or terminal via `dotnet ef`), run:
   ```powershell
   Update-Database
   ```
   *(Note: Identity roles `Customer` and `Merchant` are automatically seeded when the application starts.)*

4. **Trust the Local HTTPS Development Certificate** *(if running on a fresh machine)*
   ```powershell
   dotnet dev-certs https --trust
   ```

5. **Run the Application**
   Press `F5` in Visual Studio or run:
   ```bash
   dotnet run
   ```

---

## 🧪 Quick Walkthrough / Testing Flow

1. **Merchant Setup:** Navigate to `/register-as-merchant` to create a store account, then log in to access `/merchant/dashboard` and create a stamp campaign under **My Campaigns**.
2. **Customer Enrollment:** Open an incognito window, register at `/register-as-user`, browse `/explore`, and click **Join Campaign** to add the card to `/my-wallet`.
3. **Real-Time Stamp & Redeem:** Open the customer's QR code modal in `/my-wallet`, enter the short code (e.g., `LS-XXXXX`) in the merchant's **Issue / Redeem** terminal (`/merchant/issue-stamp`), issue stamps or redeem a reward, and watch the customer wallet update in real time.

---

## 📄 License

This project is released as **v1.0** for educational and portfolio demonstration purposes.
