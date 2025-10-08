using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using abcretailerspart2.Functions.Entities;
using abcretailerspart2.Functions.Helpers;
using abcretailerspart2.Functions.Models;

namespace abcretailerspart2.Functions.Functions
{
    public class OrdersFunctions
    {
        private readonly string _conn;
        private readonly string _ordersTable;
        private readonly string _productsTable;
        private readonly string _customersTable;
        private readonly string _queueOrder;
        private readonly string _queueStock;

        public OrdersFunctions(IConfiguration cfg)
        {
            _conn = cfg["STORAGE_CONNECTION"] ?? throw new InvalidOperationException("STORAGE_CONNECTION missing");
            _ordersTable = cfg["TABLE_ORDER"] ?? "Order";
            _productsTable = cfg["TABLE_PRODUCT"] ?? "Product";
            _customersTable = cfg["TABLE_CUSTOMER"] ?? "Customer";
            _queueOrder = cfg["QUEUE_ORDER_NOTIFICATIONS"] ?? "order-notifications";
            _queueStock = cfg["QUEUE_STOCK_UPDATES"] ?? "stock-updates";
        }

        private TableClient GetTable(string name) => new TableClient(_conn, name);

        public record OrderCreate(string CustomerId, string ProductId, int Quantity);
        public record OrderStatusUpdate(string Status);

        [Function("Orders_List")]
        public async Task<HttpResponseData> ListOrders(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")] HttpRequestData req)
        {
            var table = GetTable(_ordersTable);
            await table.CreateIfNotExistsAsync();

            var items = new List<OrderDto>();
            await foreach (var e in table.QueryAsync<OrderEntity>(x => x.PartitionKey == "Order"))
                items.Add(Map.ToDto(e));

            var ordered = items.OrderByDescending(o => o.OrderDateUtc).ToList();
            return await HttpJson.OkAsync(req, ordered);
        }

        [Function("Orders_Get")]
        public async Task<HttpResponseData> GetOrder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{id}")] HttpRequestData req,
            string id)
        {
            var table = GetTable(_ordersTable);
            try
            {
                var e = await table.GetEntityAsync<OrderEntity>("Order", id);
                return await HttpJson.OkAsync(req, Map.ToDto(e.Value));
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return await HttpJson.NotFoundAsync(req, "Order not found");
            }
            catch
            {
                return await HttpJson.ServerErrorAsync(req, "Error retrieving order");
            }
        }

        [Function("Orders_Create")]
        public async Task<HttpResponseData> CreateOrder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequestData req)
        {
            OrderCreate? input;
            try
            {
                input = await HttpJson.ReadAsync<OrderCreate>(req);
            }
            catch
            {
                return await HttpJson.BadAsync(req, "Invalid request body");
            }

            if (input is null ||
                string.IsNullOrWhiteSpace(input.CustomerId) ||
                string.IsNullOrWhiteSpace(input.ProductId) ||
                input.Quantity < 1)
            {
                return await HttpJson.BadAsync(req, "CustomerId, ProductId and Quantity (>=1) are required");
            }

            var orders = GetTable(_ordersTable);
            var products = GetTable(_productsTable);
            var customers = GetTable(_customersTable);

            await orders.CreateIfNotExistsAsync();
            await products.CreateIfNotExistsAsync();
            await customers.CreateIfNotExistsAsync();

            ProductEntity? product = null;
            CustomerEntity? customer = null;

            // Try to find Product: first by direct GetEntity (fast), then by querying ProductName or RowKey
            try
            {
                product = (await products.GetEntityAsync<ProductEntity>("Product", input.ProductId)).Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // fallback: query by ProductName or RowKey
                await foreach (var p in products.QueryAsync<ProductEntity>(x =>
                           x.PartitionKey == "Product" &&
                           (x.RowKey == input.ProductId || x.ProductName == input.ProductId)))
                {
                    product = p;
                    break;
                }

                if (product is null)
                {
                    return await HttpJson.BadAsync(req, "Invalid ProductId");
                }
            }
            catch (Exception)
            {
                return await HttpJson.ServerErrorAsync(req, "Error accessing products table");
            }

            // Try to find Customer: first by direct GetEntity (fast), then by querying Email/Username/RowKey
            try
            {
                customer = (await customers.GetEntityAsync<CustomerEntity>("Customer", input.CustomerId)).Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // fallback: query by RowKey, Email, Username
                await foreach (var c in customers.QueryAsync<CustomerEntity>(x =>
                           x.PartitionKey == "Customer" &&
                           (x.RowKey == input.CustomerId || x.Email == input.CustomerId || x.Username == input.CustomerId)))
                {
                    customer = c;
                    break;
                }

                if (customer is null)
                {
                    return await HttpJson.BadAsync(req, "Invalid CustomerId");
                }
            }
            catch (Exception)
            {
                return await HttpJson.ServerErrorAsync(req, "Error accessing customers table");
            }

            // At this point we should have product & customer
            if (product is null || customer is null)
            {
                // defensive check; should not reach here due to earlier returns
                return await HttpJson.BadAsync(req, "Invalid customer or product");
            }

            // Stock validation
            if (product.StockAvailable < input.Quantity)
                return await HttpJson.BadAsync(req, $"Insufficient stock. Available: {product.StockAvailable}");

            // Create order
            var order = new OrderEntity
            {
                PartitionKey = "Order",
                RowKey = Guid.NewGuid().ToString(),
                CustomerId = customer.RowKey,
                ProductId = product.RowKey,
                ProductName = product.ProductName,
                Quantity = input.Quantity,
                UnitPrice = product.Price,
                OrderDateUtc = DateTimeOffset.UtcNow,
                Status = "Pending"
            };

            try
            {
                await orders.AddEntityAsync(order);
            }
            catch
            {
                return await HttpJson.ServerErrorAsync(req, "Failed to create order");
            }

            // Update stock - read current ETag was already in 'product' object
            try
            {
                product.StockAvailable -= input.Quantity;
                if (product.StockAvailable < 0) product.StockAvailable = 0; // defensive
                await products.UpdateEntityAsync(product, product.ETag, TableUpdateMode.Replace);
            }
            catch
            {
                // If we fail to update stock, we might want to roll back the created order in a more complete system.
                // For now, return ServerError and let ops investigate.
                return await HttpJson.ServerErrorAsync(req, "Order created but failed to update product stock");
            }

            // Optionally add a message to stock-update queue (non-blocking best-effort)
            try
            {
                var queueStockClient = new QueueClient(_conn, _queueStock, new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
                await queueStockClient.CreateIfNotExistsAsync();
                var stockMsg = new
                {
                    Type = "StockUpdated",
                    ProductId = product.RowKey,
                    NewStock = product.StockAvailable,
                    UpdatedDateUtc = DateTimeOffset.UtcNow
                };
                await queueStockClient.SendMessageAsync(JsonSerializer.Serialize(stockMsg));
            }
            catch
            {
                // best-effort; swallow
            }

            return await HttpJson.CreatedAsync(req, Map.ToDto(order));
        }


        [Function("Orders_UpdateStatus")]
        public async Task<HttpResponseData> UpdateOrderStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "orders/{id}/status")] HttpRequestData req,
            string id)
        {
            OrderStatusUpdate? input;
            try
            {
                input = await HttpJson.ReadAsync<OrderStatusUpdate>(req);
            }
            catch
            {
                return await HttpJson.BadAsync(req, "Invalid request body");
            }

            if (input is null || string.IsNullOrWhiteSpace(input.Status))
                return await HttpJson.BadAsync(req, "Status is required");

            var orders = GetTable(_ordersTable);

            try
            {
                var resp = await orders.GetEntityAsync<OrderEntity>("Order", id);
                var e = resp.Value;
                var previous = e.Status;

                e.Status = input.Status;
                await orders.UpdateEntityAsync(e, e.ETag, TableUpdateMode.Replace);

                var queueOrderClient = new QueueClient(_conn, _queueOrder, new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
                await queueOrderClient.CreateIfNotExistsAsync();

                var statusMsg = new
                {
                    Type = "OrderStatusUpdated",
                    OrderId = e.RowKey,
                    PreviousStatus = previous,
                    NewStatus = e.Status,
                    UpdatedDateUtc = DateTimeOffset.UtcNow
                };

                try
                {
                    await queueOrderClient.SendMessageAsync(JsonSerializer.Serialize(statusMsg));
                }
                catch
                {
                    // optional logging
                }

                return await HttpJson.OkAsync(req, Map.ToDto(e));
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return await HttpJson.NotFoundAsync(req, "Order not found");
            }
            catch
            {
                return await HttpJson.ServerErrorAsync(req, "Error updating order status");
            }
        }

        [Function("Orders_Delete")]
        public async Task<HttpResponseData> DeleteOrder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "orders/{id}")] HttpRequestData req,
            string id)
        {
            var table = GetTable(_ordersTable);

            try
            {
                await table.DeleteEntityAsync("Order", id);
                return await HttpJson.NoContentAsync(req);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return await HttpJson.NotFoundAsync(req, "Order not found");
            }
            catch
            {
                return await HttpJson.ServerErrorAsync(req, "Error deleting order");
            }
        }
    }
}
