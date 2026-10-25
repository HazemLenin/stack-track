using Microsoft.EntityFrameworkCore;

namespace StackTrack.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options);
