using System.IO;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace abcretailerspart2.Functions
{
    public class BlobFunctions
    {
        [Function("OnProductImageUploaded")]
        public void OnProductImageUploaded(
            [BlobTrigger("%BLOB_PRODUCT_IMAGES%/{name}", Connection = "STORAGE_CONNECTION")]

            byte[] blob,
            string name,
            FunctionContext context)
        {
            var logger = context.GetLogger("OnProductImageUploaded");
            var size = blob?.Length ?? 0;
            logger.LogInformation($"✅ Product image uploaded: {name}, size {size} bytes");
        }
    }
}
