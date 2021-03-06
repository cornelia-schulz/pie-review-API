using Marvin.Cache.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Serialization;
using Pies.API.DbContexts;
using Pies.API.Services;
using System;
using System.Linq;

namespace Pies.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddCors(o =>
                    o
                        .AddPolicy("Policy",
                        builder =>
                        {
                            builder
                                .WithOrigins("https://localhost:3001",
                                "http://localhost:3000",
                                "http://localhost:3001")
                                .AllowAnyMethod()
                                .AllowAnyHeader();
                        }));
            services.AddHttpCacheHeaders((expirationModelOptions) => 
            {
                expirationModelOptions.MaxAge = 60;
                expirationModelOptions.CacheLocation = Marvin.Cache.Headers.CacheLocation.Private;
            },
            (validationModelOptions) => 
            {
                validationModelOptions.MustRevalidate = true;
            });

            services.AddResponseCaching();
            // default output is json, add xml as additional format and send a 406 not acceptable for any other formats requested in the header
            services.AddControllers(setupAction =>
            {
                setupAction.ReturnHttpNotAcceptable = true;
                setupAction.CacheProfiles.Add("240SecondsCacheProfile",
                    new CacheProfile()
                    {
                        Duration = 240
                    });
            }).AddNewtonsoftJson(setupAction =>
            {
                setupAction.SerializerSettings.ContractResolver =
                    new CamelCasePropertyNamesContractResolver();
            })
            .AddXmlDataContractSerializerFormatters()
            .ConfigureApiBehaviorOptions(setupAction =>
            {
                setupAction.InvalidModelStateResponseFactory = context =>
                {
                    // create a problem details object
                    var problemDetailsFactory = context.HttpContext.RequestServices
                    .GetRequiredService<ProblemDetailsFactory>();
                    var problemDetails = problemDetailsFactory.CreateValidationProblemDetails(
                        context.HttpContext,
                        context.ModelState);

                    // add additional information that isn't there by default
                    problemDetails.Detail = "See the errors field for details.";
                    problemDetails.Instance = context.HttpContext.Request.Path;

                    // find out which status code to use
                    var actionExecutingContext =
                        context as Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext;

                    // if there are modelstate errors & all arguments were correctly
                    // found or parsed, there are validation errors
                    if ((context.ModelState.ErrorCount > 0) &&
                        (actionExecutingContext?.ActionArguments.Count ==
                        context.ActionDescriptor.Parameters.Count))
                    {
                        problemDetails.Type = "https//courselibrary.com/modelvalidationproblem";
                        problemDetails.Status = StatusCodes.Status422UnprocessableEntity;
                        problemDetails.Title = "One or more validation errors occurred";

                        return new UnprocessableEntityObjectResult(problemDetails)
                        {
                            ContentTypes = { "application/problem+json" }
                        };
                    };

                    // if one of the arguments wasn't correctly found / couldn't be parsed
                    // this is null/unparsable input
                    problemDetails.Status = StatusCodes.Status400BadRequest;
                    problemDetails.Title = "One or more errors occurred on input";
                    return new BadRequestObjectResult(problemDetails)
                    {
                        ContentTypes = { "application/problem+json" }
                    };
                };
            });

            services.Configure<MvcOptions>(config =>
            {
                var newtonsoftJsonOutputFormatter = config.OutputFormatters
                    .OfType<NewtonsoftJsonOutputFormatter>()?.FirstOrDefault();

                if (newtonsoftJsonOutputFormatter != null)
                {
                    newtonsoftJsonOutputFormatter.SupportedMediaTypes.Add("application/vnd.marvin.hateoas+json");
                }
            });

            // register PropertyMappingService
            services.AddTransient<IPropertyMappingService, PropertyMappingService>();

            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

            services.AddScoped<IPiesRepository, PiesRepository>();

            services.AddDbContext<PiesContext>(options =>
            {
                options.UseSqlServer(
                    @"Server=localhost;Database=PiesDB;User Id=pies_db;Password=Password11;Integrated Security=false;");
                //@"Server=(localdb)\mssqllocaldb;Database=PiesDB;Trusted_Connection=True;");
            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler(appBuilder =>
                {
                    appBuilder.Run(async context =>
                    {
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("An unexpected fault happened. Try again later.");
                    });
                });
            }

            // caching needs to be before routing etc, so it can be hit first
            app.UseResponseCaching();

            app.UseHttpCacheHeaders();

            app.UseRouting();

            app.UseCors("Policy");

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                // Attribute based routing
                endpoints.MapControllers();
            });
        }
    }
}
