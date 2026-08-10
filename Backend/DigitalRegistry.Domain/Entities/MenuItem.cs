using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Domain.Entities
{
    public class MenuItem : EntityBase
    {
        public Guid MenuId { get; set; }
        public required string ItemName { get; set; }
        public Currency Currency { get; set; }
        public decimal Price { get; set; }
        public string? Description { get; set; }
    }
}
