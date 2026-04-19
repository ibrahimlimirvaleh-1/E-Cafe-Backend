namespace ECafe.Shared.DTOs.Base
{
    public class DropdownDto<T>
    {
        public T Id { get; set; } = default!;

        public string? Name { get; set; }
    }
}
