using System.Text.Json;
using OnlineStore.Core.Exceptions;
using OnlineStore.Core.Interfaces;

namespace OnlineStore.Infrastructure.Services;

public class DummyJsonClient(HttpClient http) : IDummyJsonClient
{
    public static readonly IReadOnlyDictionary<string, string> Categories = new Dictionary<string, string>
    {
        ["smartphones"] = "Smartphones", ["laptops"] = "Laptops",
        ["tablets"] = "Tablets", ["mobile-accessories"] = "Mobile Accessories"
    }.AsReadOnly();

    public async Task<IReadOnlyList<SourceProduct>> GetTechnologyProductsAsync(CancellationToken ct = default)
    {
        try
        {
            var batches = await Task.WhenAll(Categories.Keys.Select(async category =>
            {
                using var response = await http.GetAsync($"products/category/{category}?limit=0", ct);
                if (!response.IsSuccessStatusCode) throw new StoreException("DummyJSON returned an unsuccessful response. Retry synchronization later.", 502);
                var json = await response.Content.ReadAsStringAsync(ct);
                if (json.Length > 2_000_000) throw new JsonException("Response too large.");
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var products = root.GetProperty("products");
                if (products.GetArrayLength() == 0 || root.GetProperty("total").GetInt32() != products.GetArrayLength())
                    throw new JsonException("Incomplete category response.");
                return products.EnumerateArray().Select(p => Parse(p, category)).ToList();
            }));
            var result = batches.SelectMany(b => b).ToList();
            if (result.Select(p => p.Id).Distinct().Count() != result.Count) throw new JsonException("Duplicate external IDs.");
            return result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new StoreException("DummyJSON timed out. Retry synchronization later.", 504); }
        catch (HttpRequestException)
        { throw new StoreException("Cannot connect to DummyJSON. Existing products have not changed.", 503); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { throw new StoreException("DummyJSON returned invalid product data. Existing products have not changed.", 502); }
    }

    private static SourceProduct Parse(JsonElement p, string category)
    {
        var id = p.GetProperty("id").GetInt32();
        var name = p.GetProperty("title").GetString()!;
        var description = p.GetProperty("description").GetString()!;
        var price = p.GetProperty("price").GetDecimal();
        var brand = p.TryGetProperty("brand", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() : null;
        if (id <= 0 || string.IsNullOrWhiteSpace(name) || name.Length > 100 || description is null || description.Length > 500
            || price <= 0 || price > 1_000_000_000 || brand?.Length > 100 || p.GetProperty("category").GetString() != category
            || p.GetProperty("stock").GetInt32() < 0 || !p.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array)
            throw new JsonException("Invalid product fields.");
        if (images.EnumerateArray().Any(i => i.ValueKind != JsonValueKind.String)) throw new JsonException("Invalid images.");
        foreach (var key in new[] { "thumbnail", "sku", "availabilityStatus", "warrantyInformation", "shippingInformation", "returnPolicy" })
            if (p.TryGetProperty(key, out var value) && value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) throw new JsonException("Invalid text metadata.");
        if (p.TryGetProperty("rating", out var rating) && rating.ValueKind != JsonValueKind.Null && (rating.GetDecimal() < 0 || rating.GetDecimal() > 5)) throw new JsonException("Invalid rating.");
        if (p.TryGetProperty("reviews", out var reviews) && reviews.ValueKind != JsonValueKind.Null)
        {
            if (reviews.ValueKind != JsonValueKind.Array) throw new JsonException("Invalid reviews.");
            foreach (var review in reviews.EnumerateArray())
            {
                if (review.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid review.");
                foreach (var key in new[] { "reviewerName", "comment", "date" })
                    if (review.TryGetProperty(key, out var value) && value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) throw new JsonException("Invalid review text.");
            }
        }
        return new(id, name, description, category, brand, price, p.GetRawText());
    }
}
