using System.Reflection;
using System.Text.Json;
using ECafe.Api.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;
using ApiRoutes = ECafe.Application.Routes.Routes;

namespace ECafe.Tests;

public class ControllerRouteTests
{
    [Fact]
    public void Centralization_preserves_existing_endpoint_contracts()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "ControllerRoutes.contract.json");
        var expected = JsonSerializer.Deserialize<EndpointContract[]>(File.ReadAllText(fixturePath));

        Assert.NotNull(expected);
        Assert.Equal(Sort(expected), Sort(GetEndpoints()));
    }

    [Fact]
    public void Every_controller_endpoint_exists_in_the_central_route_catalog()
    {
        var templates = GetRouteGroups(typeof(ApiRoutes))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(templates);
        Assert.All(GetEndpoints(), endpoint => Assert.Contains(endpoint.Template, templates));
    }

    private static IEnumerable<EndpointContract> GetEndpoints()
        => typeof(BaseController).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(BaseController)))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>()
                    .SelectMany(attribute => attribute.HttpMethods.Select(verb => new EndpointContract(
                        type.Name, method.Name, verb, attribute.Template ?? string.Empty)))));

    private static EndpointContract[] Sort(IEnumerable<EndpointContract> endpoints)
        => endpoints.OrderBy(endpoint => endpoint.Controller, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.Action, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.HttpMethod, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.Template, StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<Type> GetRouteGroups(Type type)
    {
        yield return type;
        foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
        foreach (var group in GetRouteGroups(nested))
            yield return group;
    }

    public sealed record EndpointContract(string Controller, string Action, string HttpMethod, string Template);
}
