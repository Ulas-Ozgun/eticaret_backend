namespace bitirme_projesi.Models
{
    public class RecentView
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ProductId { get; set; }
        public DateTime ViewedAt { get; set; }

        public Product? Product { get; set; }
        public User? User { get; set; }
    }
}
