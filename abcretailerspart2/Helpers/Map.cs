using System;
using abcretailerspart2.Functions.Entities;
using abcretailerspart2.Functions.Models;

namespace abcretailerspart2.Functions.Helpers
{
    public static class Map
    {
        // Convert CustomerEntity to CustomerDto
        public static CustomerDto ToDto(CustomerEntity e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            return new CustomerDto(
                e.RowKey,
                e.Name ?? string.Empty,
                e.Surname ?? string.Empty,
                e.Username ?? string.Empty,
                e.Email ?? string.Empty,
                e.ShippingAddress ?? string.Empty
            );
        }

        // Convert ProductEntity to ProductDto
        public static ProductDto ToDto(ProductEntity e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            // Safe conversion from double to decimal
            decimal priceSafe;
            try
            {
                priceSafe = Convert.ToDecimal(e.Price);
            }
            catch
            {
                priceSafe = 0m; // fallback in case of invalid double
            }

            return new ProductDto(
                e.RowKey,
                e.ProductName ?? string.Empty,
                e.Description ?? string.Empty,
                priceSafe,
                e.StockAvailable,
                e.ImageUrl ?? string.Empty
            );
        }

        // Convert OrderEntity to OrderDto
        public static OrderDto ToDto(OrderEntity e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            decimal unitPriceSafe;
            try
            {
                unitPriceSafe = Convert.ToDecimal(e.UnitPrice);
            }
            catch
            {
                unitPriceSafe = 0m;
            }

            decimal totalAmount = unitPriceSafe * e.Quantity;

            return new OrderDto(
                e.RowKey,
                e.CustomerId ?? string.Empty,
                e.ProductId ?? string.Empty,
                e.ProductName ?? string.Empty,
                e.Quantity,
                unitPriceSafe,
                totalAmount,
                e.OrderDateUtc,
                e.Status ?? string.Empty
            );
        }
    }
}
