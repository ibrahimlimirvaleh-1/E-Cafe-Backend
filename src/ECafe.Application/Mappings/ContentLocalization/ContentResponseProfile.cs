using System.Linq.Expressions;
using System.Reflection;
using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Domain.Enums;

namespace ECafe.Application.Mappings.ContentLocalization;

public sealed record ContentResponseContext(int? RestaurantId, bool UnitLookup);

public interface IContentResponseProfile
{
    Type ResponseType { get; }
    void Collect(object response, ContentResponseContext context, ICollection<ContentReadBinding> bindings);
}

public sealed class ContentResponseProfile<T>(params ContentResponseField<T>[] fields) : IContentResponseProfile where T : class
{
    public Type ResponseType => typeof(T);

    /// <summary>Yalnız qeydiyyatdakı dəqiq DTO növü üçün icazəli sahələrin tipli bağlarını yaradır.</summary>
    public void Collect(object response, ContentResponseContext context, ICollection<ContentReadBinding> bindings)
    {
        if (response.GetType() != typeof(T)) return;
        foreach (var field in fields) field.Collect((T)response, context, bindings);
    }
}

public sealed class ContentResponseField<T> where T : class
{
    private readonly ContentEntityType type;
    private readonly Func<T, ContentResponseContext, int?> identifier;
    private readonly Func<ContentTranslationValue, string?> translation;
    private readonly Action<T, string> setter;

    /// <summary>Tipli property ifadəsindən bir dəfə yazma nümayəndəsi qurur; string sahə adı və dinamik SetValue istifadə etmir.</summary>
    public ContentResponseField(ContentEntityType type, Func<T, ContentResponseContext, int?> identifier,
        Expression<Func<T, string?>> destination, Func<ContentTranslationValue, string?> translation)
    {
        this.type = type;
        this.identifier = identifier;
        this.translation = translation;
        if (destination.Body is not MemberExpression { Member: PropertyInfo { CanWrite: true } } member ||
            member.Expression != destination.Parameters[0])
            throw new ArgumentException("A direct writable string property is required.", nameof(destination));
        var text = Expression.Parameter(typeof(string));
        setter = Expression.Lambda<Action<T, string>>(Expression.Assign(member, text), destination.Parameters[0], text).Compile();
    }

    /// <summary>Müsbət entity identifikatoru olan sahəni əlavə edir; uyğun olmayan kontekst və boş ID heç nə yaratmır.</summary>
    internal void Collect(T response, ContentResponseContext context, ICollection<ContentReadBinding> bindings)
    {
        var id = identifier(response, context);
        if (id is > 0) bindings.Add(new(type, id.Value, translation, text => setter(response, text)));
    }
}
