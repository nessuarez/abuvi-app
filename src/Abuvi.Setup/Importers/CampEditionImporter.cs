namespace Abuvi.Setup.Importers;

using Abuvi.API.Data;
using Abuvi.API.Features.Camps;
using Microsoft.EntityFrameworkCore;

public class CampEditionImporter(AbuviDbContext db)
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
                var campName = CsvHelper.Require(r, "campName");
                var year = int.Parse(CsvHelper.Require(r, "year"));

                // Resolve camp
                var camp = await db.Camps.FirstOrDefaultAsync(
                    c => c.Name.ToLower() == campName.ToLower());
                if (camp is null)
                {
                    results.Add(new(i + 1, false, $"Camp not found: {campName}"));
                    continue;
                }

                // Duplicate check (camp + year)
                if (await db.CampEditions.AnyAsync(e => e.CampId == camp.Id && e.Year == year))
                {
                    results.Add(new(i + 1, false,
                        $"Duplicate edition: {campName} {year}"));
                    continue;
                }

                var edition = new CampEdition
                {
                    Id = Guid.NewGuid(),
                    CampId = camp.Id,
                    Year = year,
                    StartDate = DateTime.Parse(CsvHelper.Require(r, "startDate")),
                    EndDate = DateTime.Parse(CsvHelper.Require(r, "endDate")),
                    PricePerAdult = CsvHelper.RequireDecimal(r, "pricePerAdult"),
                    PricePerChild = CsvHelper.RequireDecimal(r, "pricePerChild"),
                    PricePerBaby = CsvHelper.RequireDecimal(r, "pricePerBaby"),
                    MaxCapacity = CsvHelper.OptionalInt(r, "maxCapacity"),
                    Status = Enum.Parse<CampEditionStatus>(
                        CsvHelper.Require(r, "status"), ignoreCase: true),
                    Notes = CsvHelper.Optional(r, "notes"),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.CampEditions.Add(edition);
                await db.SaveChangesAsync();
                imported++;
                results.Add(new(i + 1, true, null));
            }
            catch (Exception ex)
            {
                results.Add(new(i + 1, false, ex.Message));
            }
        }

        return new("CampEditions", rows.Count, imported, rows.Count - imported, results);
    }
}
