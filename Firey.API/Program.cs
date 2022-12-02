using Microsoft.AspNetCore.SignalR;

namespace Firey.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddSignalR();

#if DEBUG
            var modelKiln = new ModelKiln();
            builder.Services.AddSingleton<ModelKiln>(modelKiln);
            builder.Services.AddHostedService(f => f.GetRequiredService<ModelKiln>());
            builder.Services.AddSingleton<ITemperatureSensor>(f => f.GetRequiredService<ModelKiln>());
            builder.Services.AddSingleton<IHeater>(f => f.GetRequiredService<ModelKiln>());

#else
            builder.Services.AddSingleton<ITemperatureSensor, SpiThermocouple>();
            builder.Services.AddSingleton<IHeater, FakeHeater>();

#endif
            builder.Services.AddSingleton<ITimeSource, DefaultTimeSource>();
            
            builder.Services.AddSingleton<KilnControlService>();
            builder.Services.AddHostedService(s => s.GetRequiredService<KilnControlService>());

            // wire up kiln control updates to send to signalR clients
            builder.Services.AddHostedService<BroadcastService>();

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    builder =>
                    {
                        builder.WithOrigins("http://127.0.0.1:5173")
                            .AllowAnyHeader()
                            .WithMethods("GET", "POST")
                            .AllowCredentials();
                    });
            });


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            // UseCors must be called before MapHub.
            app.UseCors();

            app.MapControllers();

            app.MapHub<ControlHub>("/control");

            app.Run();
        }
    }
}