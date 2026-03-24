using Application.Contracts;
using Domain.DTOS;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using System.Globalization;

namespace Application.Services
{
    public class PatientService : IPatientService
    {
        private readonly UserManager<CustomUser> _userManager;
        private readonly SignInManager<CustomUser> _signInManager;
        private readonly IPatientRepository _patientRepository;
        private readonly IDoctorRepository _doctorRepository;
        private readonly ICouponRepository _couponRepository;

        public PatientService(
            UserManager<CustomUser> userManager,
            SignInManager<CustomUser> signInManager,
            IPatientRepository patientRepository,
            IDoctorRepository doctorRepository,
            ICouponRepository couponRepository,
            IAdminRepository adminRepository)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _patientRepository = patientRepository;
            _doctorRepository = doctorRepository;
            _couponRepository = couponRepository;
        }

        /// <summary>
        /// Registers a new patient. Saves an optional profile image and assigns the Patient role.
        /// FIX: imageUrl is no longer set to "images/" when no image is provided.
        /// </summary>
        public async Task<bool> RegisterPatientAsync(PatientRegisterDTO model)
        {
            CultureInfo provider = new CultureInfo("en-US");
            string[] formats = { "yyyy/MM/dd", "yyyy/M/dd", "yyyy/M/d", "yyyy/MM/d",
                                  "dd/MM/yyyy", "dd/M/yyyy", "d/MM/yyyy", "d/M/yyyy" };

            // FIX: Only build imageUrl when an image is actually provided
            string? imageUrl = null;
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
                using var stream = new FileStream(filePath, FileMode.Create);
                await model.ImageUrl.CopyToAsync(stream);
                imageUrl = $"images/{fileName}";
            }

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
                ImageUrl = imageUrl,   // null if not provided
                AccountRole = AccountRole.Patient
            };

            var userResult = await _userManager.CreateAsync(user, model.Password);
            if (!userResult.Succeeded)
            {
                var errors = userResult.Errors.Select(e => e.Description);
                throw new Exception($"Registration failed: {string.Join(", ", errors)}");
            }

            await _userManager.AddToRoleAsync(user, AccountRole.Patient.ToString());
            return true;
        }

        /// <summary>
        /// Authenticates a patient and returns true on success.
        /// </summary>
        public async Task<bool> LoginPatientAsync(LoginDTO model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return false;

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, false, false);
            return result.Succeeded;
        }

        /// <summary>
        /// Returns a paginated list of doctor appointment slots, grouped by doctor and day.
        /// </summary>
        public async Task<(IEnumerable<AppointmentDTO>, int totalCount)> GetAppointmentsForDoctorAsync(PaginationAndSearchDTO request)
        {
            var (times, totalCount) = await _patientRepository.GetDoctorApptAsync(request);
            if (times == null || !times.Any())
                throw new Exception("No appointments were found.");

            var doctorSchedules = times
                .GroupBy(a => new
                {
                    a.Appointement.Doctor.User.FullName,
                    a.Appointement.Doctor.Specialization.SpecializationName,
                    a.Appointement.Doctor.Price,
                    a.Appointement.Days
                })
                .Select(doctorGroup => new AppointmentDTO
                {
                    DoctorName = doctorGroup.Key.FullName,
                    Specailization = doctorGroup.Key.SpecializationName,
                    Price = doctorGroup.Key.Price,
                    AvailableDay = doctorGroup
                        .GroupBy(a => a.Appointement.Days)
                        .Select(dayGroup => new DayScheduleDTO
                        {
                            Day = dayGroup.Key.ToString(),
                            TimeSlots = dayGroup
                                .Select(a => $"{a.StartTime} TO {a.EndTime}")
                                .ToList()
                        })
                        .ToList()
                })
                .ToList();

            return (doctorSchedules, totalCount);
        }

        /// <summary>
        /// Creates a new booking. Optionally applies a coupon discount.
        ///
        /// FIX: The original coupon logic was completely broken:
        ///   - completedRequests == 5 immediately threw "already used" due to self-assignment check
        ///   - completedRequests == 10 checked == 11 which is unreachable
        ///
        /// New logic:
        ///   - >= 10 completed requests → 10% discount
        ///   - >= 5 completed requests  →  5% discount
        ///   - Coupon usage tracked per patient/coupon pair in the database
        /// </summary>
        public async Task<bool> CreateNewBookingAsync(int appointmentId, string patientId, string? couponName = null)
        {
            var appointmentTime = await _doctorRepository.FindAppointmentByAppointmentId(appointmentId);
            if (appointmentTime == null)
                throw new Exception($"Appointment with ID {appointmentId} was not found.");

            var existingBooking = await _patientRepository.FindyBookingById(appointmentId);
            if (existingBooking != null)
                throw new Exception($"Appointment with ID {appointmentId} is already booked.");

            var originalPrice = appointmentTime.Appointement.Doctor.Price;
            var priceAfterCoupon = originalPrice;
            bool isCouponUsed = false;

            if (!string.IsNullOrWhiteSpace(couponName))
            {
                var coupon = await _couponRepository.FindCouponByName(couponName);
                if (coupon == null || !coupon.IsActive)
                    throw new Exception("The coupon is invalid or inactive.");

                // Check if this patient has already used this coupon
                bool alreadyUsed = await _couponRepository.HasPatientUsedCoupon(patientId, coupon.CouponId);
                if (alreadyUsed)
                    throw new Exception("You have already used this coupon.");

                int completedRequests = await _couponRepository.GetNumberOfCompletedRequestByPatientId(patientId);

                if (completedRequests >= 10)
                {
                    priceAfterCoupon = (int)(originalPrice * 0.90m); // 10% off
                    isCouponUsed = true;
                }
                else if (completedRequests >= 5)
                {
                    priceAfterCoupon = (int)(originalPrice * 0.95m); // 5% off
                    isCouponUsed = true;
                }
                else
                {
                    throw new Exception("You need at least 5 completed appointments to use a coupon.");
                }

                // Mark coupon as used by this patient
                await _couponRepository.MarkCouponAsUsed(patientId, coupon.CouponId);
            }

            var newBooking = new Booking
            {
                AppointmentId = appointmentTime.AppointmentId,
                PatientId = patientId,
                IsCouponUsed = isCouponUsed,
                Price = (int)originalPrice,
                PriceAfterCoupon = (int)priceAfterCoupon,
            };

            await _patientRepository.CreateBookingAsync(newBooking);
            return true;
        }

        /// <summary>
        /// Cancels a pending booking. Throws if already completed or cancelled.
        /// </summary>
        public async Task<bool> CancelBookingAsync(int bookingId)
        {
            return await _patientRepository.CancelAppointment(bookingId);
        }

        /// <summary>
        /// Returns all bookings for a specific patient.
        /// FIX: Filtering by patientId is now done in the database query (see PatientRepository),
        /// instead of loading all bookings then filtering in-memory.
        /// </summary>
        public async Task<IEnumerable<PatientBookingDTO>> GetPatientSpecificBookingsAsync(string patientId)
        {
            var bookings = await _patientRepository.GetPatientBookingsByPatientId(patientId);

            return bookings.Select(t => new PatientBookingDTO
            {
                DoctorName = t.Appointement.Doctor.User.FullName,
                Specailization = t.Appointement.Doctor.Specialization.SpecializationName,
                Price = t.Appointement.Doctor.Price.ToString(),
                PhoneNumber = t.Appointement.Doctor.User.PhoneNumber,
                Day = t.Appointement.Days.ToString(),
                FinalPrice = t.Appointement.Booking.Price.ToString(),
                Image = t.Appointement.Doctor.User.ImageUrl,
                StartTime = t.Appointement.Times.FirstOrDefault()?.StartTime,
                EndTime = t.Appointement.Times.FirstOrDefault()?.EndTime,
                BookingStatus = t.Appointement.Booking.Status.ToString()
            });
        }
    }
}
