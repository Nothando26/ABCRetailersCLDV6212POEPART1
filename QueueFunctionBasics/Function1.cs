using System;
using System.Text.Json;
using Azure.Data.Tables;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace QueueFunctionBasics;

public class Function1
{
    private readonly ILogger<Function1> _logger;
    private readonly string _storageConnectionString;
    private TableClient _tableClient;

    public Function1(ILogger<Function1> logger)
    {
        _logger = logger;
        _storageConnectionString = Environment.GetEnvironmentVariable("connection");
        var serviceClient = new TableServiceClient(_storageConnectionString);
        _tableClient = serviceClient.GetTableClient("PeopleTable");
    }

    //[Function(nameof(Function1))]
    //public void Run([QueueTrigger("youtube", Connection = "connection")] QueueMessage message)
    //{
    //    _logger.LogInformation("C# Queue trigger function processed: {messageText}", message.MessageText);
    //}


    [Function(nameof(QueuePeopleSender))]
    public static async Task QueuePeopleSender(
        [QueueTrigger("youtube", Connection = "connection")] QueueMessage message,
        ILogger _logger)
    {
        _logger.LogInformation($"C# Queue trigger function processed: {message.MessageText}");

        // Create the Table if it does not exist
        await _tableClient.CreateIfNotExistsAsync();

        // 1. Manually deserialize the JSON string into our object
        var person = JsonSerializer.Deserialize<PersonEntity>(message.MessageText);
        if (person == null)
        {
            _logger.LogError("Failed to deserialize JSON message");
            return;
        }

        // 2. CRITICAL STEP: Set the required PartitionKey and RowKey
        person.RowKey = Guid.NewGuid().ToString();
        person.PartitionKey = "People";
        _logger.LogInformation($"Saving entity with RowKey: {person.RowKey}");

        // 3. Manually add the entity to the table
        await _tableClient.AddEntityAsync(person);
        _logger.LogInformation("SUCCESSFULLY SAVED PERSON TO TABLE");
    }
}
}