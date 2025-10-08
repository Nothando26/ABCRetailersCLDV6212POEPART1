using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace abcretailerspart2.Functions.Helpers
{
    public static class HttpJson
    {
        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

        // ✅ Deserialize request body to object
        public static async Task<T?> ReadAsync<T>(HttpRequestData req)
        {
            using var s = req.Body;
            return await JsonSerializer.DeserializeAsync<T>(s, _json);
        }

        // ✅ Return 200 OK with JSON body
        public static Task<HttpResponseData> OkAsync<T>(HttpRequestData req, T body)
        {
            return WriteAsync(req, HttpStatusCode.OK, body);
        }

        // ✅ Return 201 Created with JSON body
        public static Task<HttpResponseData> CreatedAsync<T>(HttpRequestData req, T body)
        {
            return WriteAsync(req, HttpStatusCode.Created, body);
        }

        // ✅ Return 400 Bad Request with text message
        public static Task<HttpResponseData> BadAsync(HttpRequestData req, string message)
        {
            return TextAsync(req, HttpStatusCode.BadRequest, message);
        }

        // ✅ Return 404 Not Found with text message
        public static Task<HttpResponseData> NotFoundAsync(HttpRequestData req, string message = "Not Found")
        {
            return TextAsync(req, HttpStatusCode.NotFound, message);
        }

        // ✅ Return 204 No Content
        public static Task<HttpResponseData> NoContentAsync(HttpRequestData req)
        {
            var r = req.CreateResponse(HttpStatusCode.NoContent);
            return Task.FromResult(r);
        }

        // ✅ Return plain text response (async)
        public static async Task<HttpResponseData> TextAsync(HttpRequestData req, HttpStatusCode code, string message)
        {
            var r = req.CreateResponse(code);
            r.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            await r.WriteStringAsync(message, Encoding.UTF8);
            return r;
        }

        // ✅ Return JSON response (async)
        private static async Task<HttpResponseData> WriteAsync<T>(HttpRequestData req, HttpStatusCode code, T body)
        {
            var r = req.CreateResponse(code);
            r.Headers.Add("Content-Type", "application/json; charset=utf-8");
            var json = JsonSerializer.Serialize(body, _json);
            await r.WriteStringAsync(json, Encoding.UTF8);
            return r;
        }


        // ✅ Return 500 Internal Server Error
        public static async Task<HttpResponseData> ServerErrorAsync(HttpRequestData req, string message)
        {
            var r = req.CreateResponse(HttpStatusCode.InternalServerError);
            r.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            await r.WriteStringAsync(message, Encoding.UTF8);
            return r;
        }
    }
}
