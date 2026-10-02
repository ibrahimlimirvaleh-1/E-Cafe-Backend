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
    public void Reorganization_preserves_existing_http_endpoint_contracts()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "ControllerRoutes.contract.json");
        var expected = JsonSerializer.Deserialize<EndpointContract[]>(File.ReadAllText(fixturePath));

        Assert.NotNull(expected);
        // Controller ownership may change; the original verb and URL fixture stays unchanged.
        var actual = SortHttpContracts(GetEndpoints());
        Assert.All(SortHttpContracts(expected), endpoint => Assert.Contains(endpoint, actual));
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

    [Fact]
    public void Every_http_route_has_one_controller_action()
    {
        var endpoints = GetEndpoints().ToArray();
        Assert.Equal(endpoints.Length, endpoints.Select(e => (e.HttpMethod, e.Template)).Distinct().Count());
    }

    private static HttpEndpointContract[] SortHttpContracts(IEnumerable<EndpointContract> endpoints)
        => endpoints.Select(endpoint => new HttpEndpointContract(endpoint.HttpMethod, endpoint.Template))
            .OrderBy(endpoint => endpoint.HttpMethod, StringComparer.Ordinal)
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
    private sealed record HttpEndpointContract(string HttpMethod, string Template);
}
