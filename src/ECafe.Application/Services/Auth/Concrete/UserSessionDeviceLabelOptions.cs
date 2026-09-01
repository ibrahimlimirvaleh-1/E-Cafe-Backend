namespace ECafe.Application.Services.Auth.Concrete;

public sealed class UserSessionDeviceLabelOptions
{
    public string? UnknownDeviceLabel { get; set; }

    public string? UnknownBrowserLabel { get; set; }

    public string? UnknownPlatformLabel { get; set; }

    public List<UserAgentLabelPattern> Browsers { get; set; } = [];

    public List<UserAgentLabelPattern> Platforms { get; set; } = [];
}

public sealed class UserAgentLabelPattern
{
    public string? Pattern { get; set; }

    public string? Label { get; set; }
}
