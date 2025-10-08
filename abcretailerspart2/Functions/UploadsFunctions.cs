using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using abcretailerspart2.Functions.Helpers;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace abcretailerspart2.Functions
{
    public class UploadsFunctions
    {
        private readonly string _conn;
        private readonly string _proofs;
        private readonly string _share;
        private readonly string _shareDir;

        public UploadsFunctions(IConfiguration cfg)
        {
            _conn = cfg["STORAGE_CONNECTION"] ?? throw new InvalidOperationException("STORAGE_CONNECTION missing");
            _proofs = cfg["BLOB_PAYMENT_PROOFS"] ?? "payment-proofs";
            _share = cfg["FILESHARE_CONTRACTS"] ?? "contracts";
            _shareDir = cfg["FILESHARE_DIR_PAYMENTS"] ?? "payments";
        }

        [Function("Uploads_ProofOfPayment")]
        public async Task<HttpResponseData> UploadProofOfPayment(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "uploads/proof-of-payment")] HttpRequestData req)
        {
            var log = req.FunctionContext.GetLogger("Uploads_ProofOfPayment");

            try
            {
                var contentType = req.Headers.TryGetValues("Content-Type", out var ct)
                    ? ct.FirstOrDefault()
                    : null;

                if (string.IsNullOrWhiteSpace(contentType) || !contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
                    return await HttpJson.BadAsync(req, "Expected multipart/form-data");

                var form = await MultipartHelper.ParseAsync(req.Body, contentType);

                var file = form.Files.FirstOrDefault(f => f.FieldName == "ProofOfPayment");
                if (file == null)
                    return await HttpJson.BadAsync(req, "Proof of payment file is required");

                if (!file.Data.CanSeek)
                    return await HttpJson.BadAsync(req, "Uploaded file stream must be seekable");

                if (file.Data.Length == 0)
                    return await HttpJson.BadAsync(req, "Proof of payment file is empty");

                var orderId = form.Text.GetValueOrDefault("OrderId");
                var customerName = form.Text.GetValueOrDefault("CustomerName");

                if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(customerName))
                    return await HttpJson.BadAsync(req, "OrderId and CustomerName are required");

                // Reset stream position before upload
                file.Data.Position = 0;

                // Upload to Azure Blob Storage
                var container = new BlobContainerClient(_conn, _proofs);
                await container.CreateIfNotExistsAsync();
                var blobName = $"{Guid.NewGuid():N}-{file.FileName}";
                var blob = container.GetBlobClient(blobName);
                await blob.UploadAsync(file.Data, overwrite: true);

                // Upload metadata to Azure File Share
                var share = new ShareClient(_conn, _share);
                await share.CreateIfNotExistsAsync();

                var root = share.GetRootDirectoryClient();
                var dir = root.GetSubdirectoryClient(_shareDir);
                await dir.CreateIfNotExistsAsync();

                var fileClient = dir.GetFileClient(blobName + ".txt");

                var meta = new StringBuilder();
                meta.AppendLine($"UploadedAtUtc: {DateTimeOffset.UtcNow:O}");
                meta.AppendLine($"OrderId: {orderId}");
                meta.AppendLine($"CustomerName: {customerName}");
                meta.AppendLine($"BlobUrl: {blob.Uri}");

                var bytes = Encoding.UTF8.GetBytes(meta.ToString());
                using var ms = new System.IO.MemoryStream(bytes);

                await fileClient.CreateAsync(bytes.Length);
                await fileClient.UploadAsync(ms);

                return await HttpJson.OkAsync(req, new { FileName = blobName, BlobUrl = blob.Uri.ToString() });
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error uploading proof of payment");
                return await HttpJson.ServerErrorAsync(req, "An error occurred uploading the proof of payment");
            }
        }
    }
}

