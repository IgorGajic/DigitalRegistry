namespace DigitalRegistry.Domain.Enums;

public enum UserRole
{
    Guest = 1,
    Waiter = 2,
    Manager = 3,
    Owner = 4,

    /// <summary>
    /// Operator of the platform itself, not of any restaurant.
    /// </summary>
    /// <remarks>
    /// Belongs to no tenant: <c>ApplicationUser.RestaurantId</c> is null for these accounts, and they
    /// sign in against the master API only. Deliberately absent from every row of the restaurant
    /// authorization matrix — managing licences is not the same thing as running a till.
    /// </remarks>
    PlatformAdmin = 5
}
