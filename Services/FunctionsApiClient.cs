using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ABCRetailersPOEPART1.Models;
using Microsoft.AspNetCore.Http;

namespace ABCRetailersPOEPART1.Services
{
    public class FunctionsApiClient : IFunctionsApi
    {
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

        private const string CustomersRoute = "customers";
        private const string ProductsRoute = "products";
        private const string OrdersRoute = "orders";
        private const string UploadsRoute = "uploads/proof-of-payment"; // Multipart

        public FunctionsApiClient(IHttpClientFactory factory)
        {
            _http = factory.CreateClient("Functions"); // BaseAddress configured in Program.cs
        }

        private static HttpContent JsonBody(object obj) =>
            new StringContent(JsonSerializer.Serialize(obj, _json), Encoding.UTF8, "application/json");

        private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage resp)
        {
            resp.EnsureSuccessStatusCode();
            await using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var data = await JsonSerializer.DeserializeAsync<T>(stream, _json).ConfigureAwait(false);
            if (data is null)
                throw new InvalidOperationException("Failed to deserialize response.");
            return data;
        }

        // ---------------------- Customers ----------------------
        public async Task<List<Customer>> GetCustomersAsync() =>
            await ReadJsonAsync<List<Customer>>(await _http.GetAsync(CustomersRoute).ConfigureAwait(false));

        public async Task<Customer?> GetCustomerAsync(string id)
        {
            var resp = await _http.GetAsync($"{CustomersRoute}/{id}").ConfigureAwait(false);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            return await ReadJsonAsync<Customer>(resp).ConfigureAwait(false);
        }

        public async Task<Customer> CreateCustomerAsync(Customer customer) =>
            await ReadJsonAsync<Customer>(await _http.PostAsync(CustomersRoute, JsonBody(new
            {
                name = customer.Name,
                surname = customer.Surname,
                username = customer.Username,
                email = customer.Email,
                shippingAddress = customer.ShippingAddress
            })).ConfigureAwait(false));

        public async Task<Customer> UpdateCustomerAsync(string id, Customer customer) =>
            await ReadJsonAsync<Customer>(await _http.PutAsync($"{CustomersRoute}/{id}", JsonBody(new
            {
                name = customer.Name,
                surname = customer.Surname,
                username = customer.Username,
                email = customer.Email,
                shippingAddress = customer.ShippingAddress
            })).ConfigureAwait(false));

        public async Task DeleteCustomerAsync(string id) =>
            (await _http.DeleteAsync($"{CustomersRoute}/{id}").ConfigureAwait(false)).EnsureSuccessStatusCode();

        // ---------------------- Products ----------------------
        public async Task<List<Product>> GetProductsAsync() =>
            await ReadJsonAsync<List<Product>>(await _http.GetAsync(ProductsRoute).ConfigureAwait(false));

        public async Task<Product?> GetProductAsync(string id)
        {
            var resp = await _http.GetAsync($"{ProductsRoute}/{id}").ConfigureAwait(false);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            return await ReadJsonAsync<Product>(resp).ConfigureAwait(false);
        }

        public async Task<Product> CreateProductAsync(Product product, IFormFile? imageFile = null)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(product.ProductName), "ProductName");
            form.Add(new StringContent(product.Description ?? string.Empty), "Description");
            form.Add(new StringContent(product.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)), "Price");
            form.Add(new StringContent(product.StockAvailable.ToString(System.Globalization.CultureInfo.InvariantCulture)), "StockAvailable");
            if (!string.IsNullOrWhiteSpace(product.ImageUrl))
                form.Add(new StringContent(product.ImageUrl), "ImageUrl");

            if (imageFile != null)
            {
                var fileContent = new StreamContent(imageFile.OpenReadStream());
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(imageFile.ContentType ?? "application/octet-stream");
                form.Add(fileContent, "ImageFile", imageFile.FileName);
            }

            var response = await _http.PostAsync(ProductsRoute, form).ConfigureAwait(false);
            return await ReadJsonAsync<Product>(response).ConfigureAwait(false);
        }

        public async Task<Product> UpdateProductAsync(string id, Product p, IFormFile? imageFile = null)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(p.ProductName), "ProductName");
            form.Add(new StringContent(p.Description ?? string.Empty), "Description");
            form.Add(new StringContent(p.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)), "Price");
            form.Add(new StringContent(p.StockAvailable.ToString(System.Globalization.CultureInfo.InvariantCulture)), "StockAvailable");
            if (!string.IsNullOrWhiteSpace(p.ImageUrl))
                form.Add(new StringContent(p.ImageUrl), "ImageUrl");

            if (imageFile != null)
            {
                var fileContent = new StreamContent(imageFile.OpenReadStream());
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(imageFile.ContentType ?? "application/octet-stream");
                form.Add(fileContent, "ImageFile", imageFile.FileName);
            }

            var response = await _http.PutAsync($"{ProductsRoute}/{id}", form).ConfigureAwait(false);
            return await ReadJsonAsync<Product>(response).ConfigureAwait(false);
        }

        public async Task DeleteProductAsync(string id) =>
            (await _http.DeleteAsync($"{ProductsRoute}/{id}").ConfigureAwait(false)).EnsureSuccessStatusCode();

        // ---------------------- Orders ----------------------
        public async Task<List<Order>> GetOrdersAsync()
        {
            var dtos = await ReadJsonAsync<List<OrderDto>>(await _http.GetAsync(OrdersRoute).ConfigureAwait(false));
            return dtos.ConvertAll(ToOrder);
        }

        public async Task<Order?> GetOrderAsync(string id)
        {
            var resp = await _http.GetAsync($"{OrdersRoute}/{id}").ConfigureAwait(false);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            var dto = await ReadJsonAsync<OrderDto>(resp).ConfigureAwait(false);
            return ToOrder(dto);
        }

        public async Task<Order> CreateOrderAsync(string customerId, string productId, int quantity)
        {
            var payload = new { customerId, productId, quantity };
            var dto = await ReadJsonAsync<OrderDto>(await _http.PostAsync(OrdersRoute, JsonBody(payload)).ConfigureAwait(false));
            return ToOrder(dto);
        }

        public async Task UpdateOrderStatusAsync(string id, string newStatus)
        {
            var payload = new { status = newStatus };
            var response = await _http.PatchAsync($"{OrdersRoute}/{id}/status", JsonBody(payload)).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteOrderAsync(string id) =>
            (await _http.DeleteAsync($"{OrdersRoute}/{id}").ConfigureAwait(false)).EnsureSuccessStatusCode();

        // ---------------------- Uploads ----------------------
        public async Task<UploadResponse> UploadProofOfPaymentAsync(IFormFile file, string? orderId = null, string? customerName = null)
        {
            using var form = new MultipartFormDataContent();
            var fileContent = new StreamContent(file.OpenReadStream());
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
            form.Add(fileContent, "ProofOfPayment", file.FileName);

            if (!string.IsNullOrWhiteSpace(customerName))
                form.Add(new StringContent(customerName), "CustomerName");
            if (!string.IsNullOrWhiteSpace(orderId))
                form.Add(new StringContent(orderId), "OrderId");

            var resp = await _http.PostAsync(UploadsRoute, form).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var uploadResult = await ReadJsonAsync<UploadResponse>(resp).ConfigureAwait(false);
            return uploadResult;
        }

        private static Order ToOrder(OrderDto d)
        {
            return new Order
            {
                RowKey = d.Id,
                CustomerId = d.CustomerId,
                ProductId = d.ProductId,
                ProductName = d.ProductName,
                Quantity = d.Quantity,
                UnitPrice = (double)d.UnitPrice,
                TotalPrice = (double)d.UnitPrice * d.Quantity,
                OrderDate = d.OrderDateUtc,
                Status = d.Status
            };
        }

        private sealed record OrderDto(
            string Id,
            string CustomerId,
            string ProductId,
            string ProductName,
            int Quantity,
            decimal UnitPrice,
            DateTimeOffset OrderDateUtc,
            string Status
        );

        public sealed class UploadResponse
        {
            public string FileName { get; set; } = default!;
            public string BlobUrl { get; set; } = default!;
        }
    }

    internal static class HttpClientPatchExtensions
    {
        public static Task<HttpResponseMessage> PatchAsync(this HttpClient client, string requestUri, HttpContent content) =>
            client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, requestUri) { Content = content });
    }
}
