using Domain.DTOS;
using Domain.Entities;
using Domain.Enums;

namespace Domain.Interfaces
{
    public interface IAdminRepository
    {
        // Doctor management
        Task CreateNewDoctorAsync(Doctor doctor);
        Task<Doctor> GetDoctorByIdAsync(int doctorId);
        Task<bool> DeleteDoctorAsync(Doctor doctor);
        Task<bool> DoctorUpdateAsync(Doctor doctor);
        int NumOfDoctors();
        Task<(IEnumerable<Doctor>, int TotalCount)> GetAllDoctorsAsync(PaginationAndSearchDTO request);

        // Patient management
        Task<int> NumberOfPatients();
        Task<(IEnumerable<CustomUser>, int)> GetAllPatientsAsync(PaginationAndSearchDTO request);
        Task<CustomUser> GetPatientById(string patientId);

        // Dashboard stats — now return DTOs directly (grouping done in DB)
        Task<IEnumerable<TopFiveSpecalizationDTO>> GetTopFiveSpecalizationsAsync();
        Task<IEnumerable<TopTenDoctorDTO>> GetTopTenDoctors();
        Task<RequestsDTO> GetNumberOfRequests();
        Task<int> GetNumberOfDoctorsAddedLast24HoursAsync();

        // Appointment/booking helpers
        Task<bool> GetBookingByDoctorId(int doctorId);
        Task<Appointement> GetAppointmentByDoctorId(int doctorId);
        Task<bool> DeleteDoctorAppointmentAsync(Appointement doctorAppointment);

        // FIX: new method to delete ALL appointments for a doctor
        Task DeleteAllDoctorAppointmentsAsync(int doctorId);
    }
}
