namespace DigitalRegistry.Domain.Entities
{
    public class TableReservation : EntityBase
    {
        public Guid TableId { get; set; }
        public DateTime ReservedFrom { get; set; }
        public DateTime ReservedTo { get; set; }
    }
}
