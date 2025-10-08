using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;

namespace QueueFunctionBasics
{
    internal class PersonEntity : ITableEntity
    {
        // These properties will be populated from your JSON
        public string? Name { get; set; }
        public string? Email { get; set; }

        // Required Table Storage Properties
        // PartitionKey is used to group related entities
        // We will set this to "People" in our function.
        public string PartitionKey { get; set; } = "People";

        // RowKey is the unique ID for an entity within a partition.
        // We will generate a new GUID for this in our function.
        public string RowKey { get; set; } = string.Empty;

        // Required by the ITableEntity interface
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
