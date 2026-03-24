using Application.Contracts;
using Application.Services;
using Domain.DTOS;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
    }
}
