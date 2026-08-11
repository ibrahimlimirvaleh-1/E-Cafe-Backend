using ECafe.Domain.Entities.Base;

namespace ECafe.Domain.Entities
{
    public class FileType : AuditableSoftDeletableEntity<int>
    {
        public string Name { get; set; } = null!;

        public string Code { get; set; } = null!;

        public bool IsPublic { get; set; }

        public string AllowedExtensions { get; set; } = null!;

        public string AllowedMimeTypes { get; set; } = null!;

        public int MaxSizeMb { get; set; }

        public virtual ICollection<File> Files { get; set; } = new List<File>();
    }
}
