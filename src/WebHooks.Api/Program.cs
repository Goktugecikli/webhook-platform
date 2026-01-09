using Microsoft.EntityFrameworkCore;
using WebHooks.Infrastructre.Persistence;


var builder = WebApplication.CreateBuilder(args);

AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
{
    if (e.Exception is System.Reflection.ReflectionTypeLoadException rtle)
    {
        Console.WriteLine("ReflectionTypeLoadException:");
        foreach (var le in rtle.LoaderExceptions)
            Console.WriteLine(" - " + le.Message);
    }
};

// Controllers
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();


builder.Services.AddSwaggerGen();

var cs = builder.Configuration.GetConnectionString("Default")
         ?? throw new InvalidOperationException("Connection string 'Default' not found.");

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs));


// ProblemDetails
builder.Services.AddProblemDetails();

var app = builder.Build();


// Global exception handling
app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.WriteLine("🔥 Swagger / Pipeline Exception");
        Console.WriteLine(ex.ToString());
        throw;
    }
});


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
