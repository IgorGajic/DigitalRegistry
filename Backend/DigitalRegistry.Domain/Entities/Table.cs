namespace DigitalRegistry.Domain.Entities
{
    public class Table : EntityBase
    {
        public Guid RestaurantId { get; set; }
        public int NumberOfSeats { get; set; }
    }
}
