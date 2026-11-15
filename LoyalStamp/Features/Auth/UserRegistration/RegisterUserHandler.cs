using LoyalStamp.Data.Entities;
using LoyalStamp.Shared;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace LoyalStamp.Features.Auth.UserRegistration
{
    public class RegisterUserRequest
    {
        [Required(ErrorMessage = "Full name is required.")]
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
    }
    public class RegisterUserHandler
    {
        private readonly UserManager<User> _userManager;
        private readonly UserIdGenerator _idGenerator;

        public RegisterUserHandler(UserManager<User> userManager, UserIdGenerator idGenerator)
        {
            _userManager = userManager;
            _idGenerator = idGenerator;
        }

        public async Task<(bool Succeeded, IEnumerable<string> Errors)> HandleAsync(RegisterUserRequest request)
        {
            var customId = await _idGenerator.GenerateCustomerIdAsync();

            var user = new User
            {
                Id = customId,
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                CreatedAt = DateTime.Now
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                return (false, createResult.Errors.Select(e => e.Description));
            }

            // automatically assign the "Customer" role to the newly registered user
            var roleResult = await _userManager.AddToRoleAsync(user, "Customer");
            if (!roleResult.Succeeded)
            {
                return (false, roleResult.Errors.Select(e => e.Description));
            }

            return (true, Array.Empty<string>());
        }
    }
}
