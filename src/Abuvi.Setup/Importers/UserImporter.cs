namespace Abuvi.Setup.Importers;

using Abuvi.API.Data;
using Abuvi.API.Features.Users;
using Microsoft.EntityFrameworkCore;

public class UserImporter(AbuviDbContext db)
{
    public async Task<SeedResult> ImportAsync(string filePath)
    {
        var rows = CsvHelper.Parse(filePath);
        var results = new List<SeedRowResult>();
        var imported = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            try
            {
                var r = rows[i];
                var email = CsvHelper.Require(r, "email").ToLowerInvariant();

                if (await db.Users.AnyAsync(u => u.Email == email))
                {
                    results.Add(new(i + 1, false, $"Duplicate email: {email}"));
                    continue;
                }

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                        CsvHelper.Require(r, "password"), workFactor: 12),
                    FirstName = CsvHelper.Require(r, "firstName"),
                    LastName = CsvHelper.Require(r, "lastName"),
                    Phone = CsvHelper.Optional(r, "phone"),
                    DocumentNumber = CsvHelper.Optional(r, "documentNumber"),
                    Role = Enum.Parse<UserRole>(
                        CsvHelper.Require(r, "role"), ignoreCase: true),
                    IsActive = true,
                    EmailVerified = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.Users.Add(user);
                await db.SaveChangesAsync();
                imported++;
                results.Add(new(i + 1, true, null));
            }
            catch (Exception ex)
            {
                results.Add(new(i + 1, false, ex.Message));
            }
        }

        return new("Users", rows.Count, imported, rows.Count - imported, results);
    }
}
