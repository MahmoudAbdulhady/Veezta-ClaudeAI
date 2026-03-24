using Domain.DTOS;
using Domain.Entities;

namespace Domain.Interfaces
{
    public interface IPatientRepository
    {
        Task CreateBookingAsync(Booking booking);
        Task<bool> CancelAppointment(int bookingId);
        Task<Booking> FindyBookingById(int bookingId);
        Task<bool> FindyBookingByAppoitmentId(int appointmentId);
        Task<(IEnumerable<Time>, int totalCounts)> GetDoctorApptAsync(PaginationAndSearchDTO request);

        // FIX: replaces GetPatientBookings() — now filters by patientId in the DB
        Task<IEnumerable<Time>> GetPatientBookingsByPatientId(string patientId);
    }
}
