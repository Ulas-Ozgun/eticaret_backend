using Microsoft.EntityFrameworkCore;
using bitirme_projesi.Models;




namespace bitirme_projesi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Favorite> Favorites { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Beden> beden { get; set; }
        public DbSet<UrunBeden> urun_beden { get; set; }
        public DbSet<Numara> numara { get; set; }
        public DbSet<UrunNumara> urun_numara { get; set; }
        public DbSet<RecentView> RecentViews { get; set; }





    }
}
