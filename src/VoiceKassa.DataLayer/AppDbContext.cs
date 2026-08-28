using Microsoft.EntityFrameworkCore;
using VoiceKassa.Domain.Entities;

namespace VoiceKassa.DataLayer;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<Staff> StaffMembers => Set<Staff>();
    public DbSet<SalaryHistory> SalaryHistories => Set<SalaryHistory>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<RestaurantOwner> RestaurantOwners => Set<RestaurantOwner>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

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

        modelBuilder.Entity<Staff>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.Role });
            e.HasMany(x => x.SalaryHistory)
                .WithOne(h => h!.Staff)
                .HasForeignKey(h => h.StaffId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Table>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.Status });
        });

        modelBuilder.Entity<InventoryTransaction>(e =>
        {
            e.HasIndex(x => new { x.BusinessId, x.ProductId, x.CreatedAt });
        });

        modelBuilder.Entity<RestaurantOwner>(e =>
        {
            e.HasIndex(x => x.Login).IsUnique();
            e.HasIndex(x => x.AccessToken).IsUnique();
            e.HasIndex(x => x.BusinessId).IsUnique();
        });

        modelBuilder.Entity<UserAccount>(e =>
        {
            e.HasIndex(x => x.Login).IsUnique();
        });
    }
}
