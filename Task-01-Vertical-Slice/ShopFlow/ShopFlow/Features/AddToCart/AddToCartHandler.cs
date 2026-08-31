using ShopFlow.Models;

namespace ShopFlow.Features.AddToCart
{
    public class AddToCartHandler
    {
        private readonly AddToCartValidator _validator;

        // Simple in-memory data for the task.
        // This simulates products and cart storage.
        private static readonly List<Product> Products =
        [
            new Product
        {
            Id = 1,
            Name = "Laptop",
            Price = 30000m,
            Stock = 10
        },
        new Product
        {
            Id = 2,
            Name = "Mouse",
            Price = 1000m,
            Stock = 20
        },
        new Product
        {
            Id = 3,
            Name = "Keyboard",
            Price = 2000m,
            Stock = 5
        }
        ];

        private static readonly Cart Cart = new()
        {
            Id = 1
        };

        public AddToCartHandler(AddToCartValidator validator)
        {
            _validator = validator;
        }

        public Task<AddToCartResponse> HandleAsync(
            AddToCartRequest request)
        {
            // 1. Validate request
            var validationErrors = _validator.Validate(request);

            if (validationErrors.Count > 0)
            {
                throw new ArgumentException(
                    string.Join(" ", validationErrors));
            }

            // 2. Find product
            var product = Products.FirstOrDefault(
                p => p.Id == request.ProductId);

            if (product is null)
            {
                throw new KeyNotFoundException(
                    "Product was not found.");
            }

            // 3. Check stock
            if (product.Stock < request.Quantity)
            {
                throw new InvalidOperationException(
                    "Not enough stock available.");
            }

            // 4. Find existing cart item
            var existingItem = Cart.Items.FirstOrDefault(
                item => item.ProductId == request.ProductId);

            if (existingItem is not null)
            {
                existingItem.Quantity += request.Quantity;
            }
            else
            {
                Cart.Items.Add(new CartItem
                {
                    Id = Cart.Items.Count + 1,
                    ProductId = product.Id,
                    Quantity = request.Quantity,
                    UnitPrice = product.Price
                });
            }

            // 5. Update stock
            product.Stock -= request.Quantity;

            // 6. Calculate total price
            var totalPrice = Cart.Items.Sum(
                item => item.Quantity * item.UnitPrice);

            // 7. Prepare response
            var response = new AddToCartResponse
            {
                CartId = Cart.Id,
                ProductId = product.Id,
                Quantity = request.Quantity,
                TotalPrice = totalPrice,
                Message = "Product added to cart successfully."
            };

            return Task.FromResult(response);
        }
    }
}
