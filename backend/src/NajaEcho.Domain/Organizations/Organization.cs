namespace NajaEcho.Domain.Organizations;

/// <summary>
/// A tenant boundary. Carries a human-readable name used by admins to tell organizations apart.
/// </summary>
/// <remarks>
/// The name is deliberately not unique: this release ships only the default "Naja Echo"
/// organization and offers no path to create a second, so a uniqueness constraint would encode
/// a rule the product does not actually enforce.
/// </remarks>
public sealed class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
