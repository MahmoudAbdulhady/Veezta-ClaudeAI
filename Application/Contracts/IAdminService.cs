using Domain.DTOS;

namespace Application.Contracts
{
    public interface IAdminService
    {
        Task<bool> AddDocotorAsync(DoctorRegisterDTO model);
        Task<IEnumerable<SpecializationDTO>> GetAllSpecializationsAsync();
        Task<SpecializationDTO> AddSpecializationAsync(string name);
        Task<bool> DeleteSpecializationAsync(int id);
        Task<DoctorDTO> GetDoctorByIdAsync(int doctorId);
        Task<bool> DeleteDoctorAsync(int doctorId);
        int GetTotalNumOfDoctors();
        Task<bool> DoctorUpdateAsync(DoctorUpdateDTO model);
        Task<(IEnumerable<DoctorDTO>, int TotalCount)> GetAllDoctorsAsync(PaginationAndSearchDTO request);
        Task<int> TotalNumberOfPatients();
        Task<(IEnumerable<PatientDTO>, int)> GetAllPatientsAsync(PaginationAndSearchDTO request);
        Task<PatientDTO> GetPatientByIdAsync(string patientId);
        Task<IEnumerable<TopFiveSpecalizationDTO>> GetTopSpecializationsAsync();
        Task<IEnumerable<TopTenDoctorDTO>> GetTopTenDoctorsAsync();
        Task<RequestsDTO> GetNumberOfRequestsAsync();
        Task<int> GetNumberOfDoctorsAddedLast24HoursAsync();
        Task<UserProfileDTO> GetMyProfileAsync(string userId);
        Task UpdateMyProfileAsync(string userId, UpdateUserProfileDTO dto);
    }
}
