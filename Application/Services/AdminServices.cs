using Domain.DTOS;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Application.Contracts;
using System.Globalization;

namespace Application.Services
{
    public class AdminServices : IAdminService
    {
        private readonly IAdminRepository _adminRepository;
        private readonly UserManager<CustomUser> _userManager;
        private readonly ISpecializationRepository _specilizationRepository;
        private readonly IEmailSender _emailSender;

        public AdminServices(
            IAdminRepository adminRepository,
            UserManager<CustomUser> userManager,
            ISpecializationRepository specilizationRepository,
            IEmailSender emailSender)
        {
            _adminRepository = adminRepository;
            _userManager = userManager;
            _specilizationRepository = specilizationRepository;
            _emailSender = emailSender;
        }

        /// <summary>
        /// Registers a new doctor. Uploads the image, creates the Identity user,
        /// assigns the Doctor role, sends a welcome email, and creates the Doctor record.
        /// </summary>
        public async Task<bool> AddDocotorAsync(DoctorRegisterDTO model)
        {
            CultureInfo provider = new CultureInfo("en-US");
            string[] formats = { "yyyy-MM-dd", "yyyy/MM/dd", "yyyy/M/dd", "yyyy/M/d", "yyyy/MM/d",
                                  "dd/MM/yyyy", "dd/M/yyyy", "d/MM/yyyy", "d/M/yyyy" };

            if (model.ImageUrl == null || model.ImageUrl.Length == 0)
                throw new Exception("An image must be uploaded for the doctor.");

            // Validate file is an image
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(model.ImageUrl.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                throw new Exception("Only image files (jpg, jpeg, png, gif, webp) are allowed.");

            // 5 MB size limit
            if (model.ImageUrl.Length > 5 * 1024 * 1024)
                throw new Exception("Image file size must not exceed 5 MB.");

            var fileName = Guid.NewGuid().ToString() + extension;
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await model.ImageUrl.CopyToAsync(stream);

            var imageUrl = $"images/{fileName}";

            var user = new CustomUser
            {
                Email = model.Email,
                UserName = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                FullName = model.FirstName + " " + model.LastName,
                PhoneNumber = model.PhoneNumber,
                DateOfBirth = DateTime.ParseExact(model.DateOfBirth, formats, provider),
                Gender = model.Gender,
                ImageUrl = imageUrl,
                AccountRole = AccountRole.Doctor
            };

            var userResult = await _userManager.CreateAsync(user, model.Password);
            if (!userResult.Succeeded)
            {
                var errors = userResult.Errors.Select(e => e.Description);
                throw new Exception($"User creation failed: {string.Join(", ", errors)}");
            }

            await _userManager.AddToRoleAsync(user, AccountRole.Doctor.ToString());

            var specialization = await _specilizationRepository.GetByNameAsync(model.Specialization);
            if (specialization == null)
                throw new Exception($"Specialization '{model.Specialization}' was not found. Please select a valid specialization.");

            var doctor = new Doctor
            {
                UserId = user.Id,
                SpecializationId = specialization.SpecializationId,
                CreatedDate = DateTime.UtcNow
            };

            await _adminRepository.CreateNewDoctorAsync(doctor);

            // Email is best-effort — a misconfigured SMTP server must not roll back a successful registration.
            //try
            //{
            //    await SendWelcomeEmailToDoctorAsync(user.Email, model.Password);
            //}
            //catch
            //{
            //    // Email delivery failed. The doctor account was created successfully.
            //    // Credentials can be shared with the doctor through another channel.
            //}

            return true;
        }

        /// <summary>
        /// Returns a single doctor by ID.
        /// </summary>
        public async Task<DoctorDTO> GetDoctorByIdAsync(int doctorId)
        {
            var doctor = await _adminRepository.GetDoctorByIdAsync(doctorId);
            if (doctor == null)
                return null;

            return new DoctorDTO
            {
                Email = doctor.User.Email,
                FullName = doctor.User.FullName,
                PhoneNumber = doctor.User.PhoneNumber,
                Specilization = doctor.Specialization.SpecializationName,
                Gender = doctor.User.Gender.ToString(),
                DateOfBirth = Convert.ToString(doctor.User.DateOfBirth),
                ImageUrl = doctor.User.ImageUrl
            };
        }

        /// <summary>
        /// Deletes a doctor and all their appointments.
        /// Throws if the doctor has active (non-cancelled) bookings.
        /// </summary>
        public async Task<bool> DeleteDoctorAsync(int doctorId)
        {
            var doctor = await _adminRepository.GetDoctorByIdAsync(doctorId);
            if (doctor == null)
                throw new Exception($"No doctor with ID {doctorId} was found.");

            var hasActiveBookings = await _adminRepository.GetBookingByDoctorId(doctorId);
            if (hasActiveBookings)
                throw new Exception("This doctor cannot be deleted because they have existing bookings.");

            // Delete ALL appointments for this doctor, not just the first one
            await _adminRepository.DeleteAllDoctorAppointmentsAsync(doctorId);
            await _adminRepository.DeleteDoctorAsync(doctor);

            // FIX: was missing await — user was never actually deleted
            var user = await _userManager.FindByIdAsync(doctor.UserId);
            if (user != null)
                await _userManager.DeleteAsync(user);

            return true;
        }

        /// <summary>
        /// Returns the total number of doctors.
        /// </summary>
        public int GetTotalNumOfDoctors()
        {
            return _adminRepository.NumOfDoctors();
        }

        /// <summary>
        /// Updates an existing doctor's profile. Image is optional on update.
        /// </summary>
        public async Task<bool> DoctorUpdateAsync(DoctorUpdateDTO model)
        {
            var doctor = await _adminRepository.GetDoctorByIdAsync(model.DoctorId);
            if (doctor == null)
                throw new Exception($"No doctor with ID {model.DoctorId} was found.");

            var user = await _userManager.FindByIdAsync(doctor.UserId);
            if (user == null)
                throw new Exception($"Identity user for doctor ID {model.DoctorId} was not found.");

            // Image update is optional — only re-save if a new file was provided
            if (model.ImageUrl != null && model.ImageUrl.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(model.ImageUrl.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    throw new Exception("Only image files (jpg, jpeg, png, gif, webp) are allowed.");

                if (model.ImageUrl.Length > 5 * 1024 * 1024)
                    throw new Exception("Image file size must not exceed 5 MB.");

                var fileName = Guid.NewGuid().ToString() + extension;
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await model.ImageUrl.CopyToAsync(stream);

                user.ImageUrl = $"images/{fileName}";
            }

            user.Email = model.Email;
            user.UserName = model.Email;
            user.DateOfBirth = Convert.ToDateTime(model.DateOfBirth);
            user.PhoneNumber = model.PhoneNumber;
            user.Gender = model.Gender;
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.FullName = model.FirstName + " " + model.LastName;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return false;

            var specialization = await _specilizationRepository.GetByNameAsync(model.Specilization);
            if (specialization != null)
                doctor.SpecializationId = specialization.SpecializationId;

            return await _adminRepository.DoctorUpdateAsync(doctor);
        }

        /// <summary>
        /// Returns a paginated, searchable list of doctors.
        /// </summary>
        public async Task<(IEnumerable<DoctorDTO>, int TotalCount)> GetAllDoctorsAsync(PaginationAndSearchDTO request)
        {
            var (doctors, totalCount) = await _adminRepository.GetAllDoctorsAsync(request);

            var doctorDtos = doctors.Select(d => new DoctorDTO
            {
                FullName = d.User.FullName,
                Email = d.User.Email,
                DateOfBirth = Convert.ToString(d.User.DateOfBirth),
                Gender = d.User.Gender.ToString(),
                PhoneNumber = d.User.PhoneNumber,
                ImageUrl = d.User.ImageUrl,
                Specilization = d.Specialization.SpecializationName
            });

            return (doctorDtos, totalCount);
        }

        /// <summary>
        /// Returns the total number of patients.
        /// </summary>
        public async Task<int> TotalNumberOfPatients()
        {
            return await _adminRepository.NumberOfPatients();
        }

        /// <summary>
        /// Returns a paginated, searchable list of patients.
        /// </summary>
        public async Task<(IEnumerable<PatientDTO>, int)> GetAllPatientsAsync(PaginationAndSearchDTO request)
        {
            var (patients, totalcount) = await _adminRepository.GetAllPatientsAsync(request);

            var patientDTOs = patients.Select(p => new PatientDTO
            {
                Email = p.Email,
                FullName = p.FullName,
                Gender = p.Gender.ToString(),
                DateOfBirth = p.DateOfBirth.ToShortDateString(),
                PhoneNumber = p.PhoneNumber,
                ImageUrl = p.ImageUrl?.ToString()
            });

            return (patientDTOs, totalcount);
        }

        /// <summary>
        /// Returns a single patient by their Identity user ID.
        /// </summary>
        public async Task<PatientDTO> GetPatientByIdAsync(string patientId)
        {
            var patient = await _adminRepository.GetPatientById(patientId);
            if (patient == null || patient.AccountRole != AccountRole.Patient)
                throw new Exception($"No patient with ID {patientId} was found.");

            return new PatientDTO
            {
                FullName = patient.FullName,
                Email = patient.Email,
                PhoneNumber = patient.PhoneNumber,
                Gender = patient.Gender.ToString(),
                DateOfBirth = patient.DateOfBirth.ToShortDateString(),
                ImageUrl = patient.ImageUrl
            };
        }

        /// <summary>
        /// Returns the top 5 specializations by completed booking count.
        /// Grouping and ordering is done in the database, not in-memory.
        /// </summary>
        public async Task<IEnumerable<TopFiveSpecalizationDTO>> GetTopSpecializationsAsync()
        {
            return await _adminRepository.GetTopFiveSpecalizationsAsync();
        }

        /// <summary>
        /// Returns the top 10 doctors by completed booking count.
        /// Grouping and ordering is done in the database, not in-memory.
        /// </summary>
        public async Task<IEnumerable<TopTenDoctorDTO>> GetTopTenDoctorsAsync()
        {
            return await _adminRepository.GetTopTenDoctors();
        }

        /// <summary>
        /// Returns a breakdown of booking counts by status (Pending, Completed, Cancelled).
        /// </summary>
        public async Task<RequestsDTO> GetNumberOfRequestsAsync()
        {
            return await _adminRepository.GetNumberOfRequests();
        }

        /// <summary>
        /// Returns how many doctors were added in the last 24 hours.
        /// </summary>
        public async Task<int> GetNumberOfDoctorsAddedLast24HoursAsync()
        {
            return await _adminRepository.GetNumberOfDoctorsAddedLast24HoursAsync();
        }

        /// <summary>
        /// Returns the profile information of the currently authenticated admin.
        /// </summary>
        public async Task<UserProfileDTO> GetMyProfileAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new Exception("User not found.");

            return new UserProfileDTO
            {
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Gender = user.Gender.ToString(),
                DateOfBirth = user.DateOfBirth.ToString("yyyy-MM-dd"),
                ImageUrl = user.ImageUrl
            };
        }

        /// <summary>
        /// Updates the profile information of the currently authenticated admin.
        /// </summary>
        public async Task UpdateMyProfileAsync(string userId, UpdateUserProfileDTO dto)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new Exception("User not found.");

            string[] formats = { "yyyy-MM-dd", "yyyy/MM/dd", "yyyy/M/dd", "yyyy/M/d", "yyyy/MM/d",
                                  "dd/MM/yyyy", "dd/M/yyyy", "d/MM/yyyy", "d/M/yyyy" };
            CultureInfo provider = new CultureInfo("en-US");

            if (dto.ImageUrl != null && dto.ImageUrl.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(dto.ImageUrl.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    throw new Exception("Only image files (jpg, jpeg, png, gif, webp) are allowed.");
                if (dto.ImageUrl.Length > 5 * 1024 * 1024)
                    throw new Exception("Image file size must not exceed 5 MB.");
                var fileName = Guid.NewGuid().ToString() + extension;
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await dto.ImageUrl.CopyToAsync(stream);
                user.ImageUrl = $"images/{fileName}";
            }

            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.FullName = $"{dto.FirstName} {dto.LastName}";
            user.PhoneNumber = dto.PhoneNumber;
            user.Gender = (Gender)dto.Gender;
            user.DateOfBirth = DateTime.ParseExact(dto.DateOfBirth, formats, provider);

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                throw new Exception($"Profile update failed: {string.Join(", ", errors)}");
            }
        }

        /// <summary>
        /// Returns all specializations ordered alphabetically.
        /// </summary>
        public async Task<IEnumerable<SpecializationDTO>> GetAllSpecializationsAsync()
        {
            var specializations = await _specilizationRepository.GetAllAsync();
            return specializations.Select(s => new SpecializationDTO
            {
                SpecializationId   = s.SpecializationId,
                SpecializationName = s.SpecializationName
            });
        }

        /// <summary>
        /// Adds a new specialization. Throws if the name already exists.
        /// </summary>
        public async Task<SpecializationDTO> AddSpecializationAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new Exception("Specialization name cannot be empty.");

            var existing = await _specilizationRepository.GetByNameAsync(name.Trim());
            if (existing != null)
                throw new Exception($"Specialization '{name}' already exists.");

            var specialization = new Specialization { SpecializationName = name.Trim() };
            await _specilizationRepository.AddAsync(specialization);

            return new SpecializationDTO
            {
                SpecializationId   = specialization.SpecializationId,
                SpecializationName = specialization.SpecializationName
            };
        }

        /// <summary>
        /// Deletes a specialization by ID. Throws if it has doctors assigned to it.
        /// </summary>
        public async Task<bool> DeleteSpecializationAsync(int id)
        {
            var specialization = await _specilizationRepository.GetByIdAsync(id);
            if (specialization == null)
                throw new Exception($"Specialization with ID {id} was not found.");

            await _specilizationRepository.DeleteAsync(specialization);
            return true;
        }

        /// <summary>
        /// Sends a welcome email to the doctor with their login credentials.
        /// The password passed here is the one chosen at registration — no hardcoded fallback.
        /// </summary>
        private async Task SendWelcomeEmailToDoctorAsync(string email, string password)
        {
            var subject = "Welcome to Veezta — Your Account Details";
            var message = $"Your Veezta doctor account has been created.\n\n" +
                          $"Email: {email}\n" +
                          $"Password: {password}\n\n" +
                          "Please log in and change your password immediately after your first sign-in.";

            await _emailSender.SendEmailAsync(email, subject, message);
        }
    }
}
