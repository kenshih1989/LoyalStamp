USE [LoyalStampDb];
GO

-- 1. Disable all Foreign Key constraints temporarily
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
GO

-- 2. Delete all rows from business & Identity tables (child-to-parent order)
DELETE FROM [dbo].[StampTransactions];
DELETE FROM [dbo].[CustomerCards];
DELETE FROM [dbo].[StampCampaigns];
DELETE FROM [dbo].[Merchants];

-- Delete ASP.NET Core Identity user data (keep AspNetRoles if seeded at startup)
DELETE FROM [dbo].[AspNetUserTokens];
DELETE FROM [dbo].[AspNetUserLogins];
DELETE FROM [dbo].[AspNetUserClaims];
DELETE FROM [dbo].[AspNetUserRoles];
DELETE FROM [dbo].[AspNetUsers];
DELETE FROM [dbo].[AspNetRoleClaims];
-- Uncomment the line below ONLY if you want to wipe roles too (restart app to re-seed):
-- DELETE FROM [dbo].[AspNetRoles];
GO

-- 3. Reset IDENTITY (sequence) counters back to 0 so the next insert starts at 1
DBCC CHECKIDENT ('[dbo].[StampTransactions]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[CustomerCards]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[StampCampaigns]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[Merchants]', RESEED, 0);

-- Reset Identity claim tables (if they use INT identity keys)
IF EXISTS (SELECT * FROM sys.identity_columns WHERE object_id = OBJECT_ID('[dbo].[AspNetUserClaims]'))
    DBCC CHECKIDENT ('[dbo].[AspNetUserClaims]', RESEED, 0);

IF EXISTS (SELECT * FROM sys.identity_columns WHERE object_id = OBJECT_ID('[dbo].[AspNetRoleClaims]'))
    DBCC CHECKIDENT ('[dbo].[AspNetRoleClaims]', RESEED, 0);
GO

-- 4. Re-enable all Foreign Key constraints and verify integrity
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
GO