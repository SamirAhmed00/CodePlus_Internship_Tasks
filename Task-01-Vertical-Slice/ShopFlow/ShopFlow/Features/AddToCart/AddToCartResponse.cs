namespace ShopFlow.Features.AddToCart
{
    public class AddToCartResponse
    {
        public int CartId { get; set; }

        public int ProductId { get; set; }

        public int Quantity { get; set; }

        public decimal TotalPrice { get; set; }

        public string Message { get; set; } = string.Empty;

    }
}
