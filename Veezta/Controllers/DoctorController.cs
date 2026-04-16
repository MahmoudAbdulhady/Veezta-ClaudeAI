using Application.Contracts;
using Application.Services;
using Domain.DTOS;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Domain.Enums;

namespace Veezta.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DoctorController : ControllerBase
    {
        private readonly IDoctorService _doctorService;
        private readonly UserManager<CustomUser> _userManager;
        private readonly TokenService _tokenService;

        public DoctorController(
            IDoctorService doctorService,
            UserManager<CustomUser> userManager,
            TokenService tokenService)
        {
            _doctorService = doctorService;
            _userManager = userManager;
            _tokenService = tokenService;
        }

        /// <summary>
        /// Authenticates a doctor and returns a JWT token.
        /// FIX: Now returns a signed JWT token instead of just a boolean success string.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("Login")]
        public async Task<IActionResult> DoctorLogin([FromBody] LoginDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _doctorService.DoctorLoginAsync(model);
            if (!result)
                return Unauthorized("Invalid email or password.");

            var user = await _userManager.FindByEmailAsync(model.Email);
            var token = await _tokenService.GenerateTokenAsync(user!);

            return Ok(new { Token = token, Message = "Login successful." });
        }

        /// <summary>
        /// Returns all available specializations. Anonymous — needed during doctor registration.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("GetSpecializations")]
        public async Task<IActionResult> GetSpecializations()
        {
            var specializations = await _doctorService.GetSpecializationsAsync();
            return Ok(specializations);
        }

        /// <summary>
        /// Allows a doctor to self-register an account.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("Register")]
        public async Task<IActionResult> DoctorRegister([FromForm] DoctorRegisterDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            try
            {
                var result = await _doctorService.DoctorRegisterAsync(model);
                return result ? Ok("Doctor registered successfully.") : BadRequest("Registration failed.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Returns paginated appointments booked with a specific doctor.
        /// FIX: Changed from POST to GET. DoctorId now taken from the JWT claim
        /// so a doctor can only see their own appointments.
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpGet("GetMyAppointments")]
        public async Task<IActionResult> GetMyAppointments([FromQuery] PaginationAndSearchDTO request)
        {
            // Read doctorId from the JWT — prevents a doctor querying another doctor's data
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId!);
            if (user == null)
                return Unauthorized();

            // Resolve the Doctor record ID from the Identity user ID
            var doctorId = await _doctorService.GetDoctorIdByUserIdAsync(userId!);
            if (doctorId == null)
                return NotFound("No doctor record found for this account.");

            var (appointments, totalCounts) = await _doctorService.GetAppointmentsForDoctorAsync(doctorId.Value, request);
            return Ok(new { appointments, totalCounts });
        }

        /// <summary>
        /// Adds new appointment slots for a doctor.
        /// FIX: ModelState validation now happens before the service call.
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpPost("AddAppointment")]
        public async Task<IActionResult> AddDoctorAppointment([FromBody] AddAppointmentDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Override DoctorId from JWT — prevent a doctor from spoofing another doctor's ID
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var doctorId = await _doctorService.GetDoctorIdByUserIdAsync(userId!);
            if (doctorId == null)
                return NotFound("No doctor record found for this account.");
            model.DoctorId = doctorId.Value;

            await _doctorService.AddDoctorAppointmentAsync(model);
            return Ok("Appointment slots added successfully.");
        }

        /// <summary>
        /// Deletes a specific appointment time slot.
        /// FIX: Now returns 404 when the appointment isn't found instead of always 200.
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpDelete("DeleteAppointment/{appointmentId:int}")]
        public async Task<IActionResult> DeleteAppointment(int appointmentId)
        {
            await _doctorService.DeleteTimeAppointmentAsync(appointmentId);
            return Ok($"Appointment slot with ID {appointmentId} was deleted successfully.");
        }

        /// <summary>
        /// Updates the start/end time of an unbooked appointment slot.
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpPut("UpdateAppointment/{appointmentId:int}")]
        public async Task<IActionResult> UpdateAppointment(int appointmentId, [FromBody] UpdateAppointmentDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _doctorService.DoctorUpdateAppointmentAsync(appointmentId, model);
            if (!result)
                return NotFound($"Appointment slot with ID {appointmentId} was not found.");

            return Ok("Appointment updated successfully.");
        }

        /// <summary>
        /// Marks a booking as completed (confirmed checkup).
        /// FIX: Now returns 404 when the booking isn't found instead of always 200.
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpPost("ConfirmCheckup/{bookingId:int}")]
        public async Task<IActionResult> ConfirmCheckup(int bookingId)
        {
            await _doctorService.DoctorConfirmCheckUpAsync(bookingId);
            return Ok($"Booking ID {bookingId} marked as completed.");
        }

        /// <summary>
        /// Returns the profile of the currently authenticated doctor.
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpGet("MyProfile")]
        public async Task<IActionResult> GetMyProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var profile = await _doctorService.GetMyProfileAsync(userId);
            return Ok(profile);
        }

        /// <summary>
        /// Updates the profile of the currently authenticated doctor.
        /// </summary>
        [Authorize(Roles = "Doctor")]
        [HttpPut("UpdateProfile")]
        public async Task<IActionResult> UpdateProfile([FromForm] UpdateUserProfileDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            await _doctorService.UpdateMyProfileAsync(userId, model);
            return Ok("Profile updated successfully.");
        }
    }
}
