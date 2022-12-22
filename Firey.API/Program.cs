using Firey.Data;
using System.Reflection;

namespace Firey.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplication? app = null;

            try
            {
                RunApp(args, out app);
            }
            finally
            {
                if(app != null && app.Services.GetService<IHeater>() is IHeater heater)
                {
                    heater.Disable();
                }
            }
        }

        private static void RunApp(string[] args, out WebApplication? app)
        {
#if DEBUG
            var wwwroot = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "wwwroot");

            var options = new WebApplicationOptions()
            {
                WebRootPath = wwwroot,
                //ContentRootPath = wwwroot,
                EnvironmentName = "Dev"
            };
#else
            var options = new WebApplicationOptions();
#endif

            var builder = WebApplication.CreateBuilder(options);
            builder.Configuration.AddJsonFile("appsettings.json");

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddSignalR();

            builder.Services.AddSingleton<IKilnRunRepostory, KilnRunRepository>();

#if DEBUG
            var modelKiln = new ModelKiln();
            builder.Services.AddSingleton<ModelKiln>(modelKiln);
            builder.Services.AddSingleton<ITemperatureSensor>(f => f.GetRequiredService<ModelKiln>());
            builder.Services.AddSingleton<IHeater>(f => f.GetRequiredService<ModelKiln>());

            builder.Services.AddSingleton<SteppedTimeSource>();
            builder.Services.AddSingleton<ITimeSource>(s => s.GetRequiredService<SteppedTimeSource>());

            builder.Services.AddHostedService<SimulationService>();

#else
            builder.Services.AddSingleton(new GpioController());
            builder.Services.AddSingleton<ITemperatureSensor, SpiThermocouple>();
            builder.Services.AddSingleton<IHeater, GpioHeater>();
            builder.Services.AddSingleton<ITimeSource, DefaultTimeSource>();
#endif

            builder.Services.AddSingleton<KilnControlService>();
            builder.Services.AddHostedService(s => s.GetRequiredService<KilnControlService>());

            // wire up kiln control updates to send to signalR clients
            builder.Services.AddHostedService<BroadcastService>();

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    builder =>
                    {
                        builder.WithOrigins("*")
                            .AllowAnyHeader()
                            .WithMethods("GET", "POST");
                    });
            });


            app = builder.Build();

            app.UseSwagger();
            app.UseSwaggerUI();

            // UseCors must be called before MapHub.
            app.UseCors();

#if !DEBUG
            app.UseDefaultFiles();
            app.UseStaticFiles();
#endif

            app.MapControllers();

            app.MapHub<ControlHub>("/control");


#if DEBUG
            app.Run("http://0.0.0.0:5000");
#else
            app.Run("http://0.0.0.0:80");
#endif
        }
    }
}