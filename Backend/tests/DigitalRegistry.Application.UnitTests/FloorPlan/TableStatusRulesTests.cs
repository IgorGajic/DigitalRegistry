using DigitalRegistry.Application.Features.Tables;
using DigitalRegistry.Domain.Enums;
using Xunit;

namespace DigitalRegistry.Application.UnitTests.FloorPlan;

/// <summary>
/// The precedence between the three signals a table can carry at once.
/// </summary>
/// <remarks>
/// Two screens read this — the guest-facing availability search and the waiter's floor plan — so the
/// order in which the signals win is not an implementation detail; it is what both screens agree on.
/// </remarks>
public class TableStatusRulesTests
{
    [Fact]
    public void OutOfService_BeatsEverythingElse()
    {
        // A table taken out of service is reported as such even if history left a tab or a booking
        // attached to it; nobody should be sent to sit at it.
        Assert.Equal(
            TableStatus.OutOfService,
            TableStatusRules.Determine(isActive: false, isOccupied: true, isReserved: true));
    }

    [Fact]
    public void Occupied_BeatsReserved()
    {
        // Guests already sitting there are the problem to deal with first; the booking is the next
        // one.
        Assert.Equal(
            TableStatus.Occupied,
            TableStatusRules.Determine(isActive: true, isOccupied: true, isReserved: true));
    }

    [Fact]
    public void Reserved_WhenBookedButEmpty()
    {
        Assert.Equal(
            TableStatus.Reserved,
            TableStatusRules.Determine(isActive: true, isOccupied: false, isReserved: true));
    }

    [Fact]
    public void Available_WhenNothingHoldsIt()
    {
        Assert.Equal(
            TableStatus.Available,
            TableStatusRules.Determine(isActive: true, isOccupied: false, isReserved: false));
    }
}
