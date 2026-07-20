namespace NajaEcho.Domain.Users;

/// <summary>
/// The single source of truth for role names. Policy registration, role seeding, and
/// admin-assignment validation all derive from <see cref="All"/> — adding a role here is
/// the only edit required.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Quartermaster = "Quartermaster";
    public const string CrewResourceOfficer = "CrewResourceOfficer";

    /// <summary>Every role, in display order. <see cref="Admin"/> is first by convention.</summary>
    public static readonly IReadOnlyList<string> All = [Admin, Quartermaster, CrewResourceOfficer];
}
