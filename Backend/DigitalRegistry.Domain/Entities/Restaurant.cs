namespace DigitalRegistry.Domain.Entities
{
    public class Restaurant : EntityBase
    {
        public Guid OwnerId { get; set; }
        public required string Name { get; set; }
        public DateTime openFrom { get; set; }
        public DateTime openTo { get; set; }
    }
}
