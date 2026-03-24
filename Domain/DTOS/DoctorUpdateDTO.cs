using Domain.Enums;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Domain.DTOS
{
    public class DoctorUpdateDTO
    {
        [Required]
        public int DoctorId { get; set; }   // FIX: was 'doctorId' (camelCase) — now PascalCase

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; }

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; }

        [Required]
        [MaxLength(100)]
        public string Specilization { get; set; }

        [Phone]
        [Required]
        [RegularExpression(@"^\d{11}$", ErrorMessage = "Phone number must be exactly 11 digits.")]
        public string PhoneNumber { get; set; }

        [Required]
        public string DateOfBirth { get; set; }

        // FIX: Made nullable — image is optional on update (only re-saved when provided)
        public IFormFile? ImageUrl { get; set; }

        [Required]
        public Gender Gender { get; set; }
    }
}
