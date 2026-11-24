using System.Text.Json;
using System.Text.Json.Nodes;
using Sentry;
using Sentry.Extensions.Logging;
using ECafe.Api.Middlewares;
using ECafe.Shared.Diagnostics;

namespace ECafe.Api.Monitoring;

public static class SentryPrivacy
{
    private static readonly string[] EventFields = ["event_id", "timestamp", "level", "platform", "release", "environment", "logger", "sdk"];
    private static readonly string[] TraceFields = ["trace_id", "span_id", "parent_span_id", "op", "status", "origin"];
    private static readonly string[] FrameFields = ["function", "module", "lineno", "colno", "in_app", "package", "instruction_addr", "symbol_addr"];

    /// <summary>Monitorinqi saxlayır, amma body/PII/structured log göndərişini bağlayıb event və trace üçün eyni allowlist sərhədini tətbiq edir.</summary>
    public static void Apply(Sentry.AspNetCore.SentryAspNetCoreOptions options)
    {
        options.SendDefaultPii = false;
        options.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;
        options.EnableLogs = false;
        options.AddLogEntryFilter(SuppressMiddlewareLogger);
        options.SetBeforeSend(FilterEvent);
        options.SetBeforeSendTransaction(FilterTransaction);
    }

    /// <summary>Middleware xətanı ayrıca CaptureException ilə göndərir; eyni logger hadisəsindən ikinci monitorinq qeydi yaradılmır.</summary>
    public static bool SuppressMiddlewareLogger(string? category, LogLevel level, EventId eventId, Exception? error)
        => string.Equals(category, typeof(ExceptionHandlingMiddleware).FullName, StringComparison.Ordinal);

    /// <summary>Sentry-nin emal etdiyi payload-dan yalnız diaqnostik sahələri seçir; orijinal exception obyektinə toxunmur.</summary>
    public static SentryEvent? FilterEvent(SentryEvent source)
    {
        try
        {
            var input = Snapshot(writer => source.WriteTo(writer, null));
            var output = Pick(input, EventFields);
            output["message"] = new JsonObject { ["formatted"] = "ECafe diagnostic event" };
            AddContext(input, output);
            if (input["exception"]?["values"] is JsonArray exceptions)
            {
                var values = new JsonArray();
                foreach (var value in exceptions.OfType<JsonObject>())
                {
                    var clean = Pick(value, ["type", "module", "thread_id", "exception_id", "parent_id"]);
                    if (value["type"]?.GetValue<string>()?.EndsWith(nameof(SafeDiagnosticException), StringComparison.Ordinal) == true
                        && value["value"]?.GetValue<string>() is { Length: > 0 and <= 128 } code
                        && code.EndsWith("Exception", StringComparison.Ordinal)
                        && code.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
                        clean["type"] = code;
                    clean["value"] = "Exception details omitted";
                    if (value["mechanism"] is JsonObject mechanism)
                        clean["mechanism"] = Pick(mechanism, ["type", "handled", "synthetic"]);
                    if (value["stacktrace"]?["frames"] is JsonArray frames)
                        clean["stacktrace"] = new JsonObject { ["frames"] = new JsonArray(frames.OfType<JsonObject>()
                            .Select(frame => (JsonNode)Pick(frame, FrameFields)).ToArray()) };
                    values.Add(clean);
                }
                output["exception"] = new JsonObject { ["values"] = values };
            }
            return SentryEvent.FromJson(JsonSerializer.SerializeToElement(output));
        }
        catch { return null; }
    }

    /// <summary>Trace vaxtlarını və span ID-lərini qoruyur; SQL/HTTP təsviri, request payload-u və sərbəst span datası göndərilmir.</summary>
    public static SentryTransaction? FilterTransaction(SentryTransaction source)
    {
        try
        {
            var input = Snapshot(writer => source.WriteTo(writer, null));
            var output = Pick(input, [.. EventFields, "type", "start_timestamp"]);
            output["transaction"] = "ECafe request";
            AddContext(input, output);
            if (input["spans"] is JsonArray spans)
                output["spans"] = new JsonArray(spans.OfType<JsonObject>().Select(span => (JsonNode)Pick(span,
                    [.. TraceFields, "timestamp", "start_timestamp"])).ToArray());
            return SentryTransaction.FromJson(JsonSerializer.SerializeToElement(output));
        }
        catch { return null; }
    }

    /// <summary>Request-dən yalnız HTTP metodu və queriesiz origin saxlayır; correlation ID qalır, headers/cookie/user/extra/breadcrumb çıxarılır.</summary>
    private static void AddContext(JsonObject source, JsonObject target)
    {
        if (source["contexts"]?["trace"] is JsonObject trace)
            target["contexts"] = new JsonObject { ["trace"] = Pick(trace, TraceFields) };
        if (source["request"] is JsonObject request)
        {
            var clean = Pick(request, ["method"]);
            if (Uri.TryCreate(request["url"]?.GetValue<string>(), UriKind.Absolute, out var uri)
                && uri.Scheme is "https" or "http")
                clean["url"] = uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);
            target["request"] = clean;
        }
        if (source["tags"] is JsonObject tags)
        {
            var clean = new JsonObject();
            foreach (var key in new[] { "category", "event", "severity", "traceId", "method" })
                if (tags[key]?.GetValue<string>() is { Length: > 0 and <= 128 } value
                    && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.' or ':'))
                    clean[key] = value;
            target["tags"] = clean;
        }
    }

    /// <summary>SDK modelini strukturlaşdırılmış JSON-a çevirir; məxfi payload heç bir fayla və ya logger-ə yazılmır.</summary>
    private static JsonObject Snapshot(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) write(writer);
        return JsonNode.Parse(stream.ToArray())!.AsObject();
    }

    /// <summary>Yalnız adı açıq seçilmiş sahələrin müstəqil nüsxəsini yaradır; naməlum payload avtomatik ötürülmür.</summary>
    private static JsonObject Pick(JsonObject source, string[] keys)
    {
        var result = new JsonObject();
        foreach (var key in keys)
            if (source[key] is { } value) result[key] = value.DeepClone();
        return result;
    }
}
