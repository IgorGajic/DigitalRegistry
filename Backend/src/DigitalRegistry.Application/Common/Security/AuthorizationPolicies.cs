using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Common.Security;

/// <summary>
/// The RBAC matrix, expressed once.
/// </summary>
/// <remarks>
/// Each constant names a policy registered in <c>Program.cs</c>; <see cref="RolesFor"/> supplies the
/// roles that satisfy it. Controllers reference the policy names, so the matrix is defined in exactly
/// one place and the specification can be checked against this file directly.
/// <para>
/// Some rows are narrower than they might first appear and are transcribed deliberately: a waiter
/// cannot cancel a reservation, a manager can neither open a tab nor take a QR order, and only the
/// owner sees financial reports.
/// </para>
/// </remarks>
public static class AuthorizationPolicies
{
    /// <summary>View the menu and item availability. Includes anonymous QR table sessions.</summary>
    public const string ViewMenu = nameof(ViewMenu);

    /// <summary>View which tables are free for a party size and time window.</summary>
    public const string ViewTableAvailability = nameof(ViewTableAvailability);

    /// <summary>Book a table.</summary>
    public const string ReserveTable = nameof(ReserveTable);

    /// <summary>Cancel a reservation. Guests may cancel only their own; managers and owners, any.</summary>
    public const string CancelReservation = nameof(CancelReservation);

    /// <summary>Place an order through a table's QR code.</summary>
    public const string PlaceGuestQrOrder = nameof(PlaceGuestQrOrder);

    /// <summary>Open a tab directly against a table as staff.</summary>
    public const string OpenStaffOrder = nameof(OpenStaffOrder);

    /// <summary>Add, change or remove lines on an active order.</summary>
    public const string ModifyOrder = nameof(ModifyOrder);

    /// <summary>Take payment and close an order.</summary>
    public const string ProcessPayment = nameof(ProcessPayment);

    /// <summary>Create, modify and remove tables, including rotating QR tokens.</summary>
    public const string ManageTables = nameof(ManageTables);

    /// <summary>Assign, change and delete waiter shifts.</summary>
    public const string ManageShifts = nameof(ManageShifts);

    /// <summary>Maintain menu items and their recipes.</summary>
    public const string ManageMenu = nameof(ManageMenu);

    /// <summary>Maintain ingredient stock levels.</summary>
    public const string ManageInventory = nameof(ManageInventory);

    /// <summary>Financial reporting and system auditing.</summary>
    public const string FinancialReports = nameof(FinancialReports);

    private static readonly Dictionary<string, UserRole[]> Matrix = new()
    {
        [ViewMenu] = [UserRole.Guest, UserRole.Waiter, UserRole.Manager, UserRole.Owner],
        [ViewTableAvailability] = [UserRole.Guest, UserRole.Waiter, UserRole.Manager, UserRole.Owner],
        [ReserveTable] = [UserRole.Guest, UserRole.Waiter, UserRole.Manager, UserRole.Owner],
        [CancelReservation] = [UserRole.Guest, UserRole.Manager, UserRole.Owner],
        [PlaceGuestQrOrder] = [UserRole.Guest, UserRole.Owner],
        [OpenStaffOrder] = [UserRole.Waiter, UserRole.Owner],
        [ModifyOrder] = [UserRole.Waiter, UserRole.Owner],
        [ProcessPayment] = [UserRole.Waiter, UserRole.Owner],
        [ManageTables] = [UserRole.Manager, UserRole.Owner],
        [ManageShifts] = [UserRole.Manager, UserRole.Owner],
        [ManageMenu] = [UserRole.Manager, UserRole.Owner],
        [ManageInventory] = [UserRole.Manager, UserRole.Owner],
        [FinancialReports] = [UserRole.Owner]
    };

    /// <summary>Every policy name, for registration at startup.</summary>
    public static IReadOnlyCollection<string> All => Matrix.Keys;

    /// <summary>The roles that satisfy the named policy.</summary>
    public static UserRole[] RolesFor(string policyName) =>
        Matrix.TryGetValue(policyName, out var roles)
            ? roles
            : throw new ArgumentOutOfRangeException(nameof(policyName), policyName, "Unknown authorization policy.");

    /// <summary>The role names that satisfy the named policy, for the authorization system.</summary>
    public static string[] RoleNamesFor(string policyName) =>
        RolesFor(policyName).Select(role => role.ToString()).ToArray();
}
