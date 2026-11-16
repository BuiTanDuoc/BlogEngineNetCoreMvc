using System.Globalization;
using System.Reflection;
using BlogEngine.Web.Data;
using BlogEngine.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlogEngine.Web.Services;

public interface ISettingsService
{
    /// <summary>Trả về bản sao của cấu hình hiện tại (đã cache).</summary>
    Task<BlogSettings> GetAsync();
    Task SaveAsync(BlogSettings settings);
}

public class SettingsService : ISettingsService
{
    private const string CacheKey = "blog-settings";

    private static readonly PropertyInfo[] Props = typeof(BlogSettings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p is { CanRead: true, CanWrite: true })
        .ToArray();

    private readonly BlogDbContext _db;
    private readonly IMemoryCache _cache;

    public SettingsService(BlogDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<BlogSettings> GetAsync()
    {
        if (_cache.TryGetValue(CacheKey, out BlogSettings? cached) && cached is not null)
            return cached.Clone();

        var rows = await _db.Settings.AsNoTracking()
            .ToDictionaryAsync(s => s.Name.ToLower(), s => s.Value);

        var settings = new BlogSettings();
        foreach (var p in Props)
        {
            if (!rows.TryGetValue(p.Name.ToLowerInvariant(), out var raw) || raw is null) continue;

            if (p.PropertyType == typeof(string))
                p.SetValue(settings, raw);
            else if (p.PropertyType == typeof(int) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                p.SetValue(settings, i);
            else if (p.PropertyType == typeof(bool) && bool.TryParse(raw, out var b))
                p.SetValue(settings, b);
        }

        _cache.Set(CacheKey, settings, TimeSpan.FromMinutes(30));
        return settings.Clone();
    }

    public async Task SaveAsync(BlogSettings settings)
    {
        var existing = await _db.Settings.ToDictionaryAsync(s => s.Name.ToLower());

        foreach (var p in Props)
        {
            var key = p.Name.ToLowerInvariant();
            var value = Convert.ToString(p.GetValue(settings), CultureInfo.InvariantCulture) ?? "";

            if (existing.TryGetValue(key, out var row))
                row.Value = value;
            else
                _db.Settings.Add(new Setting { Name = key, Value = value });
        }

        await _db.SaveChangesAsync();
        _cache.Remove(CacheKey);
    }
}
