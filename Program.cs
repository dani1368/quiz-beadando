var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(); // API vezérlők
builder.Services.AddEndpointsApiExplorer(); // Swagger támogatás
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.MapGet("/", () => Results.Redirect("/index.html"));
app.MapControllers();

app.Run();

