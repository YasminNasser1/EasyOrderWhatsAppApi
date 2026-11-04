using EasyOrderWhatsAppBLL;
using EasyOrderWhatsAppBLL.Constants;
using EasyOrderWhatsAppBLL.Services.Classes;
using EasyOrderWhatsAppBLL.Services.Interfaces;
using EasyOrderWhatsAppDAL.Data.Contexts;
using EasyOrderWhatsAppDAL.Data.DataSeed;
using EasyOrderWhatsAppDAL.Data.FileStorage;
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
            builder.Services.AddControllers();
            builder.Services.AddHttpClient();

            // DAL: الداتابيز + الـ Repositories + الـ UnitOfWork (بنفس طريقة MetalCompany)
            builder.Services.AddDbContext<EasyOrderDbContext>(options =>
            {
                options.UseInMemoryDatabase("EasyOrderWhatsAppDb");
            });
            builder.Services.AddScoped<IOrderRepository, OrderRepository>();
            builder.Services.AddScoped<IStoreSettingRepository, StoreSettingRepository>();
            builder.Services.AddScoped<ITenantRepository, TenantRepository>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddSingleton<ISettingsFileStore>(new SettingsFileStore(builder.Environment.ContentRootPath));

            // AutoMapper
            builder.Services.AddAutoMapper(typeof(ProfileReferenceAssemply).Assembly);

            // BLL: الـ Services
            builder.Services.AddScoped<ITenantService, TenantService>();
            builder.Services.AddScoped<IStoreSettingService, StoreSettingService>();
            builder.Services.AddScoped<IOrderService, OrderService>();
            builder.Services.AddHttpClient<IEasyOrdersApiService, EasyOrdersApiService>();
            builder.Services.AddHttpClient<IWhatsAppGatewayService, WhatsAppGatewayService>(client =>
            {
                client.BaseAddress = new Uri(WhatsAppGatewayRoutes.BaseUrl);
            });
            #endregion

            var app = builder.Build();

            #region Data Seeding
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<EasyOrderDbContext>();
                var settingsFileStore = scope.ServiceProvider.GetRequiredService<ISettingsFileStore>();
                await EasyOrderDataSeeding.SeedData(dbContext, settingsFileStore);
            }
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
