
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using abcretailerspart2.Functions.Entities;
using abcretailerspart2.Functions.Helpers;
using abcretailerspart2.Functions.Models;

namespace abcretailerspart2.Functions.Functions
{
    public class CustomersFunctions
    {
        private readonly string _conn;
        private readonly string _table;

        public CustomersFunctions(IConfiguration cfg)
        {
            _conn = cfg["STORAGE_CONNECTION"]
                    ?? throw new InvalidOperationException("STORAGE_CONNECTION missing");
            _table = cfg["TABLE_CUSTOMER"] ?? "Customer";
        }

        [Function("Customers_List")]
        public async Task<HttpResponseData> ListCustomers(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers")] HttpRequestData req)
        {
            var table = new TableClient(_conn, _table);
            await table.CreateIfNotExistsAsync();

            var items = new List<CustomerDto>();
            await foreach (var e in table.QueryAsync<CustomerEntity>(x => x.PartitionKey == "Customer"))
            {
                items.Add(Map.ToDto(e));
            }

            return await HttpJson.OkAsync(req, items);
        }

        [Function("Customers_Get")]
        public async Task<HttpResponseData> GetCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers/{id}")] HttpRequestData req,
            string id)
        {
            var table = new TableClient(_conn, _table);

            try
            {
                var e = await table.GetEntityAsync<CustomerEntity>("Customer", id);
                return await HttpJson.OkAsync(req, Map.ToDto(e.Value));
            }
            catch
            {
                return await HttpJson.NotFoundAsync(req, "Customer not found");
            }
        }

        public record CustomerCreateUpdate(
            string? Name,
            string? Surname,
            string? Username,
            string? Email,
            string? ShippingAddress
        );

        [Function("Customers_Create")]
        public async Task<HttpResponseData> CreateCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers")] HttpRequestData req)
        {
            var input = await HttpJson.ReadAsync<CustomerCreateUpdate>(req);

            if (input is null || string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Email))
            {
                return await HttpJson.BadAsync(req, "Name and Email are required");
            }

            var table = new TableClient(_conn, _table);
            await table.CreateIfNotExistsAsync();

            var e = new CustomerEntity
            {
                Name = input.Name!,
                Surname = input.Surname ?? string.Empty,
                Username = input.Username ?? string.Empty,
                Email = input.Email!,
                ShippingAddress = input.ShippingAddress ?? string.Empty
            };

            await table.AddEntityAsync(e);
            return await HttpJson.CreatedAsync(req, Map.ToDto(e));
        }

        [Function("Customers_Update")]
        public async Task<HttpResponseData> UpdateCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "customers/{id}")] HttpRequestData req,
            string id)
        {
            var input = await HttpJson.ReadAsync<CustomerCreateUpdate>(req);
            if (input is null)
                return await HttpJson.BadAsync(req, "Invalid body");

            var table = new TableClient(_conn, _table);

            try
            {
                var resp = await table.GetEntityAsync<CustomerEntity>("Customer", id);
                var e = resp.Value;

                e.Name = input.Name ?? e.Name;
                e.Surname = input.Surname ?? e.Surname;
                e.Username = input.Username ?? e.Username;
                e.ShippingAddress = input.ShippingAddress ?? e.ShippingAddress;

                await table.UpdateEntityAsync(e, e.ETag, TableUpdateMode.Replace);
                return await HttpJson.OkAsync(req, Map.ToDto(e));
            }
            catch
            {
                return await HttpJson.NotFoundAsync(req, "Customer not found");
            }
        }

        [Function("Customers_Delete")]
        public async Task<HttpResponseData> DeleteCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "customers/{id}")] HttpRequestData req,
            string id)
        {
            var table = new TableClient(_conn, _table);
            await table.DeleteEntityAsync("Customer", id);
            return await HttpJson.NoContentAsync(req);
        }
    }
}

