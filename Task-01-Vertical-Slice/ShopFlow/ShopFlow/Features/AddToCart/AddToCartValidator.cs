namespace ShopFlow.Features.AddToCart
{
    public class AddToCartValidator
    {
        public List<string> Validate(AddToCartRequest request)
        {
            var errors = new List<string>();

            if (request.ProductId <= 0)
            {
                errors.Add("ProductId must be greater than 0.");
            }

            if (request.Quantity <= 0)
            {
                errors.Add("Quantity must be greater than 0.");
            }

            return errors;
        }

    }
}
