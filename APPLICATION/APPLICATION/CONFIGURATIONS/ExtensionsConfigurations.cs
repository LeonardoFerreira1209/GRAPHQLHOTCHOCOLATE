﻿using APPLICATION.APPLICATION.CONFIGURATIONS.SWAGGER;
using APPLICATION.APPLICATION.SERVICES.CEP;
using APPLICATION.APPLICATION.SERVICES.USER;
using APPLICATION.DOMAIN.CONTRACTS.API;
using APPLICATION.DOMAIN.CONTRACTS.RESPOSITORIES.CEP;
using APPLICATION.DOMAIN.CONTRACTS.SERVICES.CEP;
using APPLICATION.DOMAIN.CONTRACTS.SERVICES.USER;
using APPLICATION.DOMAIN.DTOS.CONFIGURATION.AUTH.TOKEN;
using APPLICATION.DOMAIN.DTOS.REQUEST.USER;
using APPLICATION.DOMAIN.UTILS;
using APPLICATION.INFRAESTRUTURE.CONTEXTO;
using APPLICATION.INFRAESTRUTURE.FACADES.CEP;
using APPLICATION.INFRAESTRUTURE.FACADES.EMAIL;
using APPLICATION.INFRAESTRUTURE.GRAPHQL.QUERIE;
using APPLICATION.INFRAESTRUTURE.REPOSITORY;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Refit;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Swashbuckle.AspNetCore.Annotations;
using System.Globalization;
using System.Net.Mime;

namespace APPLICATION.APPLICATION.CONFIGURATIONS
{
    public static class ExtensionsConfigurations
    {
        public static readonly string HealthCheckEndpoint = "/application/healthcheck";

        /// <summary>
        /// Configuração de Logs do sistema.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static WebApplicationBuilder ConfigureSerilog(this WebApplicationBuilder applicationBuilder)
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            applicationBuilder.Host.UseSerilog((context, config) =>
            {
                config
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Error)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentUserName()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithProcessName()
                .Enrich.WithThreadId()
                .Enrich.WithThreadName()
                .WriteTo.Console();
            });

            return applicationBuilder;
        }

        /// <summary>
        /// Configuração de linguagem principal do sistema.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureLanguage(this IServiceCollection services)
        {
            var cultureInfo = new CultureInfo("pt-BR");

            CultureInfo
                .DefaultThreadCurrentCulture = cultureInfo;

            CultureInfo
                .DefaultThreadCurrentUICulture = cultureInfo;

            return services;
        }

        /// <summary>
        /// Configuração do banco de dados do sistema.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureContexto(this IServiceCollection services, IConfiguration configurations)
        {
            services
                .AddDbContext<Contexto>(options => options.UseLazyLoadingProxies().UseSqlServer(configurations.GetValue<string>("ConnectionStrings:BaseDados")));

            return services;
        }

        /// <summary>
        /// Configuração do identity server do sistema.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureIdentityServer(this IServiceCollection services, IConfiguration configuration)
        {
            services
                .AddIdentity<IdentityUser<Guid>, IdentityRole<Guid>>(options => options.SignIn.RequireConfirmedAccount = true).AddEntityFrameworkStores<Contexto>().AddDefaultTokenProviders();

            services.Configure<IdentityOptions>(options =>
            {
                options.Password.RequireDigit = true;

                options.Password.RequireDigit = options.Password.RequireUppercase = true;

                options.Password.RequiredLength = configuration.GetValue<int>("Auth:Password:RequiredLength");
            });

            return services;
        }

        /// <summary>
        /// Configuração da autenticação do sistema.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureAuthentication(this IServiceCollection services, IConfiguration configurations)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;

            }).AddJwtBearer(options =>
            {

                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    ValidIssuer = configurations.GetValue<string>("Token:Issuer"),
                    ValidAudience = configurations.GetValue<string>("Token:Audience"),

                    IssuerSigningKey = JwtSecurityKey.Create(configurations.GetValue<string>("Token:Secret"))
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        Log.Information("[LOG INFORMATION] - OnAuthenticationFailed " + context.Exception.Message);

                        return Task.CompletedTask;
                    },

                    OnTokenValidated = context =>
                    {
                        Log.Information("[LOG INFORMATION] - OnTokenValidated " + context.SecurityToken);

                        return Task.CompletedTask;
                    }
                };

            });

            return services;
        }

        /// <summary>
        /// Configuração da autorização do sistema.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureAuthorization(this IServiceCollection services)
        {
            services.AddAuthorization(auth => { auth.AddPolicy("Bearer", new AuthorizationPolicyBuilder().AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().Build()); });

            return services;
        }

        /// <summary>
        /// Configuração do swagger do sistema.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configurations"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureSwagger(this IServiceCollection services, IConfiguration configurations)
        {
            var apiVersion = configurations.GetValue<string>("SwaggerInfo:ApiVersion"); var apiDescription = configurations.GetValue<string>("SwaggerInfo:ApiDescription"); var uriMyGit = configurations.GetValue<string>("SwaggerInfo:UriMyGit");

            services.AddSwaggerGen(swagger =>
            {
                swagger.EnableAnnotations();

                swagger.SwaggerDoc(apiVersion, new OpenApiInfo
                {
                    Version = apiVersion,
                    Title = $"{apiDescription} - {apiVersion}",
                    Description = apiDescription,

                    Contact = new OpenApiContact
                    {
                        Name = "HYPER.IO DESENVOLVIMENTOS LTDA",
                        Email = "HYPER.IO@OUTLOOK.COM",
                    }

                });

                swagger.DocumentFilter<HealthCheckSwagger>();
            });

            return services;
        }

        /// <summary>
        /// Configuração das dependencias (Serrvices, Repository, Facades, etc...).
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureDependencies(this IServiceCollection services, IConfiguration configurations)
        {
            services
                .AddTransient(x => configurations)
            // Services
                .AddTransient<ICepService, CepService>()
                .AddTransient<IUserService, UserService>()
            // Facades
                .AddSingleton<EmailFacade, EmailFacade>()
            // Facades
                .AddSingleton<ICepFacade, CepFacade>()
            //Repositories
                .AddSingleton<ICepRepository, CepRepository>();

            return services;
        }

        /// <summary>
        /// Configura chamadas a APIS externas através do Refit.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureRefit(this IServiceCollection services, IConfiguration configurations)
        {
            services
                .AddRefitClient<ICepExternal>().ConfigureHttpClient(c => c.BaseAddress = configurations.GetValue<Uri>("UrlBase:cep"));

            return services;
        }

        /// <summary>
        /// Configura as consultas através do GRAPHQL.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureGraphQL(this IServiceCollection services)
        {
            services
                .AddGraphQLServer().AddProjections().AddFiltering().AddSorting().AddQueryType<CepQuery>();

            return services;
        }

        /// <summary>
        /// Configuração do HealthChecks do sistema.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configurations"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureHealthChecks(this IServiceCollection services, IConfiguration configurations)
        {
            services
                .AddHealthChecks().AddSqlServer(configurations.GetConnectionString("BaseDados"), name: "Base de dados padrão.", tags: new string[] { "Core", "SQL Server" });

            return services;
        }

        /// <summary>
        /// Configuração dos cors aceitos.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection ConfigureCors(this IServiceCollection services)
        {
            return services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
            });
        }

        /// <summary>
        /// Configuração do HealthChecks do sistema.
        /// </summary>
        /// <param name="application"></param>
        /// <returns></returns>
        public static IApplicationBuilder ConfigureHealthChecks(this IApplicationBuilder application)
        {
            application.UseHealthChecks(ExtensionsConfigurations.HealthCheckEndpoint, new HealthCheckOptions
            {
                ResponseWriter = async (context, report) =>
                {
                    var result = JsonConvert.SerializeObject(new
                    {
                        statusApplication = report.Status.ToString(),

                        healthChecks = report.Entries.Select(e => new
                        {
                            check = e.Key,
                            ErrorMessage = e.Value.Exception?.Message,
                            status = Enum.GetName(typeof(HealthStatus), e.Value.Status)
                        })
                    });

                    context.Response.ContentType = MediaTypeNames.Application.Json;

                    await context.Response.WriteAsync(result);
                }
            });

            return application;
        }

        /// <summary>
        /// Configuração de uso do swagger do sistema.
        /// </summary>
        /// <param name="application"></param>
        /// <param name="configurations"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseSwaggerConfigurations(this IApplicationBuilder application, IConfiguration configurations)
        {
            var apiVersion = configurations.GetValue<string>("SwaggerInfo:ApiVersion");

            application
                .UseSwagger();

            application
                .UseSwaggerUI(swagger => swagger.SwaggerEndpoint($"/swagger/{apiVersion}/swagger.json", $"{apiVersion}"));

            application
                .UseMvcWithDefaultRoute();

            return application;
        }

        /// <summary>
        /// Configruação de minimals APIS.
        /// </summary>
        /// <param name="applicationBuilder"></param>
        /// <returns></returns>
        public static WebApplication UseMinimalAPI(this WebApplication application, IConfiguration configurations)
        {
            #region User's
            application.MapPost("/security/create",
            [AllowAnonymous][SwaggerOperation(Summary = "Criar usuário.", Description = "Método responsavel por criar usuário")]
            //[ProducesResponseType(typeof(ApiResponse<TokenJWT>), StatusCodes.Status200OK)]
            //[ProducesResponseType(typeof(ApiResponse<TokenJWT>), StatusCodes.Status400BadRequest)] 
            //[ProducesResponseType(typeof(ApiResponse<TokenJWT>), StatusCodes.Status500InternalServerError)]
            async ([Service] IUserService userService, CreateRequest request) =>
            {
                using (LogContext.PushProperty("Controller", "UserController"))
                using (LogContext.PushProperty("Payload", JsonConvert.SerializeObject(request)))
                using (LogContext.PushProperty("Metodo", "Create"))
                {
                    return await Tracker.Time(() => userService.Create(request), "Criar usuário");
                }
            });

            application.MapPost("/security/authentication",
            [AllowAnonymous][SwaggerOperation(Summary = "Autenticação do usuário", Description = "Método responsável por Autenticar usuário")]
            //[ProducesResponseType(typeof(ApiResponse<TokenJWT>), StatusCodes.Status200OK)]
            //[ProducesResponseType(typeof(ApiResponse<TokenJWT>), StatusCodes.Status400BadRequest)]
            //[ProducesResponseType(typeof(ApiResponse<TokenJWT>), StatusCodes.Status500InternalServerError)]
            async ([Service] IUserService userService, LoginRequest request) =>
            {
                using (LogContext.PushProperty("Controller", "UserController"))
                using (LogContext.PushProperty("Payload", JsonConvert.SerializeObject(request)))
                using (LogContext.PushProperty("Metodo", "Authentication"))
                {
                    return await Tracker.Time(() => userService.Authentication(request), "Autenticar usuário");
                }
            });

            application.MapPost("/security/activate",
            [AllowAnonymous][SwaggerOperation(Summary = "Ativar usuário", Description = "Método responsável por Ativar usuário")]
            //[ProducesResponseType(typeof(ApiResponse<TokenJWT>), StatusCodes.Status200OK)]
            //[ProducesResponseType(typeof(ApiResponse<TokenJWT>), StatusCodes.Status400BadRequest)]
            //[ProducesResponseType(typeof(ApiResponse<TokenJWT>), StatusCodes.Status500InternalServerError)]
            async ([Service] IUserService userService, ActivateUserRequest request) =>
            {
                using (LogContext.PushProperty("Controller", "UserController"))
                using (LogContext.PushProperty("Payload", JsonConvert.SerializeObject(request)))
                using (LogContext.PushProperty("Metodo", "activate"))
                {
                    return await Tracker.Time(() => userService.Activate(request), "Ativar usuário");
                }
            });
            #endregion

            return application;
        }
    }
}
