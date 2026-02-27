namespace Abuvi.Setup.Importers;

using Abuvi.API.Data;
using Abuvi.API.Features.FamilyUnits;
using Microsoft.EntityFrameworkCore;

public class FamilyMemberImporter(AbuviDbContext db)
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
                var unitName = CsvHelper.Require(r, "familyUnitName");
                var firstName = CsvHelper.Require(r, "firstName");
                var lastName = CsvHelper.Require(r, "lastName");
                var dateOfBirth = DateOnly.Parse(CsvHelper.Require(r, "dateOfBirth"));

                // Resolve family unit
                var unit = await db.FamilyUnits.FirstOrDefaultAsync(
                    u => u.Name.ToLower() == unitName.ToLower());
                if (unit is null)
                {
                    results.Add(new(i + 1, false, $"Family unit not found: {unitName}"));
                    continue;
                }

                // Composite duplicate check
                if (await db.FamilyMembers.AnyAsync(m =>
                    m.FamilyUnitId == unit.Id &&
                    m.FirstName.ToLower() == firstName.ToLower() &&
                    m.LastName.ToLower() == lastName.ToLower() &&
                    m.DateOfBirth == dateOfBirth))
                {
                    results.Add(new(i + 1, false,
                        $"Duplicate member: {firstName} {lastName} ({dateOfBirth})"));
                    continue;
                }

                var member = new FamilyMember
                {
                    Id = Guid.NewGuid(),
                    FamilyUnitId = unit.Id,
                    FirstName = firstName,
                    LastName = lastName,
                    DateOfBirth = dateOfBirth,
                    Relationship = Enum.Parse<FamilyRelationship>(
                        CsvHelper.Require(r, "relationship"), ignoreCase: true),
                    DocumentNumber = CsvHelper.Optional(r, "documentNumber"),
                    Email = CsvHelper.Optional(r, "email"),
                    Phone = CsvHelper.Optional(r, "phone"),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.FamilyMembers.Add(member);
                await db.SaveChangesAsync();
                imported++;
                results.Add(new(i + 1, true, null));
            }
            catch (Exception ex)
            {
                results.Add(new(i + 1, false, ex.Message));
            }
        }

        return new("FamilyMembers", rows.Count, imported, rows.Count - imported, results);
    }
}
