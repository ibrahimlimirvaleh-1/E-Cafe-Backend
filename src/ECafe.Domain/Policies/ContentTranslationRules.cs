using ECafe.Domain.Enums;

namespace ECafe.Domain.Policies;

public sealed record ContentTranslationRules(int NameLength, PermissionCode Permission,
    bool AllowsDescription = false);
