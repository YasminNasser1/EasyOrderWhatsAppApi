namespace EasyOrderWhatsAppDAL.Entities
{
    public abstract class BaseEntity
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        protected void MarkAsUpdated() => UpdatedAt = DateTime.Now;
    }
}
