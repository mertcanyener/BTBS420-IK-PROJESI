using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Identity;

public sealed class LookupDataSeeder(ApplicationDbContext dbContext)
{
    private static readonly IReadOnlyList<(string DepartmentName, string[] PositionNames)> Departments =
    [
        ("Yazılım Geliştirme", ["Yazılım Geliştirici", "Kıdemli Yazılım Geliştirici"]),
        ("İnsan Kaynakları", ["İK Uzmanı", "İşe Alım Uzmanı"]),
        ("Satış", ["Satış Temsilcisi", "Satış Müdürü"]),
        ("Pazarlama", ["Pazarlama Uzmanı", "Dijital Pazarlama Uzmanı"]),
        ("Finans", ["Finans Uzmanı", "Muhasebe Uzmanı"]),
        ("Operasyon", ["Operasyon Uzmanı", "Operasyon Müdürü"])
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.Departments.AnyAsync(cancellationToken))
        {
            return;
        }

        var createdDepartments = new List<(Department Department, string[] PositionNames)>();

        foreach (var (departmentName, positionNames) in Departments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var department = new Department(departmentName);
            dbContext.Departments.Add(department);
            createdDepartments.Add((department, positionNames));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var (department, positionNames) in createdDepartments)
        {
            foreach (var positionName in positionNames)
            {
                dbContext.Positions.Add(
                    new Position(positionName, department.Id, jobFamilyId: null, seniorityId: null));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
