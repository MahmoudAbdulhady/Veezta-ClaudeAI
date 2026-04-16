using Application.Contracts;
using Application.Services;
using Domain.DTOS;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace VeeztaUnitTest.MockData
{
    /// <summary>
    /// Unit tests for PatientService.GetAppointmentsForDoctorAsync.
    ///
    /// Bug reproduced: on initial page load (no search term), the repository returns ALL Time
    /// rows in-memory. If any Doctor has a null Specialization navigation property (orphaned FK),
    /// the GroupBy crashes with NullReferenceException. A non-empty search term accidentally
    /// avoided this because EF Core SQL WHERE translates to INNER JOINs that filter null rows.
    ///
    /// Fixes applied:
    ///   1. Repository: added .Where(t => t.Appointement.Doctor.Specialization != null) so the
    ///      DB query always behaves like the search path.
    ///   2. Service: pre-filters null-Specialization slots in-memory as a secondary defence,
    ///      and returns an empty collection instead of throwing when no slots exist.
    /// </summary>
    public class PatientServiceTests
    {
        private readonly Mock<IPatientRepository> _patientRepoMock;
        private readonly PatientService _sut;

        public PatientServiceTests()
        {
            _patientRepoMock = new Mock<IPatientRepository>();

            // UserManager requires an IUserStore; remaining constructor args can be null
            // because they are not exercised by GetAppointmentsForDoctorAsync.
            var userStore = new Mock<IUserStore<CustomUser>>();
            var userManager = new Mock<UserManager<CustomUser>>(
                userStore.Object, null, null, null, null, null, null, null, null);

            var contextAccessor = new Mock<IHttpContextAccessor>();
            var claimsFactory = new Mock<IUserClaimsPrincipalFactory<CustomUser>>();
            var signInManager = new Mock<SignInManager<CustomUser>>(
                userManager.Object, contextAccessor.Object, claimsFactory.Object, null, null, null, null);

            _sut = new PatientService(
                userManager.Object,
                signInManager.Object,
                _patientRepoMock.Object,
                Mock.Of<IDoctorRepository>(),
                Mock.Of<ICouponRepository>(),
                Mock.Of<IAdminRepository>()
            );
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        private static Time MakeTime(string doctorName, string specialization, WeekDays day,
            string start = "09:00", string end = "10:00", int price = 100)
            => new Time
            {
                StartTime = start,
                EndTime = end,
                Appointement = new Appointement
                {
                    Days = day,
                    Doctor = new Doctor
                    {
                        Price = price,
                        User = new CustomUser { FullName = doctorName },
                        Specialization = new Specialization { SpecializationName = specialization }
                    }
                }
            };

        private static Time MakeTimeWithNullSpecialization(string doctorName, WeekDays day)
            => new Time
            {
                StartTime = "11:00",
                EndTime = "12:00",
                Appointement = new Appointement
                {
                    Days = day,
                    Doctor = new Doctor
                    {
                        Price = 80,
                        User = new CustomUser { FullName = doctorName },
                        Specialization = null   // orphaned FK — the buggy case
                    }
                }
            };

        private void SetupRepo(IEnumerable<Time> times, int totalCount)
            => _patientRepoMock
                .Setup(r => r.GetDoctorApptAsync(It.IsAny<PaginationAndSearchDTO>()))
                .ReturnsAsync((times, totalCount));

        // ── tests ────────────────────────────────────────────────────────────────

        /// <summary>
        /// On initial page load with an empty database (or all slots booked/removed),
        /// the service must return an empty collection — not throw an exception.
        /// Previously the service threw "No appointments were found." here.
        /// </summary>
        [Fact]
        public async Task GetAppointmentsForDoctorAsync_NoSlots_ReturnsEmptyCollection()
        {
            SetupRepo(Enumerable.Empty<Time>(), 0);

            var (results, count) = await _sut.GetAppointmentsForDoctorAsync(
                new PaginationAndSearchDTO { PageNumber = 1, PageSize = 10 });

            Assert.Empty(results);
            Assert.Equal(0, count);
        }

        /// <summary>
        /// When the repository returns valid slots (all navigation properties populated),
        /// the service must correctly group them by doctor and return the schedule.
        /// </summary>
        [Fact]
        public async Task GetAppointmentsForDoctorAsync_ValidSlots_ReturnsGroupedByDoctor()
        {
            var times = new List<Time>
            {
                MakeTime("Dr. Ahmed Hassan", "Cardiology", WeekDays.Monday, "09:00", "10:00"),
                MakeTime("Dr. Ahmed Hassan", "Cardiology", WeekDays.Monday, "10:00", "11:00"),
                MakeTime("Dr. Sara Ali",     "Dermatology", WeekDays.Wednesday, "14:00", "15:00"),
            };
            SetupRepo(times, times.Count);

            var (results, count) = await _sut.GetAppointmentsForDoctorAsync(
                new PaginationAndSearchDTO { PageNumber = 1, PageSize = 10 });

            Assert.Equal(2, results.Count());

            var ahmed = results.First(r => r.DoctorName == "Dr. Ahmed Hassan");
            Assert.Equal("Cardiology", ahmed.Specailization);
            Assert.Single(ahmed.AvailableDay);                    // Monday grouped once
            Assert.Equal(2, ahmed.AvailableDay[0].TimeSlots.Count); // two slots on Monday

            var sara = results.First(r => r.DoctorName == "Dr. Sara Ali");
            Assert.Equal("Dermatology", sara.Specailization);
        }

        /// <summary>
        /// THE BUG CASE: if the repository leaks a slot whose Doctor.Specialization is null
        /// (orphaned FK), the service must skip that slot instead of crashing.
        /// Previously this caused a NullReferenceException on unfiltered (no search term) loads.
        /// </summary>
        [Fact]
        public async Task GetAppointmentsForDoctorAsync_SlotWithNullSpecialization_IsSkipped()
        {
            var times = new List<Time>
            {
                MakeTime("Dr. Ahmed Hassan", "Cardiology", WeekDays.Monday),   // valid
                MakeTimeWithNullSpecialization("Dr. Ghost", WeekDays.Tuesday), // broken — null spec
            };
            SetupRepo(times, times.Count);

            var exception = await Record.ExceptionAsync(() =>
                _sut.GetAppointmentsForDoctorAsync(
                    new PaginationAndSearchDTO { PageNumber = 1, PageSize = 10 }));

            Assert.Null(exception); // must NOT throw

            var (results, _) = await _sut.GetAppointmentsForDoctorAsync(
                new PaginationAndSearchDTO { PageNumber = 1, PageSize = 10 });

            Assert.Single(results);
            Assert.Equal("Dr. Ahmed Hassan", results.First().DoctorName);
        }

        /// <summary>
        /// The total count returned by the repository is passed through unchanged,
        /// even when some slots are filtered out by the null-specialization guard.
        /// </summary>
        [Fact]
        public async Task GetAppointmentsForDoctorAsync_PassesThroughTotalCount()
        {
            var times = new List<Time>
            {
                MakeTime("Dr. Ahmed Hassan", "Cardiology", WeekDays.Monday),
            };
            SetupRepo(times, 42); // backend reports 42 total across all pages

            var (_, count) = await _sut.GetAppointmentsForDoctorAsync(
                new PaginationAndSearchDTO { PageNumber = 1, PageSize = 10 });

            Assert.Equal(42, count);
        }
    }
}
