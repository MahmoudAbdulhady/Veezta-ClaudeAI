using Domain.DTOS;

namespace Application.Contracts
{
    public interface IDoctorService
    {
        Task<bool> DoctorLoginAsync(LoginDTO model);
        Task<(IEnumerable<DoctorBookingsDTO>, int totalCounts)> GetAppointmentsForDoctorAsync(int doctorId, PaginationAndSearchDTO request);
        Task<bool> AddDoctorAppointmentAsync(AddAppointmentDTO doctorDTO);
        Task<bool> DeleteTimeAppointmentAsync(int appointmentId);
        Task<bool> DoctorUpdateAppointmentAsync(int appointmentId, UpdateAppointmentDTO model);
        Task<bool> DoctorConfirmCheckUpAsync(int bookingId);

        // New: resolves a Doctor table ID from an Identity user ID (used in controller JWT flow)
        Task<int?> GetDoctorIdByUserIdAsync(string userId);
        Task<bool> DoctorRegisterAsync(DoctorRegisterDTO model);
        Task<IEnumerable<SpecializationDTO>> GetSpecializationsAsync();
        Task<UserProfileDTO> GetMyProfileAsync(string userId);
        Task UpdateMyProfileAsync(string userId, UpdateUserProfileDTO dto);
    }
}
