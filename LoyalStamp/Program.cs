using LoyalStamp.Components;
using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Features.Auth.MerchantsRegistration;
using LoyalStamp.Features.Auth.UserRegistration;
using LoyalStamp.Features.Campaigns.CreateCampaign;
using LoyalStamp.Features.Campaigns.EditCampaign;
using LoyalStamp.Features.Campaigns.ListCampaigns;
using LoyalStamp.Features.CustomerWallet.ExploreCampaigns;
using LoyalStamp.Features.CustomerWallet.MyCards;
using LoyalStamp.Features.HistoryAndDashboard.CustomerHistory;
using LoyalStamp.Features.HistoryAndDashboard.MerchantDashboard;
using LoyalStamp.Features.StampOperations.IssueStamp;
using LoyalStamp.Features.StampOperations.RedeemReward;
using LoyalStamp.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register the DbContext with a factory and configure it to use SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped(p =>
    p.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

// Register ASP.NET Core Identity services with the custom ApplicationUser class and IdentityRole
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddScoped<UserIdGenerator>();
builder.Services.AddScoped<RegisterUserHandler>();
builder.Services.AddScoped<RegisterMerchantHandler>();
builder.Services.AddScoped<ListCampaignsQuery>();
builder.Services.AddScoped<CreateCampaignHandler>();
builder.Services.AddScoped<EditCampaignHandler>();
builder.Services.AddScoped<DashboardMetricsQuery>();
builder.Services.AddScoped<JoinCampaignCommand>();
builder.Services.AddScoped<MyCardsQuery>();
builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IssueStampCommand>();
builder.Services.AddScoped<RedeemRewardCommand>();
builder.Services.AddScoped<HistoryQuery>();

// Add authentication and authorization services
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Seed roles in the database
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedRolesAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// 退出登录端点（支持表单 POST 退出）
app.MapPost("/logout", async (SignInManager<User> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/");
});

app.MapHub<StampNotificationHub>("/hubs/stamps");

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
