using FluentAssertions;
using NajaEcho.Application.Features.Loot;
using Xunit;

namespace NajaEcho.Application.Tests.Features.Loot;

public sealed class ClaimPriorityTests
{
    [Fact]
    public void BothZero_Returns_Zero()
    {
        var result = ClaimPriority.Compute(0, 0);
        result.Should().BeApproximately(0.00, 0.001);
    }

    [Fact]
    public void LootTotalZero_OrgPositive_DividesBy100()
    {
        var result = ClaimPriority.Compute(150, 0);
        result.Should().BeApproximately(1.50, 0.001);
    }

    [Fact]
    public void NormalRatio()
    {
        var result = ClaimPriority.Compute(150, 300);
        result.Should().BeApproximately(0.50, 0.001);
    }

    [Fact]
    public void NegativeOrgPoints()
    {
        var result = ClaimPriority.Compute(-20, 100);
        result.Should().BeApproximately(-0.20, 0.001);
    }
}
