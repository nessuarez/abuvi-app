namespace Abuvi.Setup;

using System.Text.Json;
using Abuvi.API.Data;
using Abuvi.API.Features.Camps;
using Abuvi.API.Features.Registrations;
using Microsoft.EntityFrameworkCore;
using Serilog;

public class RegistrationSeeder(AbuviDbContext db)
{
    private record AgeRanges(int BabyMaxAge, int ChildMinAge, int ChildMaxAge, int AdultMinAge);

    public async Task SeedAsync()
    {
        Log.Information("Seeding registrations...");

        var edition = await db.CampEditions
            .Include(e => e.Camp)
            .FirstOrDefaultAsync(e => e.Camp.Name == "Camp Costa" && e.Year == 2027);

        if (edition is null)
        {
            Log.Warning("Registrations: skipped (Camp Costa 2027 not found)");
            return;
        }

        if (await db.Registrations.AnyAsync(r => r.CampEditionId == edition.Id))
        {
            Log.Information("Registrations: already seeded, skipping");
            return;
        }

        var setting = await db.AssociationSettings
            .FirstOrDefaultAsync(s => s.SettingKey == "age_ranges");

        if (setting is null)
        {
            Log.Warning("Registrations: skipped (age_ranges setting not found)");
            return;
        }

        var ranges = JsonSerializer.Deserialize<AgeRanges>(
            setting.SettingValue,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (ranges is null)
        {
            Log.Warning("Registrations: skipped (age_ranges JSON is invalid)");
            return;
        }

        var now = DateTime.UtcNow;

        await SeedGarciaRegistrationAsync(edition, ranges, now);
        await SeedLopezRegistrationAsync(edition, ranges, now);

        Log.Information("Registrations: seeded successfully");
    }

    private async Task SeedGarciaRegistrationAsync(CampEdition edition, AgeRanges ranges, DateTime now)
    {
        var familyUnit = await db.FamilyUnits
            .FirstOrDefaultAsync(f => f.Name == "Garcia Family");
        var registeredBy = await db.Users
            .FirstOrDefaultAsync(u => u.Email == "member1@abuvi.local");

        if (familyUnit is null || registeredBy is null)
        {
            Log.Warning("Registrations: skipped Garcia (family or user not found)");
            return;
        }

        var members = await db.FamilyMembers
            .Where(m => m.FamilyUnitId == familyUnit.Id)
            .ToListAsync();

        var registrationMembers = new List<RegistrationMember>();
        decimal totalAmount = 0;

        foreach (var member in members)
        {
            var age = CalculateAge(member.DateOfBirth, edition.StartDate);
            var category = GetAgeCategory(age, ranges);
            if (category is null) continue;

            var price = GetPrice(category.Value, edition);
            totalAmount += price;

            registrationMembers.Add(new RegistrationMember
            {
                Id = Guid.NewGuid(),
                FamilyMemberId = member.Id,
                AgeAtCamp = age,
                AgeCategory = category.Value,
                IndividualAmount = price,
                AttendancePeriod = AttendancePeriod.Complete,
                CreatedAt = now,
            });
        }

        var registrationId = Guid.NewGuid();
        foreach (var m in registrationMembers)
            m.RegistrationId = registrationId;

        var registration = new Registration
        {
            Id = registrationId,
            FamilyUnitId = familyUnit.Id,
            CampEditionId = edition.Id,
            RegisteredByUserId = registeredBy.Id,
            BaseTotalAmount = totalAmount,
            ExtrasAmount = 0,
            TotalAmount = totalAmount,
            Status = RegistrationStatus.Pending,
            Members = registrationMembers,
            StatusHistory =
            [
                new RegistrationStatusHistory
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = registrationId,
                    PreviousStatus = RegistrationStatus.Pending,
                    NewStatus = RegistrationStatus.Pending,
                    ChangedAt = now,
                    Trigger = StatusChangeTrigger.Automatic,
                }
            ],
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Registrations.Add(registration);
        await db.SaveChangesAsync();
    }

    private async Task SeedLopezRegistrationAsync(CampEdition edition, AgeRanges ranges, DateTime now)
    {
        var familyUnit = await db.FamilyUnits
            .FirstOrDefaultAsync(f => f.Name == "Lopez Family");
        var registeredBy = await db.Users
            .FirstOrDefaultAsync(u => u.Email == "board@abuvi.local");

        if (familyUnit is null || registeredBy is null)
        {
            Log.Warning("Registrations: skipped Lopez (family or user not found)");
            return;
        }

        var member = await db.FamilyMembers
            .FirstOrDefaultAsync(m =>
                m.FamilyUnitId == familyUnit.Id &&
                m.FirstName == "Ana" &&
                m.LastName == "Lopez");

        if (member is null)
        {
            Log.Warning("Registrations: skipped Lopez (Ana Lopez not found)");
            return;
        }

        var age = CalculateAge(member.DateOfBirth, edition.StartDate);
        var category = GetAgeCategory(age, ranges) ?? AgeCategory.Adult;
        var price = GetPrice(category, edition);

        var registrationId = Guid.NewGuid();

        var registration = new Registration
        {
            Id = registrationId,
            FamilyUnitId = familyUnit.Id,
            CampEditionId = edition.Id,
            RegisteredByUserId = registeredBy.Id,
            BaseTotalAmount = price,
            ExtrasAmount = 0,
            TotalAmount = price,
            Status = RegistrationStatus.Confirmed,
            Members =
            [
                new RegistrationMember
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = registrationId,
                    FamilyMemberId = member.Id,
                    AgeAtCamp = age,
                    AgeCategory = category,
                    IndividualAmount = price,
                    AttendancePeriod = AttendancePeriod.Complete,
                    CreatedAt = now,
                }
            ],
            Payments =
            [
                new Payment
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = registrationId,
                    Amount = price,
                    PaymentDate = now.AddDays(-30),
                    Method = PaymentMethod.Transfer,
                    Status = PaymentStatus.Completed,
                    InstallmentNumber = 1,
                    IsManual = false,
                    CreatedAt = now.AddDays(-30),
                    UpdatedAt = now,
                }
            ],
            StatusHistory =
            [
                new RegistrationStatusHistory
                {
                    Id = Guid.NewGuid(),
                    RegistrationId = registrationId,
                    PreviousStatus = RegistrationStatus.Pending,
                    NewStatus = RegistrationStatus.Confirmed,
                    ChangedAt = now.AddDays(-29),
                    Trigger = StatusChangeTrigger.AdminAction,
                }
            ],
            CreatedAt = now.AddDays(-30),
            UpdatedAt = now,
        };

        db.Registrations.Add(registration);
        await db.SaveChangesAsync();
    }

    // Inlined from RegistrationPricingService — same logic, no DI required
    private static int CalculateAge(DateOnly dateOfBirth, DateTime campStartDate)
    {
        var campDate = DateOnly.FromDateTime(campStartDate);
        var age = campDate.Year - dateOfBirth.Year;
        if (campDate < dateOfBirth.AddYears(age)) age--;
        return age;
    }

    private static AgeCategory? GetAgeCategory(int age, AgeRanges ranges)
    {
        if (age >= 0 && age <= ranges.BabyMaxAge) return AgeCategory.Baby;
        if (age >= ranges.ChildMinAge && age <= ranges.ChildMaxAge) return AgeCategory.Child;
        if (age >= ranges.AdultMinAge) return AgeCategory.Adult;
        return null;
    }

    private static decimal GetPrice(AgeCategory category, CampEdition edition)
        => category switch
        {
            AgeCategory.Adult => edition.PricePerAdult,
            AgeCategory.Child => edition.PricePerChild,
            AgeCategory.Baby => edition.PricePerBaby,
            _ => 0m,
        };
}
