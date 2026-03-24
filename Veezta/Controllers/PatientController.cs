using Application.Contracts;
using Application.DTOS;
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
    // FIX: Changed base class from Controller to ControllerBase —
    // Controller adds MVC/Razor view support which is unnecessary for a pure API.
    public class PatientController : ControllerBase
    {
        private readonly IPatientService _patientService;
        private readonly UserManager<CustomUser> _userManager;
        private readonly TokenService _tokenService;

        public PatientController(
            IPatientService patientService,
            UserManager<CustomUser> userManager,
            TokenService tokenService)
        {
            _patientService = patientService;
            _userManager = userManager;
            _tokenService = tokenService;
        }

        /// <summary>
        /// Registers a new patient account.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("Register")]
        public async Task<IActionResult> PatientRegister([FromForm] PatientRegisterDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _patientService.RegisterPatientAsync(model);
            if (result)
                return Ok("Patient registered successfully.");

            return BadRequest("Registration failed.");
        }

        /// <summary>
        /// Authenticates a patient and returns a JWT token.
        /// FIX: Now returns a signed JWT token instead of a plain success string.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("Login")]
        public async Task<IActionResult> PatientLogin([FromBody] LoginDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _patientService.LoginPatientAsync(model);
            if (!result)
                return Unauthorized("Invalid email or password.");

            var user = await _userManager.FindByEmailAsync(model.Email);
            var token = await _tokenService.GenerateTokenAsync(user!);

            return Ok(new { Token = token, Message = "Login successful." });
        }

        /// <summary>
        /// Returns available appointment slots, with optional search and pagination.
        /// FIX: Changed from POST to GET — read operations should use GET.
        /// </summary>
        [Authorize(Roles = "Patient")]
        [HttpGet("GetDoctorAppointments")]
        public async Task<IActionResult> GetDoctorAppointments([FromQuery] PaginationAndSearchDTO request)
        {
            request.PageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
            request.PageSize = request.PageSize < 1 ? 10 : request.PageSize;

            var (appointments, totalCounts) = await _patientService.GetAppointmentsForDoctorAsync(request);
            return Ok(new { appointments, totalCounts });
        }

        /// <summary>
        /// Books an appointment slot for the authenticated patient.
        /// FIX: PatientId is now taken from the JWT claim instead of the request body,
        /// preventing a patient from booking on behalf of another patient.
        /// </summary>
        [Authorize(Roles = "Patient")]
        [HttpPost("BookAppointment")]
        public async Task<IActionResult> BookAppointment([FromForm] CreateBookingDTO bookingModel)
        {
            // Derive the patient's ID from their JWT claim — never trust the client to send it
            var patientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(patientId))
                return Unauthorized();

            await _patientService.CreateNewBookingAsync(
                bookingModel.AppointmentId,
                patientId,
                bookingModel.CouponName);

            return Ok($"Appointment slot ID {bookingModel.AppointmentId} booked successfully.");
        }

        /// <summary>
        /// Cancels a pending booking.
        /// FIX: Changed from POST to DELETE — cancellation is a delete-style operation.
        /// </summary>
        [Authorize(Roles = "Patient")]
        [HttpDelete("CancelAppointment/{bookingId:int}")]
        public async Task<IActionResult> CancelBooking(int bookingId)
        {
            await _patientService.CancelBookingAsync(bookingId);
            return Ok($"Booking ID {bookingId} was cancelled successfully.");
        }

        /// <summary>
        /// Returns all bookings for the currently authenticated patient.
        /// FIX: PatientId now read from JWT claim — a patient can only see their own bookings.
        /// </summary>
        [Authorize(Roles = "Patient")]
        [HttpGet("MyBookings")]
        public async Task<IActionResult> GetMyBookings()
        {
            var patientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(patientId))
                return Unauthorized();

            var bookings = await _patientService.GetPatientSpecificBookingsAsync(patientId);
            return Ok(bookings);
        }
    }
}
