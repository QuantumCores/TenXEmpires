-- ============================================================================
-- Migration: 20251019130000_create_identity_schema.sql
-- Purpose : Create ASP.NET Identity tables in the auth schema
-- Scope   : Creates AspNetUsers, AspNetRoles, and related Identity tables
-- Notes   :
--   - Uses uuid for user id (uuid v7 generated in application)
--   - Table and column names match EF Core Identity conventions exactly
--   - All tables in auth schema, not dbo
--   - Timestamps use timestamptz
-- ============================================================================

-- Ensure auth schema exists
create schema if not exists auth;

-- Users table (AspNetUsers)
create table if not exists auth."AspNetUsers" (
  "Id"                    uuid primary key,
  "UserName"              varchar(256) null,
  "NormalizedUserName"    varchar(256) null,
  "Email"                 varchar(256) null,
  "NormalizedEmail"       varchar(256) null,
  "EmailConfirmed"        boolean not null default false,
  "PasswordHash"          text null,
  "SecurityStamp"         text null,
  "ConcurrencyStamp"      text null,
  "PhoneNumber"           text null,
  "PhoneNumberConfirmed"  boolean not null default false,
  "TwoFactorEnabled"      boolean not null default false,
  "LockoutEnd"            timestamptz null,
  "LockoutEnabled"        boolean not null default false,
  "AccessFailedCount"     int not null default 0
);

-- Roles table (AspNetRoles)
create table if not exists auth."AspNetRoles" (
  "Id"                uuid primary key,
  "Name"              varchar(256) null,
  "NormalizedName"    varchar(256) null,
  "ConcurrencyStamp"  text null
);

-- User Roles table (AspNetUserRoles)
create table if not exists auth."AspNetUserRoles" (
  "UserId"  uuid not null references auth."AspNetUsers"("Id") on delete cascade,
  "RoleId"  uuid not null references auth."AspNetRoles"("Id") on delete cascade,
  primary key ("UserId", "RoleId")
);

-- User Claims table (AspNetUserClaims)
create table if not exists auth."AspNetUserClaims" (
  "Id"         bigint generated always as identity primary key,
  "UserId"     uuid not null references auth."AspNetUsers"("Id") on delete cascade,
  "ClaimType"  text null,
  "ClaimValue" text null
);

-- User Logins table (AspNetUserLogins)
create table if not exists auth."AspNetUserLogins" (
  "LoginProvider"       varchar(128) not null,
  "ProviderKey"         varchar(128) not null,
  "ProviderDisplayName" text null,
  "UserId"              uuid not null references auth."AspNetUsers"("Id") on delete cascade,
  primary key ("LoginProvider", "ProviderKey")
);

-- User Tokens table (AspNetUserTokens)
create table if not exists auth."AspNetUserTokens" (
  "UserId"        uuid not null references auth."AspNetUsers"("Id") on delete cascade,
  "LoginProvider" varchar(128) not null,
  "Name"          varchar(128) not null,
  "Value"         text null,
  primary key ("UserId", "LoginProvider", "Name")
);

-- Role Claims table (AspNetRoleClaims)
create table if not exists auth."AspNetRoleClaims" (
  "Id"         bigint generated always as identity primary key,
  "RoleId"     uuid not null references auth."AspNetRoles"("Id") on delete cascade,
  "ClaimType"  text null,
  "ClaimValue" text null
);

-- ============================================================================
-- Indexes for ASP.NET Identity
-- ============================================================================

-- Users indexes
create unique index if not exists "UserNameIndex" 
  on auth."AspNetUsers"("NormalizedUserName");

create index if not exists "EmailIndex" 
  on auth."AspNetUsers"("NormalizedEmail");

-- Roles indexes
create unique index if not exists "RoleNameIndex" 
  on auth."AspNetRoles"("NormalizedName");

-- User Roles indexes
create index if not exists "IX_AspNetUserRoles_RoleId" 
  on auth."AspNetUserRoles"("RoleId");

-- User Claims indexes
create index if not exists "IX_AspNetUserClaims_UserId" 
  on auth."AspNetUserClaims"("UserId");

-- User Logins indexes
create index if not exists "IX_AspNetUserLogins_UserId" 
  on auth."AspNetUserLogins"("UserId");

-- Role Claims indexes
create index if not exists "IX_AspNetRoleClaims_RoleId" 
  on auth."AspNetRoleClaims"("RoleId");

-- ============================================================================
-- Grants for Identity tables
-- ============================================================================

-- Grant access to app_user role (for reading user info in game context)
grant select on auth."AspNetUsers" to app_user;

-- Grant full access to app_admin role (for user management)
grant select, insert, update, delete on 
  auth."AspNetUsers",
  auth."AspNetRoles",
  auth."AspNetUserRoles",
  auth."AspNetUserClaims",
  auth."AspNetUserLogins",
  auth."AspNetUserTokens",
  auth."AspNetRoleClaims"
to app_admin;

-- Grant sequence usage for identity columns
grant usage, select on all sequences in schema auth to app_admin;

-- ============================================================================
-- End of migration
-- ============================================================================

