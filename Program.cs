using System.IO;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using JobEntryApp.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

var app = builder.Build();
// FileSystemWatcher setup
var basePath = app.Configuration["JobFoldersBasePath"] ?? "P:\\Danielle\\JOB FOLDERS";
if (Directory.Exists(basePath))
{
	var watcher = new FileSystemWatcher(basePath)
	{
		IncludeSubdirectories = true,
		EnableRaisingEvents = true
	};
	watcher.Created += (s, e) =>
	{
		var folder = Path.GetFileName(Path.GetDirectoryName(e.FullPath));
		var file = Path.GetFileName(e.FullPath);
		string eventType = null;
		if (folder == "Data") eventType = "Data Received";
		else if (folder == "Art") eventType = "Art Received";
		else if (folder == "Reports" && file.Contains("counts", StringComparison.OrdinalIgnoreCase)) eventType = "Counts Received";
		else if (folder == "Signoffs" && file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) eventType = "Signoffs Received";
		if (eventType != null)
		{
			// TODO: Replace with your notification logic
			Console.WriteLine($"[{eventType}] {e.FullPath}");
		}
	};
}

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