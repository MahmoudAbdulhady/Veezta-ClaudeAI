using Domain.DTOS;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastrucutre.Repositories
{
    public class AdminRepository : IAdminRepository
    {
        private readonly VeeztaDbContext _veeztaDbContext;
        private readonly UserManager<CustomUser> _userManager;

        public AdminRepository(UserManager<CustomUser> userManager, VeeztaDbContext veeztaDbContext)
        {
            _veeztaDbContext = veeztaDbContext;
            _userManager = userManager;
        }

        public async Task CreateNewDoctorAsync(Doctor doctor)
        {
            await _veeztaDbContext.Doctors.AddAsync(doctor);
            await _veeztaDbContext.SaveChangesAsync();
        }

        public int NumOfDoctors()
        {
            return _veeztaDbContext.Doctors.Count();
        }

        public async Task<Doctor> GetDoctorByIdAsync(int doctorId)
        {
            return await _veeztaDbContext.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialization)
                .Include(d => d.Appointements)
                .FirstOrDefaultAsync(d => d.DoctorId == doctorId);
        }

        public async Task<bool> GetBookingByDoctorId(int doctorId)
        {
            return await _veeztaDbContext.Bookings
                .Include(a => a.Appointement)
                .Where(a => a.Appointement.DoctorId == doctorId)
                .AnyAsync();
        }

        public async Task<bool> DeleteDoctorAsync(Doctor doctor)
        {
            _veeztaDbContext.Doctors.Remove(doctor);
            await _veeztaDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DoctorUpdateAsync(Doctor doctor)
        {
            _veeztaDbContext.Entry(doctor).State = EntityState.Modified;
            await _veeztaDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<(IEnumerable<Doctor>, int TotalCount)> GetAllDoctorsAsync(PaginationAndSearchDTO request)
        {
            var query = _veeztaDbContext.Doctors
                .Include(d => d.User)
                .Include(d => d.Specialization)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                query = query.Where(d =>
                    d.User.FullName.Contains(request.SearchTerm) ||
                    d.User.Email.Contains(request.SearchTerm) ||
                    d.Specialization.SpecializationName.Contains(request.SearchTerm));
            }

            int totalCount = await query.CountAsync();
            var doctors = await query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            return (doctors, totalCount);
        }

        public async Task<int> NumberOfPatients()
        {
            return await _userManager.Users.CountAsync(u => u.AccountRole == AccountRole.Patient);
        }

        public async Task<(IEnumerable<CustomUser>, int)> GetAllPatientsAsync(PaginationAndSearchDTO request)
        {
            var query = _userManager.Users
                .Where(u => u.AccountRole == AccountRole.Patient)
                .AsQueryable();

            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                query = query.Where(u =>
                    u.FirstName.Contains(request.SearchTerm) ||
                    u.LastName.Contains(request.SearchTerm) ||
                    u.FullName.Contains(request.SearchTerm) ||
                    u.Email.Contains(request.SearchTerm) ||
                    u.PhoneNumber.Contains(request.SearchTerm));
            }

            var totalItems = await query.CountAsync();
            var users = await query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            return (users, totalItems);
        }

        /// <summary>
        /// Returns a patient by ID. Only returns users with the Patient role.
        /// FIX: Previously returned the user even when the role check failed.
        /// </summary>
        public async Task<CustomUser> GetPatientById(string patientId)
        {
            var user = await _userManager.FindByIdAsync(patientId);
            if (user == null)
                return null;

            var roles = await _userManager.GetRolesAsync(user);
            return roles.Contains(AccountRole.Patient.ToString()) ? user : null;
        }

        /// <summary>
        /// Returns the top 5 specializations by completed booking count.
        /// FIX: Now performs grouping, ordering, and Take(5) in the database query
        /// instead of loading all bookings into memory first.
        /// </summary>
        public async Task<IEnumerable<TopFiveSpecalizationDTO>> GetTopFiveSpecalizationsAsync()
        {
            return await _veeztaDbContext.Bookings
                .Where(b => b.Status == BookingStatus.Completed)
                .Include(b => b.Appointement.Doctor.Specialization)
                .GroupBy(b => b.Appointement.Doctor.Specialization.SpecializationName)
                .Select(g => new TopFiveSpecalizationDTO
                {
                    SpecalizationName = g.Key,
                    RequestCount = g.Count()
                })
                .OrderByDescending(x => x.RequestCount)
                .Take(5)
                .ToListAsync();
        }

        /// <summary>
        /// Returns the top 10 doctors by completed booking count.
        /// FIX: Now performs grouping, ordering, and Take(10) in the database query
        /// instead of loading all bookings into memory first.
        /// </summary>
        public async Task<IEnumerable<TopTenDoctorDTO>> GetTopTenDoctors()
        {
            return await _veeztaDbContext.Bookings
                .Where(b => b.Status == BookingStatus.Completed)
                .Include(b => b.Appointement.Doctor.User)
                .Include(b => b.Appointement.Doctor.Specialization)
                .GroupBy(b => new
                {
                    b.Appointement.Doctor.User.FullName,
                    b.Appointement.Doctor.Specialization.SpecializationName,
                    b.Appointement.Doctor.User.ImageUrl
                })
                .Select(g => new TopTenDoctorDTO
                {
                    FullName = g.Key.FullName,
                    Specilization = g.Key.SpecializationName,
                    Image = g.Key.ImageUrl,
                    RequestCount = g.Count()
                })
                .OrderByDescending(x => x.RequestCount)
                .Take(10)
                .ToListAsync();
        }

        /// <summary>
        /// Returns booking counts split by Pending, Completed, and Cancelled.
        /// FIX: Now uses three CountAsync calls in the database instead of
        /// loading all booking statuses into memory.
        /// </summary>
        public async Task<RequestsDTO> GetNumberOfRequests()
        {
            return new RequestsDTO
            {
                NumOfPendingRequest = await _veeztaDbContext.Bookings
                    .CountAsync(b => b.Status == BookingStatus.Pending),
                NumOfCompletedRequest = await _veeztaDbContext.Bookings
                    .CountAsync(b => b.Status == BookingStatus.Completed),
                NumOfCanceledRequest = await _veeztaDbContext.Bookings
                    .CountAsync(b => b.Status == BookingStatus.Canceled)
            };
        }

        public async Task<int> GetNumberOfDoctorsAddedLast24HoursAsync()
        {
            var since = DateTime.UtcNow.AddHours(-24);
            return await _veeztaDbContext.Doctors
                .CountAsync(d => d.CreatedDate >= since);
        }

        /// <summary>
        /// FIX: Returns ONLY the first appointment (for backward compat with old delete path).
        /// Use DeleteAllDoctorAppointmentsAsync for full cleanup.
        /// </summary>
        public async Task<Appointement> GetAppointmentByDoctorId(int doctorId)
        {
            return await _veeztaDbContext.Appointments
                .Where(a => a.DoctorId == doctorId)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// FIX: Deletes ALL appointments for a doctor, not just the first one.
        /// Prevents orphaned appointment rows when a doctor is deleted.
        /// </summary>
        public async Task DeleteAllDoctorAppointmentsAsync(int doctorId)
        {
            var appointments = await _veeztaDbContext.Appointments
                .Where(a => a.DoctorId == doctorId)
                .ToListAsync();

            if (appointments.Any())
            {
                _veeztaDbContext.Appointments.RemoveRange(appointments);
                await _veeztaDbContext.SaveChangesAsync();
            }
        }

        public async Task<bool> DeleteDoctorAppointmentAsync(Appointement doctorAppointment)
        {
            _veeztaDbContext.Appointments.Remove(doctorAppointment);
            await _veeztaDbContext.SaveChangesAsync();
            return true;
        }
    }
}
