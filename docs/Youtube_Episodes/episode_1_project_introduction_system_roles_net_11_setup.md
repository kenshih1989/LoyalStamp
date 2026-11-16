# Episode 1: Project Introduction, System Roles & .NET 11 Project Setup

## Episode Metadata
* **Series:** Build a Full-Stack Digital Stamp Card App with Blazor & SQL Server (Vertical Slice Architecture)
* **Framework & Tools:** Blazor Web App (.NET 11, Interactive Server Render Mode), Visual Studio Community 2026
* **Episode Goal:** Introduce the real-world problem and objectives of **LoyalStamp**, provide a high-level walkthrough of the system roles and use cases, and set up the `.NET 11` Vertical Slice Architecture (VSA) project skeleton.
* **Status:** Ready to Record (No finished-app demo required in this episode).

---

## Segment Breakdown & Teaching Script Outline

### 1. Real-World Problem & Project Objective (~3 mins)
* **The Problem:**
  * Physical paper stamp cards are easily lost, damaged, or forgotten at home when customers visit a store.
  * Local merchants have no visibility into customer retention, active cardholders, or repeat-visit frequency.
* **The Objective:**
  * Build **LoyalStamp**, a full-stack digital loyalty stamp card web application that replaces physical stamp cards with unique scannable QR codes (plus short fallback codes) and real-time stamp updates.
* **Core Tech Stack Overview:**
  * **Frontend & Backend:** Blazor Web App (`.NET 11`, Interactive Server Render Mode)
  * **Database & ORM:** Microsoft SQL Server + Entity Framework Core (Code-First)
  * **Auth:** ASP.NET Core Identity (`Customer` and `Merchant` roles)
  * **Real-Time & QR:** ASP.NET Core SignalR + `QRCoder`

---

### 2. High-Level System & Role Walkthrough (~5 mins)
Walk through the system architecture and explain what each user role can do inside **LoyalStamp** (using the Use Case Diagram as a visual aid):

* **Customer Role (`Customer`):**
  * **Account Access:** Register as a customer (`/register-as-user`) and log in (`/login`).
  * **Campaign Discovery:** Browse active merchant stamp campaigns and join them (enforcing a 1-card-per-campaign rule).
  * **Digital Stamp Wallet:** View joined stamp cards with a visual progress grid (`CurrentStamps / TargetStamps`).
  * **Checkout QR / Short Code:** Open a card to display a unique scannable QR code or alphanumeric `CardCode` for the merchant to scan at checkout.
  * **Live Sync & History:** Receive instant UI stamp updates via SignalR and view personal earn/redeem transaction history.

* **Merchant Role (`Merchant`):**
  * **Account & Store Onboarding:** Register as a merchant (`/register-as-merchant`) and configure a 1-to-1 **Store Profile** (`StoreName`, `Description`, `LogoUrl`).
  * **Campaign Management:** Create, edit, and toggle active/inactive status for stamp campaigns (`MinSpendAmount`, `TargetStamps`, `RewardItem`).
  * **Checkout Operations:** Scan or enter a customer's card code to issue stamps (`Floor(SpendAmount / MinSpendAmount)`) or verify and redeem rewards (with concurrency protection).
  * **Store Analytics:** Track engagement via the Merchant KPI Dashboard (Total Stamps Issued, Total Rewards Redeemed, Active Customers).

---

### 3. Creating the .NET 11 Blazor Web App (~4 mins)
Switch to **Visual Studio Community 2026** and create the base solution:
1. Select **Blazor Web App** project template.
2. Name the project `LoyalStamp`.
3. Configure project options:
   * **Framework:** `.NET 11.0`
   * **Authentication Type:** `None` (we will build custom Identity + 12-digit User IDs cleanly in Episode 2)
   * **Interactive Render Mode:** `Server`
   * **Interactivity Location:** `Per page/component`
   * **Include Sample Pages:** Checked (we will clean them up immediately to show how to strip a template)
4. Trust the local HTTPS development certificate if running on a fresh machine:
   ```powershell
   dotnet dev-certs https --trust
   ```

---

### 4. Cleaning Up the Template & Vertical Slice Folder Structure (~6 mins)
Explain **Vertical Slice Architecture (VSA)**—organizing code by *business feature* rather than technical layers (avoiding global `Services/`, `Repositories/`, or `DTOs/` folders).

1. **Remove Boilerplate Pages:**
   * Delete `Components/Pages/Counter.razor` and `Components/Pages/Weather.razor`.
   * Strip out the `Counter` and `Weather` links from `Components/Layout/NavMenu.razor`.
2. **Move `_Imports.razor` to Project Root:**
   * Move `Components/_Imports.razor` to `LoyalStamp/_Imports.razor` (alongside `Program.cs`) so Razor directives (`@rendermode`, `@inject`, etc.) apply automatically to components inside `Features/`.
3. **Create the Top-Level VSA Folder Skeleton:**
   ```text
   LoyalStamp/
   ├── Components/                  # App.razor, Routes.razor, Layout/, Pages/Home.razor
   ├── Data/                        # Entities/, Migrations/, ApplicationDbContext, DbSeeder
   │   └── Entities/
   ├── Features/                    # Self-contained business slices
   │   ├── Auth/
   │   │   ├── Login/
   │   │   ├── UserRegistration/
   │   │   └── MerchantsRegistration/
   │   ├── Campaigns/
   │   ├── CustomerWallet/
   │   ├── StampOperations/
   │   └── HistoryAndDashboard/
   └── Shared/                      # Cross-cutting utilities (UserIdGenerator, QrCodeHelper, SignalR Hub)
   ```

---

## Episode 1 Wrap-Up & Teaser for Episode 2
* **Summary:** We defined the problem, mapped out Customer vs. Merchant capabilities, created the `.NET 11` Blazor Web App, and established our Vertical Slice folder structure.
* **Next Episode Preview:** In Episode 2, we will wire up SQL Server with Entity Framework Core, build our database entities, implement concurrency-safe **12-digit custom User IDs** (`100000000001` for Customers), and complete the end-to-end **Customer Registration, Login, and Home/Logout** flow.