using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Data;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options) : DbContext(options);
