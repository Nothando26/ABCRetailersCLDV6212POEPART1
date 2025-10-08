using ABCRetailersPOEPART1.Models;
using ABCRetailersPOEPART1.Models.ViewModels;
using ABCRetailersPOEPART1.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ABCRetailersPOEPART1.Controllers
{
    public class OrderController : Controller
    {
        private readonly IFunctionsApi _api;

        public OrderController(IFunctionsApi api) => _api = api;

        public async Task<IActionResult> Index()
        {
            var orders = await _api.GetOrdersAsync();
            return View(orders.OrderByDescending(o => o.OrderDate).ToList());
        }

        public async Task<IActionResult> Create()
        {
            var customers = await _api.GetCustomersAsync();
            var products = await _api.GetProductsAsync();

            var viewModel = new OrderCreateViewModel
            {
                Customers = customers,
                Products = products
            };
            return View(viewModel);
        }

        [HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(OrderCreateViewModel model)
{
    if (!ModelState.IsValid)
    {
        await PopulateDropDowns(model);
        return View(model);
    }

    try
    {
        // Fetch by RowKey (ID)
        var customer = await _api.GetCustomerAsync(model.CustomerId);
        var product = await _api.GetProductAsync(model.ProductId);

        if (customer == null || product == null)
        {
            ModelState.AddModelError("", "Invalid customer or product selected.");
            await PopulateDropDowns(model);
            return View(model);
        }

        if (product.StockAvailable < model.Quantity)
        {
            ModelState.AddModelError("Quantity", $"Insufficient stock. Available: {product.StockAvailable}");
            await PopulateDropDowns(model);
            return View(model);
        }

        // Create order
        var order = await _api.CreateOrderAsync(model.CustomerId, model.ProductId, model.Quantity);

        // Update product stock
        product.StockAvailable -= model.Quantity;
        await _api.UpdateProductAsync(product.ProductId, product, null);

        TempData["Success"] = "Order created successfully!";
        return RedirectToAction(nameof(Index));
    }
    catch (Exception ex)
    {
        ModelState.AddModelError("", $"Error creating order: {ex.Message}");
    }

    await PopulateDropDowns(model);
    return View(model);
}


        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var order = await _api.GetOrderAsync(id);
            if (order == null) return NotFound();

            return View(order);
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var order = await _api.GetOrderAsync(id);
            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Order order)
        {
            if (!ModelState.IsValid) return View(order);

            try
            {
                await _api.UpdateOrderStatusAsync(id, order.Status);
                TempData["Success"] = "Order status updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error updating order: {ex.Message}");
            }

            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _api.DeleteOrderAsync(id);
                TempData["Success"] = "Order deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting order: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropDowns(OrderCreateViewModel model)
        {
            model.Customers = await _api.GetCustomersAsync();
            model.Products = await _api.GetProductsAsync();
        }
    }
}

