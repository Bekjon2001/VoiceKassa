using Microsoft.EntityFrameworkCore;
using VoiceKassa.Domain.Entities;

namespace VoiceKassa.DataLayer;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<Staff> StaffMembers => Set<Staff>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("voicekassa");

        // Kalit, ustun nomlari, uzunlik va decimal aniqligi har bir entity
        // faylida [Key]/[Column]/[MaxLength]/[ForeignKey] atributlari orqali
        // beriladi (VoiceKassa.Domain.Entities/*.cs). Bu yerda faqat
        // atribut orqali ifodalab bo'lmaydigan narsalar qoladi.

        modelBuilder.Entity<Product>(e =>
        {
            // Aliases vergul bilan ajratilgan matn sifatida saqlanadi.
            e.Property(x => x.Aliases)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());

            e.HasIndex(x => new { x.BusinessId, x.Name });
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.CreatedAt });
            e.HasIndex(x => new { x.BusinessId, x.Status });
        });

        modelBuilder.Entity<Table>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.Status });
        });

        modelBuilder.Entity<InventoryTransaction>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.ProductId, x.CreatedAt });
        });
    }
}
