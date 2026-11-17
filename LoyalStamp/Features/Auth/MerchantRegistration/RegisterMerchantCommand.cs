using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace LoyalStamp.Features.Auth.MerchantsRegistration;

public class RegisterMerchantRequest
{
    // Account information (to be written to the Users table)
    [Required(ErrorMessage = "Owner full name is required.")]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    // Store information (to be written to the Merchants table)
    [Required(ErrorMessage = "Store name is required.")]
    [MaxLength(100, ErrorMessage = "Store name cannot exceed 100 characters.")]
    public string StoreName { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [MaxLength(300, ErrorMessage = "Logo URL cannot exceed 300 characters.")]
    [Url(ErrorMessage = "Please enter a valid URL (e.g., https://example.com/logo.png).")]
    public string? LogoUrl { get; set; }
}

public class RegisterMerchantHandler
{
    private readonly UserManager<User> _userManager;
    private readonly UserIdGenerator _idGenerator;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public RegisterMerchantHandler(
        UserManager<User> userManager,
        UserIdGenerator idGenerator,
        IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _userManager = userManager;
        _idGenerator = idGenerator;
        _dbFactory = dbFactory;
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors)> HandleAsync(RegisterMerchantRequest request)
    {
        // Generate a unique merchant user ID starting with '2' (e.g., 200000000001)
        var customMerchantUserId = await _idGenerator.GenerateMerchantIdAsync();

        var user = new User
        {
            Id = customMerchantUserId,
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            CreatedAt = DateTime.UtcNow
        };

        // Attempt to create the user account
        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return (false, createResult.Errors.Select(e => e.Description));
        }

        try
        {
            // Assign the "Merchant" role to the newly registered user
            var roleResult = await _userManager.AddToRoleAsync(user, "Merchant");
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                return (false, roleResult.Errors.Select(e => e.Description));
            }

            // 4. 在 Merchants 表中创建关联店铺档案
            await using var db = await _dbFactory.CreateDbContextAsync();
            var merchant = new Merchant
            {
                UserId = user.Id,
                StoreName = request.StoreName.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim(),
                CreatedAt = DateTime.Now
            };

            db.Merchants.Add(merchant);
            await db.SaveChangesAsync();

            return (true, Array.Empty<string>());
        }
        catch (Exception)
        {
            // rollback: Delete the user account if merchant creation fails
            await _userManager.DeleteAsync(user);
            return (false, new[] { "An error occurred while setting up your store profile. Please try again." });
        }
    }
}