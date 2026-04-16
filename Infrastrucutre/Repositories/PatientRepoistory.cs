using Domain.DTOS;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastrucutre.Repositories
{
    public class PatientRepoistory : IPatientRepository
    {
        private readonly VeeztaDbContext _veeztaDbContext;
        private readonly IDoctorRepository _doctorRepository;

        public PatientRepoistory(VeeztaDbContext veeztaDbContext, IDoctorRepository doctorRepository)
        {
            _veeztaDbContext = veeztaDbContext;
            _doctorRepository = doctorRepository;
        }

        /// <summary>
        /// Creates a new booking record in the database.
        /// FIX: Removed duplicate AddBookingAsync method that did the same thing.
        /// </summary>
        public async Task CreateBookingAsync(Booking booking)
        {
            _veeztaDbContext.Bookings.Add(booking);
            await _veeztaDbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Cancels a pending booking. Throws a descriptive error if already completed/cancelled.
        /// FIX: Error message was misleading — said "Pending Appointment" when status was not pending.
        /// </summary>
        public async Task<bool> CancelAppointment(int bookingId)
        {
            var booking = await _veeztaDbContext.Bookings.FindAsync(bookingId);
            if (booking == null)
                throw new Exception($"No booking with ID {bookingId} was found.");

            if (booking.Status == BookingStatus.Pending)
            {
                booking.Status = BookingStatus.Canceled;
                await _veeztaDbContext.SaveChangesAsync();
                return true;
            }

            // FIX: corrected error message — "Pending" was listed as a reason it can't be cancelled,
            // which was wrong. The real reason is it's already Completed or already Cancelled.
            throw new Exception($"Booking ID {bookingId} cannot be cancelled because it is already {booking.Status}.");
        }

        /// <summary>
        /// Finds a booking by its booking ID.
        /// </summary>
        public async Task<Booking> FindyBookingById(int bookingId)
        {
            return await _veeztaDbContext.Bookings
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);
        }

        /// <summary>
        /// Returns true if any booking exists for the given appointment time slot.
        /// </summary>
        public async Task<bool> FindyBookingByAppoitmentId(int appointmentId)
        {
            return await _veeztaDbContext.Bookings
                .AnyAsync(b => b.AppointmentId == appointmentId);
        }

        /// <summary>
        /// Returns a paginated list of available appointment time slots with doctor info.
        /// Only includes slots where the Doctor has a valid Specialization — guards against
        /// orphaned FK records that would cause a NullReferenceException in the service
        /// grouping logic when no search term is provided.
        /// </summary>
        public async Task<(IEnumerable<Time>, int totalCounts)> GetDoctorApptAsync(PaginationAndSearchDTO request)
        {
            var query = _veeztaDbContext.Times
                .Include(a => a.Appointement)
                .Include(a => a.Appointement.Doctor)
                .Include(a => a.Appointement.Doctor.User)
                .Include(a => a.Appointement.Doctor.Specialization)
                // FIX: exclude slots whose Doctor has no valid Specialization record.
                // Without this, an unfiltered load returns all Time rows in-memory,
                // and the service GroupBy crashes on null Specialization navigation.
                .Where(t => t.Appointement.Doctor.Specialization != null)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                query = query.Where(d =>
                    d.Appointement.Doctor.User.FullName.Contains(request.SearchTerm) ||
                    d.Appointement.Doctor.User.Email.Contains(request.SearchTerm) ||
                    d.Appointement.Doctor.Specialization.SpecializationName.Contains(request.SearchTerm));
            }

            int totalCount = await query.CountAsync();
            var times = await query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            return (times, totalCount);
        }

        /// <summary>
        /// FIX: Replaces GetPatientBookings() which loaded ALL bookings then filtered in-memory.
        /// Now filters by patientId directly in the database query.
        /// </summary>
        public async Task<IEnumerable<Time>> GetPatientBookingsByPatientId(string patientId)
        {
            return await _veeztaDbContext.Times
                .Include(t => t.Appointement)
                .Include(t => t.Appointement.Booking)
                .Include(t => t.Appointement.Doctor)
                .Include(t => t.Appointement.Doctor.User)
                .Include(t => t.Appointement.Doctor.Specialization)
                .Where(t => t.Appointement.Booking != null &&
                            t.Appointement.Booking.PatientId == patientId)
                .ToListAsync();
        }
    }
}
