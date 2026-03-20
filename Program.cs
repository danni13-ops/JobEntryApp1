using JobEntryApp.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

var app = builder.Build();

// Initialize database constraints / performance
DatabaseInitializer.EnsurePerformanceAndConstraints(app.Configuration, app.Logger);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();   // required for css/js/images

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();