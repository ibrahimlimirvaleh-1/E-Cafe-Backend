namespace ECafe.Application.DTOs.Restaurant
{
    public class RoleDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public bool IsStaffAssignable { get; set; }
    }
}
