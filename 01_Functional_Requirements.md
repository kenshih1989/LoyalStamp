# LoyalStamp - Functional Requirements & Business Rules

## 1. Authentication & Role Management (Slice: `Features/Auth`)
- Users register and log in using Email and Password (ASP.NET Core Identity).
- During registration (or profile setup), users are assigned either the `Customer` or `Merchant` role.
- A `Merchant` user must set up their Store Profile (`StoreName`, `Description`, `LogoUrl`) before creating stamp campaigns.

## 2. Merchant Campaign Management (Slice: `Features/Campaigns`)
- **Create / Edit Campaign:** Merchant defines:
  - `Title` (e.g., "Coffee Lover Card")
  - `MinSpendAmount` (minimum purchase required to earn 1 stamp, e.g., $10.00)
  - `TargetStamps` (number of stamps needed for reward, e.g., 10 stamps)
  - `RewardItem` (description of free item, e.g., "1 Free Artisan Latte")
  - `IsActive` (toggle campaign visibility)
- **List Merchant Campaigns:** View all campaigns created by the logged-in merchant along with active status.

## 3. Customer Wallet & Discovery (Slice: `Features/CustomerWallet`)
- **Explore Campaigns:** Customers can browse all active (`IsActive == true`) merchant campaigns and click "Join" to add the stamp card to their wallet.
- **Duplicate Prevention:** A customer can only hold one active card per campaign.
- **My Cards View:** Displays all joined cards with a visual stamp grid (`CurrentStamps / TargetStamps`).
- **Checkout Code / QR Generation:** Clicking a card displays a unique, scannable QR Code (and short alphanumeric fallback code) representing the `CustomerCard.Id` for merchant scanning.

## 4. Stamp Issuing & Reward Redemption (Slice: `Features/StampOperations`)
- **Issue Stamps (Merchant Action):**
  - Merchant scans customer's QR code or enters the `CustomerCard` code.
  - Merchant enters the customer's `SpendAmount`.
  - **Calculation Rule:** `StampsEarned = Floor(SpendAmount / Campaign.MinSpendAmount)`. If `SpendAmount < MinSpendAmount`, 0 stamps are issued (display validation message).
  - **Cap / Overflow Rule:** Stamps can accumulate beyond `TargetStamps` (e.g., `12 / 10` stamps), allowing leftover stamps to carry over after redemption.
  - Records an `Earn` entry in `StampTransactions` and updates `CustomerCards.CurrentStamps`.
- **Redeem Reward (Merchant Action):**
  - Available only when `CustomerCard.CurrentStamps >= Campaign.TargetStamps`.
  - Merchant verifies customer's card code and clicks "Confirm Redemption".
  - Deducts `TargetStamps` from `CurrentStamps` (`CurrentStamps -= TargetStamps`), increments `TotalRedeemedCount`, and logs a `Redeem` entry in `StampTransactions`.
  - **Concurrency Guard:** Use optimistic concurrency / database transaction to prevent double-redemption from double-clicks.
- **Real-Time UI Feedback:** When a merchant issues stamps or redeems a reward, notify the customer's active Blazor session via SignalR so the stamp card UI updates immediately without manual page refresh.

## 5. History & Analytics (Slice: `Features/HistoryAndDashboard`)
- **Customer Transaction History:** Chronological list of stamps earned and rewards redeemed across all cards.
- **Merchant Dashboard:** Summary KPIs showing Total Stamps Issued, Total Rewards Redeemed, and Total Active Customers.