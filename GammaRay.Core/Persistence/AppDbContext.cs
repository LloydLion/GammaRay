using GammaRay.Core.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace GammaRay.Core.Persistence;

public class AppDbContext : DbContext
{
	public DbSet<SiteProfileModel> Routes { get; set; } = null!;

	public DbSet<NetworkModel> Networks { get; set; } = null!;


	public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
	{

	}

#if DEBUG
	public AppDbContext() : this(
		new DbContextOptionsBuilder<AppDbContext>()
			.UseSqlite()
			.Options
		)
	{ }
#endif


	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<SiteProfileModel>(b =>
		{
			b.HasKey(e => new { e.SiteDomain, e.ProfileName });
			b.Property(e => e.ConfigurationName).IsRequired();
			b.Property(e => e.ValidUntil).IsRequired();
		});

		modelBuilder.Entity<NetworkModel>(b =>
		{
			b.HasKey(e => e.Identity);
			b.Property(e => e.UsedProfile);
		});
	}
}
