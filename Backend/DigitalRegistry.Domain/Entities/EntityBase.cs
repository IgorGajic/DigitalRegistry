namespace DigitalRegistry.Domain.Entities
{
    public class EntityBase
    {
        public Guid Id { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime Modified { get; set; } = DateTime.Now;
        public bool Active { get; set; }
    }
}
