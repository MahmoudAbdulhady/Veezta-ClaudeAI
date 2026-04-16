using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.DTOS
{
    public class DoctorRegisterDTO
    {
        [Required]
        [EmailAddress]
        [MaxLength(100 , ErrorMessage ="You Should not Exceed 100 Characters")]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]

        public string Password { get; set; }
        [Required]
        [MaxLength(100, ErrorMessage = "You Should not Exceed 100 Characters")]
        public string FirstName { get; set; }

        [Required]
        [MaxLength(100, ErrorMessage = "You Should not Exceed 100 Characters")]
        public string LastName { get; set; }

        [Required]
        [MaxLength(100, ErrorMessage = "You Should not Exceed 100 Characters")]
        public string Specialization { get; set; }

        [Phone]
        [Required]
        [RegularExpression(@"^(\+?20)?01[0125]\d{8}$", ErrorMessage = "Phone number must be a valid Egyptian number (e.g. 01012345678 or +2001012345678).")]
        public string PhoneNumber { get; set; }

        [Required]
        public  string DateOfBirth { get; set; }
       

        [Required]
        public IFormFile ImageUrl { get; set; }

        [Required]
        public Gender Gender { get; set; }
    }
}
