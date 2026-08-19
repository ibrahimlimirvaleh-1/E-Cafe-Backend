namespace ECafe.Api.Middlewares;

public sealed class SecurityHeadersMiddleware
{
    private const string DefaultContentSecurityPolicy =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "frame-ancestors 'none'; " +
        "object-src 'none'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self' data:; " +
        "style-src 'self' 'unsafe-inline'; " +
        "script-src 'self'; " +
        "connect-src 'self'";

    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public SecurityHeadersMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _next = next;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task Invoke(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            ApplyHeaders(context);
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private void ApplyHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers.TryAdd("X-Content-Type-Options", "nosniff");
        headers.TryAdd("X-Frame-Options", "DENY");
        headers.TryAdd("Referrer-Policy", "no-referrer");
        headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()");

        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Local"))
            headers.TryAdd("Strict-Transport-Security", "max-age=31536000; includeSubDomains");

        if (ShouldSkipCsp(context))
            return;

        var csp = _configuration["SecurityHeaders:ContentSecurityPolicy"];
        headers.TryAdd("Content-Security-Policy", string.IsNullOrWhiteSpace(csp) ? DefaultContentSecurityPolicy : csp);
    }

    private bool ShouldSkipCsp(HttpContext context)
        => (_environment.IsDevelopment() || _environment.IsEnvironment("Local"))
           && context.Request.Path.StartsWithSegments("/swagger");
}
