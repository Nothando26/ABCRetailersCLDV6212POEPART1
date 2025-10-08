using System.Globalization;
using ABCRetailersPOEPART1.Services;

namespace ABCRetailersPOEPART1
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container
            builder.Services.AddControllersWithViews();

            // Simple HttpClient configuration without Polly
            builder.Services.AddHttpClient("Functions", client =>
            {
                var baseUrl = builder.Configuration["FunctionApi:BaseUrl"];
                client.BaseAddress = new Uri("http://localhost:7168/api/");
            });

            builder.Services.AddScoped<IFunctionsApi, FunctionsApiClient>();
            builder.Services.AddLogging();

            var app = builder.Build();

            var culture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            // Configure middleware
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}