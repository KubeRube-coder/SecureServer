using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SecureServer.Controllers;
using SecureServer.Data;
using SecureServer.Models;

namespace SecureServer.data
{
    public class TokenValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _memoryCache;
        private readonly TimeSpan _blockDuration = TimeSpan.FromMinutes(5);
        private readonly int _maxRequests = 10;
        private readonly TimeSpan _timeWindow = TimeSpan.FromSeconds(30);

        private readonly ILogger<TokenValidationMiddleware> _logger;

        private readonly int _maxRequestsExcludedPaths = 20;
        private readonly TimeSpan _timeWindowExcludedPaths = TimeSpan.FromMinutes(2);

        public TokenValidationMiddleware(RequestDelegate next, IMemoryCache memoryCache, ILogger<TokenValidationMiddleware> logger)
        {
            _next = next;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            if (IsExcludedPath(context.Request.Path.Value))
            {
                await HandleExcludedPathRequest(context);
                return;
            }

            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            var username = context.Request.Headers["UserName"].FirstOrDefault();

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Some of string null or empty. Username: {username}, token: {token}. Refuse request", username, token);
                await DenyRequest(context, "Unauthorized from validation");
                return;
            }

            await HandleRequestFrequency(context, username);
            await HandleTokenValidation(context, token, username, serviceProvider);
        }

        private bool IsExcludedPath(string path)
        {
            return path != null && (path.StartsWith("/api/auth/login") || path.StartsWith("/api/check/") || path.StartsWith("/health") || path.StartsWith("/admin/"));
        }

        private async Task HandleExcludedPathRequest(HttpContext context)
        {
            var clientKeyExcluded = $"ExcludedPathFrequency:{context.Connection.RemoteIpAddress}";

            if (!_memoryCache.TryGetValue(clientKeyExcluded, out ClientRequestInfo requestInfoExcluded))
            {
                requestInfoExcluded = new ClientRequestInfo { RequestCount = 1, LastRequestTime = DateTime.UtcNow };
                _memoryCache.Set(clientKeyExcluded, requestInfoExcluded, TimeSpan.FromMinutes(10));
            }
            else
            {
                if (DateTime.UtcNow - requestInfoExcluded.LastRequestTime <= _timeWindowExcludedPaths)
                {
                    requestInfoExcluded.RequestCount++;
                    if (requestInfoExcluded.RequestCount > _maxRequestsExcludedPaths)
                    {
                        await BlockRequest(context, "Too many requests to excluded path. You are temporarily blocked.");
                        return;
                    }
                }
                else
                {
                    requestInfoExcluded.RequestCount = 1;
                }

                requestInfoExcluded.LastRequestTime = DateTime.UtcNow;
                _memoryCache.Set(clientKeyExcluded, requestInfoExcluded, TimeSpan.FromMinutes(10));
            }

            await _next(context);
        }

        private async Task HandleRequestFrequency(HttpContext context, string username)
        {
            var clientKey = $"RequestFrequency:{username}";

            if (!_memoryCache.TryGetValue(clientKey, out ClientRequestInfo requestInfo))
            {
                requestInfo = new ClientRequestInfo { RequestCount = 1, BlockedUntil = null, LastRequestTime = DateTime.UtcNow };
                _memoryCache.Set(clientKey, requestInfo, TimeSpan.FromMinutes(10));
            }
            else
            {
                if (requestInfo.BlockedUntil != null && requestInfo.BlockedUntil > DateTime.UtcNow)
                {
                    _logger.LogWarning("[{username}] Blocked request while temporarily blocked.", username);
                    await BlockRequest(context, "Too many requests. You are temporarily blocked.");
                    return;
                }

                if (DateTime.UtcNow - requestInfo.LastRequestTime <= _timeWindow)
                {
                    requestInfo.RequestCount++;
                    if (requestInfo.RequestCount > _maxRequests)
                    {
                        requestInfo.BlockedUntil = DateTime.UtcNow.Add(_blockDuration);
                        _logger.LogWarning("[{username}] Request limit exceeded. Blocking user for {duration} minutes.", username, _blockDuration.TotalMinutes);
                        await BlockRequest(context, "Too many requests. You are temporarily blocked.");
                        return;
                    }
                }
                else
                {
                    requestInfo.RequestCount = 1;
                }

                requestInfo.LastRequestTime = DateTime.UtcNow;
            }

            _memoryCache.Set(clientKey, requestInfo, TimeSpan.FromMinutes(10));
        }

        private async Task HandleTokenValidation(HttpContext context, string token, string username, IServiceProvider serviceProvider)
        {
            if (!await ValidateTokenAsync(token, username, serviceProvider))
            {
                _logger.LogWarning("[{username}] Token validation failed!", username);
                await DenyRequest(context, "Unauthorized from validation");
                return;
            }

            _logger.LogInformation("[{username}] Token validated successfully.", username);
            await _next(context);
        }

        private async Task DenyRequest(HttpContext context, string message)
        {
            var username = context.Request.Headers["UserName"].FirstOrDefault();
            _logger.LogWarning("[{username}] Access denied: {message}", username ?? "Unknown", message);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync(message);
        }

        private async Task BlockRequest(HttpContext context, string message)
        {
            var username = context.Request.Headers["UserName"].FirstOrDefault();
            _logger.LogWarning("[{username}] User blocked: {message}", username ?? "Unknown", message);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsync(message);
        }

        private async Task<bool> ValidateTokenAsync(string token, string username, IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Login == username);
            var date = await dbContext.ActiveTokens.SingleOrDefaultAsync(u => u.Username == username);

            if (date?.ExpiryDate < DateTime.UtcNow || user == null || user.Banned)
                return false;

            return user.JwtSecretKey == token;
        }

        private class ClientRequestInfo
        {
            public int RequestCount { get; set; }
            public DateTime LastRequestTime { get; set; }
            public DateTime? BlockedUntil { get; set; }
        }
    }
}
