using Application.Contracts;
using Domain.DTOS;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Veezta.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]   // FIX: all admin endpoints now require the Admin role
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminServices;

        public AdminController(IAdminService adminServices)
        {
            _adminServices = adminServices;
        }

        /// <summary>
        /// Adds a new doctor. Sends a welcome email with their credentials.
        /// </summary>
        [HttpPost("AddDoctor")]
        public async Task<IActionResult> AddDoctor([FromForm] DoctorRegisterDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _adminServices.AddDocotorAsync(model);
            if (result)
                return Ok("Doctor registered successfully.");

            return BadRequest("Doctor registration failed.");
        }

        /// <summary>
        /// Returns a single doctor by their ID.
        /// </summary>
        [HttpGet("GetDoctorById/{doctorId:int}")]
        public async Task<IActionResult> GetDoctorById(int doctorId)
        {
            var doctor = await _adminServices.GetDoctorByIdAsync(doctorId);
            if (doctor == null)
                return NotFound($"Doctor with ID {doctorId} was not found.");

            return Ok(doctor);
        }

        /// <summary>
        /// Deletes a doctor. Fails if the doctor has existing bookings.
        /// </summary>
        [HttpDelete("DeleteDoctorById/{doctorId:int}")]
        public async Task<IActionResult> DeleteDoctorById(int doctorId)
        {
            var result = await _adminServices.DeleteDoctorAsync(doctorId);
            if (!result)
                return NotFound($"Doctor with ID {doctorId} was not found.");

            return Ok($"Doctor with ID {doctorId} was deleted successfully.");
        }

        /// <summary>
        /// Updates an existing doctor's profile information.
        /// </summary>
        [HttpPut("UpdateDoctorInfo")]
        public async Task<IActionResult> UpdateDoctorInfo([FromForm] DoctorUpdateDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var success = await _adminServices.DoctorUpdateAsync(model);
            if (!success)
                return NotFound($"Doctor with ID {model.DoctorId} was not found.");

            return Ok($"Doctor with ID {model.DoctorId} updated successfully.");
        }

        /// <summary>
        /// Returns a paginated, searchable list of all doctors.
        /// FIX: Changed from POST to GET — read operations should use GET.
        /// Pagination params moved to query string.
        /// </summary>
        [HttpGet("GetAllDoctors")]
        public async Task<IActionResult> GetAllDoctors([FromQuery] PaginationAndSearchDTO request)
        {
            request.PageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
            request.PageSize = request.PageSize < 1 ? 10 : request.PageSize;

            var (doctors, totalCount) = await _adminServices.GetAllDoctorsAsync(request);
            return Ok(new { doctors, totalCount });
        }

        /// <summary>
        /// Returns the total number of registered doctors.
        /// </summary>
        [HttpGet("NumOfDoctors")]
        public IActionResult NumOfDoctors()
        {
            return Ok(new { TotalDoctors = _adminServices.GetTotalNumOfDoctors() });
        }

        /// <summary>
        /// Returns the total number of registered patients.
        /// </summary>
        [HttpGet("TotalNumberOfPatients")]
        public async Task<IActionResult> TotalNumberOfPatients()
        {
            var count = await _adminServices.TotalNumberOfPatients();
            return Ok(new { TotalPatients = count });
        }

        /// <summary>
        /// Returns a paginated, searchable list of all patients.
        /// FIX: Changed from POST to GET.
        /// </summary>
        [HttpGet("GetAllPatients")]
        public async Task<IActionResult> GetAllPatients([FromQuery] PaginationAndSearchDTO request)
        {
            request.PageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
            request.PageSize = request.PageSize < 1 ? 10 : request.PageSize;

            var (patients, totalPatients) = await _adminServices.GetAllPatientsAsync(request);
            return Ok(new { patients, totalPatients });
        }

        /// <summary>
        /// Returns a single patient by their user ID.
        /// </summary>
        [HttpGet("GetPatientById/{patientId}")]
        public async Task<IActionResult> GetPatientById(string patientId)
        {
            var patient = await _adminServices.GetPatientByIdAsync(patientId);
            if (patient == null)
                return NotFound($"Patient with ID {patientId} was not found.");

            return Ok(patient);
        }

        /// <summary>
        /// Returns the top 5 specializations by number of completed bookings.
        /// </summary>
        [HttpGet("TopFiveSpecializations")]
        public async Task<IActionResult> TopFiveSpecializations()
        {
            var result = await _adminServices.GetTopSpecializationsAsync();
            return Ok(result);
        }

        /// <summary>
        /// Returns the top 10 doctors by number of completed bookings.
        /// </summary>
        [HttpGet("TopTenDoctors")]
        public async Task<IActionResult> TopTenDoctors()
        {
            var result = await _adminServices.GetTopTenDoctorsAsync();
            return Ok(result);
        }

        /// <summary>
        /// Returns a breakdown of booking counts by status (Pending, Completed, Cancelled).
        /// </summary>
        [HttpGet("GetNumberOfRequests")]
        public async Task<IActionResult> GetNumberOfRequests()
        {
            var result = await _adminServices.GetNumberOfRequestsAsync();
            return Ok(result);
        }

        /// <summary>
        /// Returns the number of doctors added in the last 24 hours.
        /// </summary>
        [HttpGet("NumOfDoctorsInTheLast24Hours")]
        public async Task<IActionResult> NumOfDoctorsInTheLast24Hours()
        {
            int count = await _adminServices.GetNumberOfDoctorsAddedLast24HoursAsync();
            return Ok(new { DoctorsAddedLast24Hours = count });
        }
    }
}
