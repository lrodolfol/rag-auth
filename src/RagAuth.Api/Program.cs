using RagAuth.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.AddApplication();

var app = builder.Build();
app.MapApplication();
app.Run();

public partial class Program;
