using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using SecureServer.Controllers;
using SecureServer.Data;
using SecureServer.Models;
using System.Collections.Concurrent;

namespace SecureServer.data
{
    public class TokenValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _memoryCache;
        private readonly TimeSpan _blockDuration = TimeSpan.FromMinutes(1);
        private readonly int _maxRequests = 100;
        private readonly TimeSpan _timeWindow = TimeSpan.FromMinutes(1);

        private readonly ILogger<TokenValidationMiddleware> _logger;

        private static readonly ConcurrentQueue<HttpContext> _requestQueue = new();
        private static readonly SemaphoreSlim _queueSemaphore = new(1, 1);
        private const int MaxRequestsPerBlock = 300;
        private static bool _isProcessing = false;

        private readonly int _maxRequestsExcludedPaths = 50;
        private readonly TimeSpan _timeWindowExcludedPaths = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _blockDurationExcluded = TimeSpan.FromMinutes(5);

        public TokenValidationMiddleware(RequestDelegate next, IMemoryCache memoryCache, ILogger<TokenValidationMiddleware> logger)
        {
            _next = next;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            if (context.Request.Path.Value == null)
            {
                await UnknownRequest(context, "The requested resource was not found.");
                return;
            }

            _requestQueue.Enqueue(context);
            _logger.LogInformation("Request added to queue. Current queue size: {size}", _requestQueue.Count);

            await ProcessQueue(serviceProvider);
        }

        private async Task ProcessQueue(IServiceProvider serviceProvider)
        {
            if (_isProcessing || _requestQueue.IsEmpty) return;

            await _queueSemaphore.WaitAsync();
            try
            {
                _isProcessing = true;

                while (!_requestQueue.IsEmpty)
                {
                    var batch = new List<HttpContext>();
                    while (batch.Count < MaxRequestsPerBlock && _requestQueue.TryDequeue(out var request))
                    {
                        batch.Add(request);
                    }

                    _logger.LogInformation("Processing batch of {count} requests", batch.Count);
                    var tasks = batch.Select(async request => await ProcessRequest(request, serviceProvider)).ToList();
                    await Task.WhenAll(tasks);
                }
            }
            finally
            {
                _isProcessing = false;
                _queueSemaphore.Release();
            }
        }

        private async Task ProcessRequest(HttpContext context, IServiceProvider serviceProvider)
        {
            if (IsExcludedPath(context.Request.Path.Value))
            {
                await HandleExcludedPathRequest(context);
                return;
            }

            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            var username = context.Request.Headers["UserName"].FirstOrDefault();

            _logger.LogInformation("Token:{token}\nUsername:{user}", token?.ToString(), username?.ToString());

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Some of string null or empty. Username: {username}, token: {token}. Refuse request", username, token);
                await DenyRequest(context, "Some of entered options == null");
                return;
            }

            if (context.Request.Path.Value.StartsWith("/api/admin"))
            {
                _logger.LogInformation("Admin api request. Checking for admin premissions");
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var user = dbContext.Users.FirstOrDefault(u => u.Login == username);

                if (user == null || user.role != "admin")
                {
                    await DenyRequest(context, "Unauthorized from validation");
                    return;
                }
            }

            await HandleRequestFrequency(context, username);
            await HandleTokenValidation(context, token, username, serviceProvider);
        }

        private bool IsExcludedPath(string path)
        {
            // Потом под мапу сделать?
            return path != null && (path.StartsWith("/api/auth/login") || 
                                    path.StartsWith("/api/check/") || 
                                    path.StartsWith("/health") || 
                                    path.StartsWith("/favicon") || 
                                    path.StartsWith("/api/mods/public") || 
                                    path.StartsWith("/api/token/valid") || 
                                    path.StartsWith("/api/user/getMods") || 
                                    path.StartsWith("/api/ip"));
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
                _memoryCache.Set(clientKeyExcluded, requestInfoExcluded, _blockDurationExcluded);
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
                await DenyRequest(context, "Unauthorized from validation. Something wrong!");
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

        private async Task UnknownRequest(HttpContext context, string message)
        {
            _logger.LogInformation("The requested resource was not found. Path: {path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync(message);
        }

        private async Task<bool> ValidateTokenAsync(string token, string username, IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Login == username);
            var date = await dbContext.ActiveTokens.SingleOrDefaultAsync(u => u.Username == username);

            if (date?.ExpiryDate.Date < DateTime.UtcNow.Date || user == null || user.Banned)
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
