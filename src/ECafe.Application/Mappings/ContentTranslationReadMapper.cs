using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Mappings.ContentLocalization;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Enums;
using ECafe.Shared.DTOs;

namespace ECafe.Application.Mappings;

public sealed record ContentReadBinding(ContentEntityType Type, int Id,
    Func<ContentTranslationValue, string?> ReadTranslation, Action<string> WriteTranslation);

public static class ContentTranslationReadMapper
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Properties = new();
    private static readonly IReadOnlyDictionary<Type, IContentResponseProfile> DefaultProfiles =
        ContentResponseProfiles.Default.ToDictionary(profile => profile.ResponseType);

    /// <summary>Tanınmış profillərin məzmun bağlarını toplayır; redaktor və sifariş snapshot sahələrini dəyişmir.</summary>
    public static IReadOnlyList<ContentReadBinding> Collect(object response, int? restaurantId = null, bool unitLookup = false)
        => CollectCore(response, DefaultProfiles, new(restaurantId, unitLookup));

    /// <summary>Açıq profil dəsti ilə eyni təhlükəsiz gəzintini işlədir; yeni DTO xəritəsi üçün əsas alqoritm dəyişmir.</summary>
    public static IReadOnlyList<ContentReadBinding> Collect(object response, IEnumerable<IContentResponseProfile> profiles,
        int? restaurantId = null, bool unitLookup = false)
        => CollectCore(response, profiles.ToDictionary(profile => profile.ResponseType), new(restaurantId, unitLookup));

    /// <summary>Boş tərcümədə ilkin mətni saxlayır; yalnız profilin tipli yazma nümayəndəsini çağırır.</summary>
    public static void Apply(ContentReadBinding binding, ContentTranslationValue value)
    {
        var text = binding.ReadTranslation(value);
        if (!string.IsNullOrWhiteSpace(text)) binding.WriteTranslation(text);
    }

    /// <summary>Bir cavab üçün istinad izləməsini ayırır və bağları dəyişməz kontekstdə toplayır.</summary>
    private static IReadOnlyList<ContentReadBinding> CollectCore(object response,
        IReadOnlyDictionary<Type, IContentResponseProfile> profiles, ContentResponseContext context)
    {
        var bindings = new List<ContentReadBinding>();
        Walk(response, bindings, new HashSet<object>(ReferenceEqualityComparer.Instance), profiles, context, 0);
        return bindings;
    }

    /// <summary>Yalnız DTO və səhifə zərflərini məhdud dərinliklə gəzir; dövrləri və xam tərcümə redaktorunu buraxır.</summary>
    private static void Walk(object value, List<ContentReadBinding> bindings, HashSet<object> visited,
        IReadOnlyDictionary<Type, IContentResponseProfile> profiles, ContentResponseContext context, int depth)
    {
        if (depth > 12 || value is string || !visited.Add(value)) return;
        if (value is IEnumerable collection)
        {
            foreach (var item in collection)
                if (item != null) Walk(item, bindings, visited, profiles, context, depth + 1);
            return;
        }
        var type = value.GetType();
        var page = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PaginatedList<>);
        if (!page && (type.Namespace?.StartsWith("ECafe.Application.DTOs.", StringComparison.Ordinal) != true ||
            type.Namespace.StartsWith("ECafe.Application.DTOs.ContentLocalization", StringComparison.Ordinal))) return;
        if (profiles.TryGetValue(type, out var profile)) profile.Collect(value, context, bindings);
        foreach (var property in ReadableProperties(type))
        {
            if (property.PropertyType == typeof(string) || property.PropertyType.IsValueType) continue;
            var child = property.GetValue(value);
            if (child != null) Walk(child, bindings, visited, profiles, context, depth + 1);
        }
    }

    /// <summary>Reflection yalnız obyekt qrafının oxunmasına xidmət edir; sahə yazılması profildə tipli şəkildə təyin olunur.</summary>
    private static PropertyInfo[] ReadableProperties(Type type)
        => Properties.GetOrAdd(type, static item => item.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0).ToArray());
}
