namespace ECafe.Application.DTOs.User
{
    public class RoleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public bool IsStaffAssignable { get; set; }
    }
}
