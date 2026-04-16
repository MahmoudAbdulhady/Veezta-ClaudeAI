namespace Domain.DTOS
{
    public class UserProfileDTO
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Gender { get; set; }
        public string DateOfBirth { get; set; }
        public string? ImageUrl { get; set; }
    }
}
