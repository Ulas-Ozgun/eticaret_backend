namespace bitirme_projesi.Models
{
    public class AddCartItemDto
    {
        public int UserId { get; set; }
        public int CartId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
