using LoyalStamp.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LoyalStamp.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {

        }

        public DbSet<Merchant> Merchants { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Call the base method to ensure Identity configurations are applied
            base.OnModelCreating(builder);

            // Define a sequences for CustomerId
            builder.HasSequence<long>("CustomerIdSeq")
                .StartsAt(1)
                .IncrementsBy(1);

            // Define a sequence for MerchantId
            builder.HasSequence<long>("MerchantIdSeq")
                .StartsAt(1)
                .IncrementsBy(1);

            // Control the length of the User Id to be 12 characters
            builder.Entity<User>()
                .Property(u => u.Id)
                .HasMaxLength(12);

            // Remove the default Identity table prefix and configure the table names
            builder.Entity<User>().ToTable("Users");
            builder.Entity<IdentityRole>().ToTable("Roles");
            builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
            builder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims");
            builder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins");
            builder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims");
            builder.Entity<IdentityUserToken<string>>().ToTable("UserTokens");

            // Configure the User entity
            builder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

            // Configure the Merchant entity
            builder.Entity<Merchant>(entity =>
            {
                entity.ToTable("Merchants");

                entity.HasIndex(m => m.UserId)
                    .IsUnique();

                entity.HasOne(m => m.User)
                    .WithOne(u => u.MerchantProfile)
                    .HasForeignKey<Merchant>(m => m.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
