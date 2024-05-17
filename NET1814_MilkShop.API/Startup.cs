﻿using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NET1814_MilkShop.API.Infrastructure;
using NET1814_MilkShop.Repositories.Data;
using NET1814_MilkShop.Repositories.Repositories;
using NET1814_MilkShop.Services.Services;

namespace NET1814_MilkShop.API
{
    public class Startup
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public Startup(WebApplicationBuilder builder, IWebHostEnvironment env)
        {
            _configuration = builder.Configuration;
            _env = env;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(o =>
            {
                o.SwaggerDoc(
                    "v1",
                    new OpenApiInfo { Title = "NET1814_MilkShop.API", Version = "v1" }
                );
            });

            services.Configure<RouteOptions>(options =>
            {
                options.AppendTrailingSlash = false;
                options.LowercaseUrls = true;
                options.LowercaseQueryStrings = false;
            });
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (connectionString == null)
            {
                throw new InvalidOperationException(
                    "Could not find connection string 'DefaultConnection'"
                );
            }
            AddDI(services);
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
            services.AddExceptionHandler<ExceptionLoggingHandler>();
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddCors(services =>
            {
                services.AddPolicy(
                    "DefaultPolicy",
                    builder =>
                    {
                        //cho nay de domain web cua minh
                        builder
                            .WithOrigins("https://localhost:5000", "http://localhost:5001") // Allow only these origins
                            .WithMethods("GET", "POST", "PUT", "DELETE") // Allow only these methods
                            .AllowAnyHeader();
                    }
                );
                services.AddPolicy(
                    "AllowAll",
                    builder =>
                    {
                        builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                    }
                );
            });

            services
                .AddAuthentication("Bearer")
                .AddJwtBearer(o =>
                {
                    o.TokenValidationParameters =
                        new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                        {
                            ValidateIssuer = false,
                            ValidateAudience = false,
                            ValidateLifetime = false,
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"])
                            )
                        };
                });
        }

        public void Configure(WebApplication app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            var isUserSwagger = _configuration.GetValue<bool>("UseSwagger", false);
            if (isUserSwagger)
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.DefaultModelsExpandDepth(-1);
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NET1814_MilkShop.API v1");
                });
            }

            app.UseRouting();
            app.UseCors("AllowAll"); //luon dat truoc app.UseAuthorization()
            app.UseAuthorization();
            app.UseExceptionHandler(options => { });
            // ko biet sao cai nay no keu violate ASP0014, keu map route truc tiep trong api luon
            //app.UseEndpoints(endpoint =>
            //{
            //    endpoint.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
            //});
            app.MapControllers();
        }

        private void AddDI(IServiceCollection services)
        {
            //Add DI for services
            services.AddScoped<IUserService, UserService>();
            //Add DI for repositories
            services.AddScoped<IUserRepository, UserRepository>();
            //Add DI
            services.AddScoped<IAuthenticationRepository, AuthenticationRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<IUserService, UserService>();

            services.AddScoped<IAuthenticationService, AuthenticationService>();

            services.AddScoped<IUnitOfWork, UnitOfWork>();
        }
    }
}
