using Domain.DTOS;
using Domain.Entities;

namespace Domain.Interfaces
{
    public interface IDoctorRepository
    {
        Task<(IEnumerable<Appointement>, int)> GetDoctorApptAsync(PaginationAndSearchDTO request, int doctorId);
        Task UpdateDoctorPrice(int doctorId, int newPrice);
        Task<Appointement> AddDoctorAppointment(Appointement appointement);
        Task<Time> UpdateTimeAppointment(Time time);
        Task<Time> FindAppointmentByAppointmentId(int appointmentId);
        Task<Time> DeleteAppointmentTime(Time appointment);
        Task<Time> DoctorAppointmentUpdateAsync(Time timeEntity);
        Task<bool> ConfirmCheckup(int bookingId);
        Task<Booking> FindBookingByAppointmentId(int appointmentId);

        // New: resolves Doctor.DoctorId from Identity user ID
        Task<int?> GetDoctorIdByUserIdAsync(string userId);
    }
}
