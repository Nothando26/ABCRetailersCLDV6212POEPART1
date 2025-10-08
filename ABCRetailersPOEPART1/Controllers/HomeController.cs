using System.Diagnostics;
using ABCRetailersPOEPART1.Models;
using ABCRetailersPOEPART1.Models.ViewModels;
using ABCRetailersPOEPART1.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetailersPOEPART1.Controllers
{
    public class HomeController : Controller
    {
        private readonly IFunctionsApi _api;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IFunctionsApi api, ILogger<HomeController> logger)
        {
            _api = api;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _api.GetProductsAsync();
            var customers = await _api.GetCustomersAsync();
            var orders = await _api.GetOrdersAsync();

            var viewModel = new HomeViewModel
            {
                FeaturedProducts = products.Take(5).ToList(),
                ProductCount = products.Count,
                CustomerCount = customers.Count,
                OrderCount = orders.Count
            };
            return View(viewModel);
        }

        public IActionResult Privacy() => View();
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
 }
