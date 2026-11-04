var app = WebApplication.CreateBuilder(args).Build();

// Writes 64 KB in 16 chunks of 4 KB, flushing after each one.
// Expected: 65536 bytes. Actual (with the module in web.config, v4.0 app pool): 4096 bytes, status 200.
app.MapGet("/", async (HttpContext ctx) =>
{
    var chunk = new byte[4096];
    Array.Fill(chunk, (byte)'a');

    for (var i = 0; i < 16; i++)
    {
        await ctx.Response.Body.WriteAsync(chunk);
        await ctx.Response.Body.FlushAsync();
    }
});

app.Run();
