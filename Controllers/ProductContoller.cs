
using ABCRetailersPOEPART1.Models;
using Microsoft.AspNetCore.Mvc;
using ABCRetailersPOEPART1.Services;

namespace ABCRetailersPOEPART1.Controllers
{
    public class ProductController : Controller
    {
        private readonly IFunctionsApi _api;
        private readonly ILogger<ProductController> _logger;

        public ProductController(IFunctionsApi api, ILogger<ProductController> logger)
        {
            _api = api;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _api.GetProductsAsync();
            return View(products);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
        {
            // Parse Price from form manually if needed
            if (Request.Form.TryGetValue("Price", out var priceFormValue) &&
                double.TryParse(priceFormValue, out var parsedPrice))
            {
                product.Price = parsedPrice;
            }

            if (!ModelState.IsValid) return View(product);

            try
            {
                // Create product with optional image
                var createdProduct = await _api.CreateProductAsync(product, imageFile);

                TempData["Success"] = $"Product '{createdProduct.ProductName}' created successfully with price {createdProduct.Price}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                ModelState.AddModelError("", $"Error creating product: {ex.Message}");
                return View(product);
            }
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var product = await _api.GetProductAsync(id);
            if (product == null) return NotFound();

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Product product, IFormFile? imageFile)
        {
            // Parse Price from form manually if needed
            if (Request.Form.TryGetValue("Price", out var priceFormValue) &&
                double.TryParse(priceFormValue, out var parsedPrice))
            {
                product.Price = parsedPrice;
            }

            if (!ModelState.IsValid) return View(product);

            try
            {
                // Update product with optional image
                await _api.UpdateProductAsync(product.ProductId, product, imageFile);

                TempData["Success"] = "Product updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product");
                ModelState.AddModelError("", $"Error updating product: {ex.Message}");
                return View(product);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _api.DeleteProductAsync(id);
                TempData["Success"] = "Product deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting product: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

