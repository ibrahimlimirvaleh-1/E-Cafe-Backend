using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace ECafe.Shared.Extensions;

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        var type = value.GetType();
        var name = value.ToString();

        var field = type.GetField(name);
        if (field == null)
            return name;

        var attribute = field.GetCustomAttribute<DescriptionAttribute>();

        return attribute?.Description ?? name;
    }
    public static string GetName(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());

        var attribute = field?.GetCustomAttribute<DisplayAttribute>();

        if (!string.IsNullOrWhiteSpace(attribute?.Name))
            return attribute!.Name!;
        
        var description = field?.GetCustomAttribute<DescriptionAttribute>();

        return description?.Description ?? value.ToString();
    }

    public static string GetValueDescription(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());

        var attribute = field?.GetCustomAttribute<DisplayAttribute>();

        if (!string.IsNullOrWhiteSpace(attribute?.Description))
            return attribute!.Description!;

        var description = field?.GetCustomAttribute<DescriptionAttribute>();

        return description?.Description ?? string.Empty;
    }
}
