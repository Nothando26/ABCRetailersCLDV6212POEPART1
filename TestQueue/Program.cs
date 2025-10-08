using System.Text.Json;
using Azure.Storage.Queues;

namespace TestQueue
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Your real Azure Storage connection string
            string connectionString = "<DefaultEndpointsProtocol=https;AccountName=abcretailerspoepart1;AccountKey=YdTsuCdhOdDlphkmXG1G86LIGEojirLmkpHdNDMNKCXE8OGnzpm73jd0ZvN04bALrpYVDOD/dvmH+AStVWCzmw==;EndpointSuffix=core.windows.net>";

            // Queue name must match the one your function listens to
            var queueClient = new QueueClient(
                connectionString,
                "youtube",
                new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 } // ensure it's Base64
            );

            // Create queue if it doesn't exist
            await queueClient.CreateIfNotExistsAsync();

            // Build test object
            var person = new
            {
                Name = "John Daniel",
                Email = "John@example.com"
            };

            // Serialize object to JSON
            string json = JsonSerializer.Serialize(person);

            // Send as plain JSON string
            await queueClient.SendMessageAsync(json);

            Console.WriteLine($"Message sent: {json}");
        }
    }
}
