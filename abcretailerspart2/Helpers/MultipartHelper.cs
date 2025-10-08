using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker.Http;

namespace abcretailerspart2.Functions.Helpers
{
    public sealed record FilePart(string FieldName, string FileName, Stream Data);

    public sealed record FormData(IReadOnlyDictionary<string, string> Text, IReadOnlyList<FilePart> Files);

    public static class MultipartHelper
    {
        public static async Task<FormData> ParseAsync(Stream body, string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                throw new ArgumentException("Content-Type cannot be null or empty", nameof(contentType));

            var mediaType = MediaTypeHeaderValue.Parse(contentType);
            var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;

            if (string.IsNullOrEmpty(boundary))
                throw new InvalidOperationException("Multipart boundary missing");

            var reader = new MultipartReader(boundary, body);
            var text = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var files = new List<FilePart>();

            MultipartSection section;
            while ((section = await reader.ReadNextSectionAsync()) != null)
            {
                if (string.IsNullOrEmpty(section.ContentDisposition))
                    continue; // Skip invalid sections

                var cd = ContentDispositionHeaderValue.Parse(section.ContentDisposition);

                var fieldName = cd.Name.HasValue ? cd.Name.Value.Trim('"') : string.Empty;
                if (string.IsNullOrEmpty(fieldName))
                    continue; // Skip sections without a field name

                // File section
                if (cd.DispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase)
                    && cd.FileName.HasValue)
                {
                    var fileName = cd.FileName.Value.Trim('"');
                    var ms = new MemoryStream();
                    await section.Body.CopyToAsync(ms);
                    ms.Position = 0;

                    // Do NOT dispose MemoryStream here
                    files.Add(new FilePart(fieldName, fileName, ms));
                }
                // Text section
                else if (cd.DispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase))
                {
                    using var sr = new StreamReader(section.Body, Encoding.UTF8);
                    var value = await sr.ReadToEndAsync();
                    text[fieldName] = value;
                }
            }

            return new FormData(text, files);
        }

        public static async Task<FormData> ParseAsync(Stream body, HttpHeadersCollection headers)
        {
            if (!headers.TryGetValues("Content-Type", out var contentTypes))
                throw new InvalidOperationException("Missing Content-Type header");

            var contentType = string.Join(";", contentTypes);
            return await ParseAsync(body, contentType);
        }
    }
}

