namespace Abuvi.Setup.Importers;

using Abuvi.API.Data;
using Abuvi.API.Features.FamilyUnits;
using Microsoft.EntityFrameworkCore;

public class FamilyUnitImporter(AbuviDbContext db)
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
                var name = CsvHelper.Require(r, "name");
                var email = CsvHelper.Require(r, "representativeEmail").ToLowerInvariant();

                // Duplicate check
                if (await db.FamilyUnits.AnyAsync(u => u.Name.ToLower() == name.ToLower()))
                {
                    results.Add(new(i + 1, false, $"Duplicate family unit name: {name}"));
                    continue;
                }

                // Resolve representative user
                var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user is null)
                {
                    results.Add(new(i + 1, false, $"Representative not found: {email}"));
                    continue;
                }

                var unit = new FamilyUnit
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    RepresentativeUserId = user.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.FamilyUnits.Add(unit);

                // Link user back to family unit
                user.FamilyUnitId = unit.Id;

                await db.SaveChangesAsync();
                imported++;
                results.Add(new(i + 1, true, null));
            }
            catch (Exception ex)
            {
                results.Add(new(i + 1, false, ex.Message));
            }
        }

        return new("FamilyUnits", rows.Count, imported, rows.Count - imported, results);
    }
}
