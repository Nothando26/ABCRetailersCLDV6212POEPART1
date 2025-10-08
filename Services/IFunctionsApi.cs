using System.Collections.Generic;
using System.Threading.Tasks;
using ABCRetailersPOEPART1.Models;
using Microsoft.AspNetCore.Http;
using static ABCRetailersPOEPART1.Services.FunctionsApiClient;

namespace ABCRetailersPOEPART1.Services
{
    public interface IFunctionsApi
    {
        // Customers
        Task<List<Customer>> GetCustomersAsync();
        Task<Customer?> GetCustomerAsync(string id);
        Task<Customer> CreateCustomerAsync(Customer customer);
        Task<Customer> UpdateCustomerAsync(string id, Customer customer);
        Task DeleteCustomerAsync(string id);

        // Products
        Task<List<Product>> GetProductsAsync();
        Task<Product?> GetProductAsync(string id);
        Task<Product> CreateProductAsync(Product product, IFormFile? imageFile = null);
        Task<Product> UpdateProductAsync(string id, Product product, IFormFile? imageFile = null);
        Task DeleteProductAsync(string id);

        // Orders
        Task<List<Order>> GetOrdersAsync();
        Task<Order?> GetOrderAsync(string id);
        Task<Order> CreateOrderAsync(string customerId, string productId, int quantity);
        Task UpdateOrderStatusAsync(string id, string newStatus);
        Task DeleteOrderAsync(string id);

        // Uploads
        Task<UploadResponse> UploadProofOfPaymentAsync(IFormFile file, string? orderId = null, string? customerName = null);

    }
}
