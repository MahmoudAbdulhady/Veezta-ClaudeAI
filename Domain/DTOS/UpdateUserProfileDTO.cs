using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Domain.DTOS
{
    public class UpdateUserProfileDTO
    {
        [Required(ErrorMessage = "FirstName is required")]
        [MaxLength(100)]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "LastName is required")]
        [MaxLength(100)]
        public string LastName { get; set; }

        [Phone]
        [Required(ErrorMessage = "PhoneNumber is required")]
        [RegularExpression(@"^\+\d{1,3}\d{10}$", ErrorMessage = "Invalid phone number format.")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        public int Gender { get; set; } // 0 = Female, 1 = Male

        [Required(ErrorMessage = "DateOfBirth is required")]
        public string DateOfBirth { get; set; }

        public IFormFile? ImageUrl { get; set; }
    }
}
