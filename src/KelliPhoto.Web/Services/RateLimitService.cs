using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace KelliPhoto.Web.Services;

public class RateLimitService : IRateLimitService
{
    private readonly IMemoryCache _cache;
    private readonly Serilog.ILogger _logger;
    private const int MaxSubmissionsPerHour = 3; // Allow 3 submissions per hour per IP
    private const int MaxSubmissionsPerDay = 10; // Allow 10 submissions per day per IP
    private static readonly TimeSpan HourWindow = TimeSpan.FromHours(1);
    private static readonly TimeSpan DayWindow = TimeSpan.FromDays(1);

    public RateLimitService(IMemoryCache cache)
    {
        _cache = cache;
        _logger = Serilog.Log.ForContext<RateLimitService>();
    }

    public bool IsRateLimited(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return false; // Can't rate limit without IP
        }

        var normalizedIp = NormalizeIpAddress(ipAddress);
        var hourKey = $"rate_limit_hour_{normalizedIp}";
        var dayKey = $"rate_limit_day_{normalizedIp}";

        // Check hourly limit
        if (_cache.TryGetValue(hourKey, out int hourCount) && hourCount >= MaxSubmissionsPerHour)
        {
            _logger.Warning("Rate limit exceeded for IP {IpAddress} (hourly limit: {Count}/{Max})", 
                normalizedIp, hourCount, MaxSubmissionsPerHour);
            return true;
        }

        // Check daily limit
        if (_cache.TryGetValue(dayKey, out int dayCount) && dayCount >= MaxSubmissionsPerDay)
        {
            _logger.Warning("Rate limit exceeded for IP {IpAddress} (daily limit: {Count}/{Max})", 
                normalizedIp, dayCount, MaxSubmissionsPerDay);
            return true;
        }

        return false;
    }

    public void RecordSubmission(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return;
        }

        var normalizedIp = NormalizeIpAddress(ipAddress);
        var hourKey = $"rate_limit_hour_{normalizedIp}";
        var dayKey = $"rate_limit_day_{normalizedIp}";

        // Increment hourly count
        var hourCount = _cache.GetOrCreate(hourKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = HourWindow;
            return 0;
        });
        _cache.Set(hourKey, hourCount + 1, HourWindow);

        // Increment daily count
        var dayCount = _cache.GetOrCreate(dayKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DayWindow;
            return 0;
        });
        _cache.Set(dayKey, dayCount + 1, DayWindow);

        _logger.Information("Recorded contact form submission for IP {IpAddress} (Hour: {HourCount}/{MaxHour}, Day: {DayCount}/{MaxDay})", 
            normalizedIp, hourCount + 1, MaxSubmissionsPerHour, dayCount + 1, MaxSubmissionsPerDay);
    }

    private static string NormalizeIpAddress(string ipAddress)
    {
        // Remove port number if present
        var colonIndex = ipAddress.LastIndexOf(':');
        if (colonIndex > 0 && ipAddress.Substring(colonIndex + 1).All(char.IsDigit))
        {
            ipAddress = ipAddress.Substring(0, colonIndex);
        }

        // Handle IPv6 localhost
        if (ipAddress == "::1")
        {
            return "127.0.0.1";
        }

        // Handle IPv4-mapped IPv6 addresses
        if (ipAddress.StartsWith("::ffff:"))
        {
            return ipAddress.Substring(7);
        }

        return ipAddress;
    }
}
