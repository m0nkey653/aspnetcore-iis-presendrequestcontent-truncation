var app = WebApplication.CreateBuilder(args).Build();

// Writes two lines, flushing after each one.
// Expected: "first" and "second". Actual (with the module in web.config, v4.0 app pool): only "first", status 200.
app.MapGet("/", async (HttpContext ctx) =>
{
    await ctx.Response.WriteAsync("first\n");
    await ctx.Response.Body.FlushAsync();

    await ctx.Response.WriteAsync("second\n");
    await ctx.Response.Body.FlushAsync();
});

app.Run();
