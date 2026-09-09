using HackerNewsApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<HackerNewsServiceOptions>(
    builder.Configuration.GetSection("HackerNewsService"));

builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var options = new HackerNewsServiceOptions();
    configuration.GetSection("HackerNewsService").Bind(options);
    return options;
});

builder.Services.AddHttpClient<IHackerNewsService, HackerNewsService>(httpClient =>
    {
        var baseAddress = builder.Configuration["HackerNewsService:BaseAddress"]
                          ?? "https://hacker-news.firebaseio.com/";
        httpClient.BaseAddress = new Uri(baseAddress);
        httpClient.Timeout = TimeSpan.FromSeconds(10);
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();