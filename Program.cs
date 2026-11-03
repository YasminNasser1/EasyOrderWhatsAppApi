using EasyOrderWhatsAppBLL;
using EasyOrderWhatsAppBLL.Services.Classes;
using EasyOrderWhatsAppBLL.Services.Interfaces;
using EasyOrderWhatsAppDAL.Data.Contexts;
using EasyOrderWhatsAppDAL.Data.DataSeed;
using EasyOrderWhatsAppDAL.Repositories.Classes;
using EasyOrderWhatsAppDAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EasyOrderWhatsAppApi
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            #region Add services to the container
            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddHttpClient();

            builder.Services.AddDbContext<EasyOrderDbContext>(options =>
            {
                options.UseInMemoryDatabase("EasyOrderWhatsAppDb");
            });

            // تسجيل الـ Repositories والـ UnitOfWork بنفس طريقة MetalCompany
            builder.Services.AddScoped<IOrderRepository, OrderRepository>();
            builder.Services.AddScoped<IStoreSettingRepository, StoreSettingRepository>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

            // تسجيل الـ AutoMapper
            builder.Services.AddAutoMapper(typeof(ProfileReferenceAssemply).Assembly);

            // تسجيل الـ Services
            builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
            builder.Services.AddScoped<IOrderService, OrderService>();
            #endregion

            var app = builder.Build();

            #region Migrate Database - Data Seeding
            using var Scope = app.Services.CreateScope();
            var dbContextObj = Scope.ServiceProvider.GetRequiredService<EasyOrderDbContext>();

            string defaultBrand = "";
            string defaultApiKey = builder.Configuration["EasyOrders:ApiKey"] ?? "";

            string savedKeyFile = Path.Combine(app.Environment.ContentRootPath, "easyorders_apikey.txt");
            if (File.Exists(savedKeyFile))
            {
                string keyFromFile = File.ReadAllText(savedKeyFile).Trim();
                if (!string.IsNullOrWhiteSpace(keyFromFile))
                    defaultApiKey = keyFromFile;
            }

            string savedBrandFile = Path.Combine(app.Environment.ContentRootPath, "easyorders_brandname.txt");
            if (File.Exists(savedBrandFile))
            {
                string brandFromFile = File.ReadAllText(savedBrandFile).Trim();
                if (!string.IsNullOrWhiteSpace(brandFromFile))
                    defaultBrand = brandFromFile;
            }

            await EasyOrderDataSeeding.SeedData(dbContextObj, defaultBrand, defaultApiKey);
            #endregion

            #region Configure the HTTP request pipeline
            app.UseRouting();
            app.UseAuthorization();
            app.MapControllers();
            #endregion

            app.Run();
        }
    }
}
