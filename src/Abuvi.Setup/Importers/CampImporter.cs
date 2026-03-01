namespace Abuvi.Setup.Importers;

using Abuvi.API.Data;
using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;

public class CampImporter(AbuviDbContext db)
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

                // Duplicate check
                if (await db.Camps.AnyAsync(c => c.Name.ToLower() == name.ToLower()))
                {
                    results.Add(new(i + 1, false, $"Duplicate camp name: {name}"));
                    continue;
                }

                var camp = new Camp
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Description = CsvHelper.Optional(r, "description"),
                    Location = CsvHelper.Optional(r, "location"),
                    PricePerAdult = CsvHelper.RequireDecimal(r, "pricePerAdult"),
                    PricePerChild = CsvHelper.RequireDecimal(r, "pricePerChild"),
                    PricePerBaby = CsvHelper.RequireDecimal(r, "pricePerBaby"),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.Camps.Add(camp);
                await db.SaveChangesAsync();
                imported++;
                results.Add(new(i + 1, true, null));
            }
            catch (Exception ex)
            {
                results.Add(new(i + 1, false, ex.Message));
            }
        }

        return new("Camps", rows.Count, imported, rows.Count - imported, results);
    }
}
