using Microsoft.EntityFrameworkCore;
using VoiceKassa.Domain.Entities;

namespace VoiceKassa.DataLayer;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<Cashier> Cashiers => Set<Cashier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Shop>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Cashier>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Shop).WithMany(s => s.Cashiers).HasForeignKey(x => x.ShopId);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Shop).WithMany(s => s.Products).HasForeignKey(x => x.ShopId);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            // Aliases stored as a simple comma-separated string via converter
            // (Postgres text[] can be used instead once traffic justifies it).
            e.Property(x => x.Aliases)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
            e.Property(x => x.DefaultPrice).HasColumnType("numeric(14,2)");
            e.Property(x => x.StockQuantity).HasColumnType("numeric(14,3)");
            e.HasIndex(x => new { x.ShopId, x.Name });
        });

        modelBuilder.Entity<Sale>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Shop).WithMany(s => s.Sales).HasForeignKey(x => x.ShopId);
            e.HasOne(x => x.Cashier).WithMany().HasForeignKey(x => x.CashierId).IsRequired(false);
            e.Property(x => x.TotalAmount).HasColumnType("numeric(14,2)");
            e.Property(x => x.TranscriptText).HasMaxLength(1000);
            e.HasIndex(x => new { x.ShopId, x.CreatedAt });
        });

        modelBuilder.Entity<SaleItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Sale).WithMany(s => s.Items).HasForeignKey(x => x.SaleId);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).IsRequired(false);
            e.Property(x => x.Quantity).HasColumnType("numeric(14,3)");
            e.Property(x => x.LineTotal).HasColumnType("numeric(14,2)");
            e.Property(x => x.ProductNameSpoken).IsRequired().HasMaxLength(200);
        });
    }
}
