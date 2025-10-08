using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using abcretailerspart2.Functions.Entities;
using abcretailerspart2.Functions.Helpers;
using abcretailerspart2.Functions.Models;

namespace abcretailerspart2.Functions.Functions
{
    public class ProductsFunctions
    {
        private readonly string _conn;
        private readonly string _tableName;
        private readonly string _imagesContainer;

        public ProductsFunctions(IConfiguration cfg)
        {
            _tableName = cfg["TABLE_PRODUCT"] ?? "Product";
            _imagesContainer = cfg["BLOB_PRODUCT_IMAGES"] ?? "product-images";
            _conn = cfg["STORAGE_CONNECTION"] ?? throw new InvalidOperationException("STORAGE_CONNECTION missing");
        }

        private TableClient GetTable() => new TableClient(_conn, _tableName);

        [Function("Products_List")]
        public async Task<HttpResponseData> ListProducts(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "products")] HttpRequestData req)
        {
            var table = GetTable();
            await table.CreateIfNotExistsAsync();

            var items = new List<ProductDto>();
            await foreach (var e in table.QueryAsync<ProductEntity>(x => x.PartitionKey == "Product"))
                items.Add(Map.ToDto(e));

            return await HttpJson.OkAsync(req, items);
        }

        [Function("Products_Get")]
        public async Task<HttpResponseData> GetProduct(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "products/{id}")] HttpRequestData req,
            string id)
        {
            var table = GetTable();
            try
            {
                var e = await table.GetEntityAsync<ProductEntity>("Product", id);
                return await HttpJson.OkAsync(req, Map.ToDto(e.Value));
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return await HttpJson.NotFoundAsync(req, "Product not found");
            }
        }

        [Function("Products_Create")]
        public async Task<HttpResponseData> CreateProduct(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "products")] HttpRequestData req)
        {
            var table = GetTable();
            await table.CreateIfNotExistsAsync();

            var form = await MultipartHelper.ParseAsync(req.Body, req.Headers);

            string productName = form.Text.GetValueOrDefault("ProductName")?.Trim() ?? "";
            string description = form.Text.GetValueOrDefault("Description")?.Trim() ?? "";
            string stockStr = form.Text.GetValueOrDefault("StockAvailable") ?? "0";
            string priceStr = form.Text.GetValueOrDefault("Price") ?? "0";
            string? imageUrlFromText = form.Text.GetValueOrDefault("ImageUrl")?.Trim();

            if (string.IsNullOrWhiteSpace(productName))
                return await HttpJson.BadAsync(req, "ProductName is required");

            if (!int.TryParse(stockStr, out int stock))
                return await HttpJson.BadAsync(req, "StockAvailable must be an integer");

            if (!double.TryParse(priceStr, out double price))
                return await HttpJson.BadAsync(req, "Price must be a valid number");

            string imageUrl = imageUrlFromText;
            var file = form.Files.FirstOrDefault(f => f.FieldName == "ImageFile");
            if (file != null && file.Data.Length > 0)
            {
                if (file.Data.CanSeek) file.Data.Position = 0;

                var container = new BlobContainerClient(_conn, _imagesContainer);
                await container.CreateIfNotExistsAsync();

                string blobName = $"{Guid.NewGuid():N}-{file.FileName}";
                var blob = container.GetBlobClient(blobName);

                await blob.UploadAsync(file.Data, overwrite: true);

                imageUrl = blob.Uri.ToString();
            }

            var product = new ProductEntity
            {
                PartitionKey = "Product",
                RowKey = Guid.NewGuid().ToString(),
                ProductName = productName,
                Description = description,
                Price = price,
                StockAvailable = stock,
                ImageUrl = imageUrl ?? string.Empty
            };

            await table.AddEntityAsync(product);
            return await HttpJson.CreatedAsync(req, Map.ToDto(product));
        }

        [Function("Products_Update")]
        public async Task<HttpResponseData> UpdateProduct(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "products/{id}")] HttpRequestData req,
            string id)
        {
            var table = GetTable();
            ProductEntity existing;
            try
            {
                existing = (await table.GetEntityAsync<ProductEntity>("Product", id)).Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return await HttpJson.NotFoundAsync(req, "Product not found");
            }

            req.Headers.TryGetValues("Content-Type", out var ctValues);
            string contentType = ctValues?.FirstOrDefault() ?? "";

            if (contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                var form = await MultipartHelper.ParseAsync(req.Body, req.Headers);

                if (form.Text.TryGetValue("ProductName", out var pn) && !string.IsNullOrWhiteSpace(pn))
                    existing.ProductName = pn.Trim();

                if (form.Text.TryGetValue("Description", out var desc))
                    existing.Description = desc.Trim();

                if (form.Text.TryGetValue("StockAvailable", out var st) && int.TryParse(st, out var stock))
                    existing.StockAvailable = stock;

                if (form.Text.TryGetValue("Price", out var pr) && double.TryParse(pr, out var price))
                    existing.Price = price;

                if (form.Text.TryGetValue("ImageUrl", out var imgUrl))
                    existing.ImageUrl = imgUrl.Trim();

                var file = form.Files.FirstOrDefault(f => f.FieldName == "ImageFile");
                if (file != null && file.Data.Length > 0)
                {
                    if (file.Data.CanSeek) file.Data.Position = 0;

                    var container = new BlobContainerClient(_conn, _imagesContainer);
                    await container.CreateIfNotExistsAsync();

                    string blobName = $"{Guid.NewGuid():N}-{file.FileName}";
                    var blob = container.GetBlobClient(blobName);

                    await blob.UploadAsync(file.Data, overwrite: true);

                    existing.ImageUrl = blob.Uri.ToString();
                }
            }
            else
            {
                var body = await HttpJson.ReadAsync<Dictionary<string, object>>(req);

                if (body.TryGetValue("ProductName", out var pnObj) && pnObj != null)
                    existing.ProductName = pnObj.ToString();

                if (body.TryGetValue("Description", out var descObj) && descObj != null)
                    existing.Description = descObj.ToString();

                if (body.TryGetValue("StockAvailable", out var stObj) && stObj != null
                    && int.TryParse(stObj.ToString(), out var stVal))
                    existing.StockAvailable = stVal;

                if (body.TryGetValue("Price", out var prObj) && prObj != null
                    && double.TryParse(prObj.ToString(), out var prVal))
                    existing.Price = prVal;

                if (body.TryGetValue("ImageUrl", out var imgObj) && imgObj != null)
                    existing.ImageUrl = imgObj.ToString();
            }

            await table.UpdateEntityAsync(existing, existing.ETag, TableUpdateMode.Replace);
            return await HttpJson.OkAsync(req, Map.ToDto(existing));
        }

        [Function("Products_Delete")]
        public async Task<HttpResponseData> DeleteProduct(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "products/{id}")] HttpRequestData req,
            string id)
        {
            var table = GetTable();
            try
            {
                await table.DeleteEntityAsync("Product", id);
                return await HttpJson.NoContentAsync(req);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return await HttpJson.NotFoundAsync(req, "Product not found");
            }
        }
    }
}

