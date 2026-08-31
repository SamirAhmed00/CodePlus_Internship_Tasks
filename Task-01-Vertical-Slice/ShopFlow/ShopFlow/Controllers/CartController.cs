using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShopFlow.Features.AddToCart;

namespace ShopFlow.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CartController : ControllerBase
    {
        private readonly AddToCartHandler _handler;

        public CartController(AddToCartHandler handler)
        {
            _handler = handler;
        }

        [HttpPost("items")]
        public async Task<IActionResult> AddToCart(
            AddToCartRequest request)
        {
            try
            {
                var response = await _handler.HandleAsync(request);

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new
                {
                    message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }
    }
}
