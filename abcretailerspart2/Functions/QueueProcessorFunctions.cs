using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace abcretailerspart2.Functions
{
    public class QueueProcessorFunctions
    {
        [Function("OrderNotifications_Processor")]
        public async Task OrderNotificationsProcessorAsync(
            [QueueTrigger("order-notifications", Connection = "STORAGE_CONNECTION")] string message,
            FunctionContext ctx)
        {
            var log = ctx.GetLogger("OrderNotifications_Processor");
            try
            {
                log.LogInformation($"OrderNotifications message: {message}");

                // TODO: Add async processing (e.g., email notifications)
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error processing OrderNotifications message");
                throw; // Optionally rethrow to let the Functions runtime handle retries
            }
        }

        [Function("StockUpdates_Processor")]
        public async Task StockUpdatesProcessorAsync(
           [QueueTrigger("stock-updates", Connection = "STORAGE_CONNECTION")] string message,
            FunctionContext ctx)
        {
            var log = ctx.GetLogger("StockUpdates_Processor");
            try
            {
                log.LogInformation($"StockUpdates message: {message}");

                // TODO: Add async processing (e.g., update reporting DB)
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error processing StockUpdates message");
                throw;
            }
        }
    }
}
