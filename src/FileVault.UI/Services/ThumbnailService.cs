using System.IO;
using System.Windows.Media.Imaging;

namespace FileVault.UI.Services;

public class ThumbnailService
{
    private readonly Dictionary<string, BitmapImage> _cache = new();
    private readonly Queue<string> _order = new();
    private const int MaxEntries = 200;

    public Task<BitmapImage?> GetThumbnailAsync(byte[] imageBytes, string cacheKey,
        int decodePixelWidth = 240, int rotationDegrees = 0)
    {
        var effectiveKey = rotationDegrees != 0 ? $"{cacheKey}@r{rotationDegrees}" : cacheKey;
        if (_cache.TryGetValue(effectiveKey, out var cached)) return Task.FromResult<BitmapImage?>(cached);

        try
        {
            var bitmap = new BitmapImage();
            using var ms = new MemoryStream(imageBytes);
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = decodePixelWidth;
            bitmap.StreamSource = ms;
            bitmap.Rotation = rotationDegrees switch
            {
                90 => Rotation.Rotate90,
                180 => Rotation.Rotate180,
                270 => Rotation.Rotate270,
                _ => Rotation.Rotate0,
            };
            bitmap.EndInit();
            bitmap.Freeze();

            if (_cache.Count >= MaxEntries)
            {
                var oldest = _order.Dequeue();
                _cache.Remove(oldest);
            }
            _cache[effectiveKey] = bitmap;
            _order.Enqueue(effectiveKey);
            return Task.FromResult<BitmapImage?>(bitmap);
        }
        catch { return Task.FromResult<BitmapImage?>(null); }
    }
}
