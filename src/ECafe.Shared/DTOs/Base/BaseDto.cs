namespace ECafe.Shared.DTOs.Base
{
    public class BaseDto<TKey>
    {
        public TKey Id { get; set; } = default!;
    }
}
