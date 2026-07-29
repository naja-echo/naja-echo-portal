namespace NajaEcho.Domain.Organizations;

/// <summary>
/// The single organization this release produces, seeded by the AddOrganizations migration.
/// </summary>
/// <remarks>
/// The id is a fixed literal rather than a generated value so the migration's seed can be guarded
/// with <c>ON CONFLICT (id) DO NOTHING</c> and tests have a stable value to assert against.
///
/// This literal is duplicated in the migration SQL — the two are a matched pair. A mismatch would
/// create a second organization on every deploy while leaving this constant pointing at a row that
/// does not exist.
/// </remarks>
public static class DefaultOrganization
{
    public static readonly Guid Id = new("9b8ac811-3cec-421c-8cfb-cc56f775ad5a");
    public const string Name = "Naja Echo";
}
