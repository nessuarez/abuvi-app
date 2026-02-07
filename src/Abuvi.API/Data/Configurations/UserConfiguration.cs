using Abuvi.API.Features.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abuvi.API.Data.Configurations;

/// <summary>
/// EF Core configuration for User entity
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Table name (snake_case for PostgreSQL convention)
        builder.ToTable("users");

        // Primary key
        builder.HasKey(u => u.Id);

        // Email: required, max 255, unique index
        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255)
            .HasColumnName("email");

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");

        // Password hash: required
        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasColumnName("password_hash");

        // First name: required, max 100
        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("first_name");

        // Last name: required, max 100
        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("last_name");

        // Phone: optional, max 20
        builder.Property(u => u.Phone)
            .HasMaxLength(20)
            .HasColumnName("phone");

        // Role: stored as string, max 20
        builder.Property(u => u.Role)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("role");

        // Family unit FK: nullable (will be enforced when FamilyUnit entity exists)
        builder.Property(u => u.FamilyUnitId)
            .HasColumnName("family_unit_id");

        // IsActive: required, default true
        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        // Timestamps: required, default NOW()
        builder.Property(u => u.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(u => u.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");
    }
}
