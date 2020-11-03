using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureADB2C.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nodsoft.YumeChan.Core;

using static Nodsoft.YumeChan.NetRunner.Properties.AppProperties;


namespace Nodsoft.YumeChan.NetRunner
{
	public class Startup
	{
		public IConfiguration Configuration { get; }

		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		// This method gets called by the runtime. Use this method to add services to the container.
		// For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
		public void ConfigureServices(IServiceCollection services)
		{
			#pragma warning disable CS0618 // Le type ou le membre est obsolète
			services.AddAuthentication(AzureADB2CDefaults.AuthenticationScheme)
					.AddAzureADB2C(options => Configuration.Bind("AzureAdB2C", options));
			#pragma warning restore CS0618 // Le type ou le membre est obsolète

			services.AddRazorPages();
			services.AddServerSideBlazor();
			services.AddHttpsRedirection(options =>
			{
				options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
				options.HttpsPort = 5001;
			});

			services.AddLogging();
			services.AddSingleton(LoggerFactory.Create(builder => 
			{
				builder.ClearProviders()
#if DEBUG
						.SetMinimumLevel(LogLevel.Trace)
#endif
						.AddConsole()
						.AddFilter("Microsoft", LogLevel.Warning)
						.AddFilter("System", LogLevel.Warning)
						.AddDebug();
			}));
			services.AddSingleton(YumeCore.Instance);
			YumeCore.ConfigureServices(services);
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
				app.UseBrowserLink();
			}
			else
			{
				app.UseExceptionHandler("/Error");
				// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
				app.UseHsts();
			}

			app.UseHttpsRedirection();
			app.UseStaticFiles();
			app.UseRouting();

			app.UseAuthentication();
			app.UseAuthorization();

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapControllers();
				endpoints.MapBlazorHub();
				endpoints.MapFallbackToPage("/_Host");
			});
		}
	}
}
