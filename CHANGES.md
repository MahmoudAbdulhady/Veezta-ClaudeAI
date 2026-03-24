# Veezta — Code Review Fixes

## 🔒 Security Fixes

### 1. JWT Authentication added
- All endpoints are now protected with `[Authorize(Roles = "...")]`
- Admin endpoints → `[Authorize(Roles = "Admin")]`
- Doctor endpoints → `[Authorize(Roles = "Doctor")]`
- Patient endpoints → `[Authorize(Roles = "Patient")]`
- Register/Login endpoints are `[AllowAnonymous]`
- New `TokenService.cs` generates signed JWT tokens on login
- Added `JwtSettings` section to `appsettings.json` — **change `SecretKey` before deploying**

### 2. Login endpoints now return JWT tokens
- `POST /api/Doctor/Login` → returns `{ Token, Message }`
- `POST /api/Patient/Login` → returns `{ Token, Message }`
- Pass the token as `Authorization: Bearer <token>` on subsequent requests

### 3. PatientId no longer trusted from client
- `BookAppointment` and `GetMyBookings` now derive `patientId` from the JWT claim
- A patient can no longer book or view appointments on behalf of another patient
- `CreateBookingDTO.PatientId` field removed

### 4. Hardcoded password removed
- `AdminServices.SendEmailToDoctorAsync` was emailing `"Test@2023"` to every doctor
- Now emails the actual password chosen at registration time

### 5. Image upload validation added
- Allowed extensions: `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`
- Maximum file size: 5 MB
- Applied in both `AddDocotorAsync` and `RegisterPatientAsync`

### 6. Global exception handler added (`Program.cs`)
- Unhandled exceptions no longer leak stack traces or internal messages to the client
- Returns a generic `{ StatusCode, Message }` JSON response
- Internal error is logged via Serilog

### 7. `GetPatientById` role check fixed (`AdminRepository`)
- Previously returned the user even when the Patient role check failed
- Now returns `null` if the user does not have the Patient role

## 🐛 Bug Fixes

### 8. `DeleteDoctorAsync` — missing `await` fixed
- `_userManager.DeleteAsync(user)` was fire-and-forget; user records were never deleted
- Now correctly `await`ed

### 9. Doctor deletion now removes ALL appointments
- `GetAppointmentByDoctorId` used `FirstOrDefaultAsync` — only the first appointment was deleted
- New `DeleteAllDoctorAppointmentsAsync(doctorId)` deletes every appointment for the doctor
- Prevents orphaned appointment rows

### 10. Coupon logic completely rewritten (`PatientService`)
- Original logic: assigned `coupon.PatientId = patientId` then immediately checked `coupon.PatientId == patientId` → always threw "already used"
- The `completedRequests == 10` branch checked `== 11` which is unreachable
- New logic:
  - `>= 10` completed bookings → 10% discount
  - `>= 5` completed bookings → 5% discount
  - Coupon usage tracked via `HasPatientUsedCoupon` / `MarkCouponAsUsed` in the database

### 11. `imageUrl` broken path fixed (`PatientService`)
- When no image was uploaded, `imageUrl` was set to `"images/"` (a broken path)
- Now `null` when no image is provided

### 12. `CancelAppointment` error message corrected
- Said "can't be cancelled since it's a Completed or **Pending** Appointment" — Pending was wrong
- Now says "cannot be cancelled because it is already {status}"

### 13. Unreachable `return true` after `throw` removed (`DoctorService.DeleteTimeAppointmentAsync`)

## ⚡ Performance Fixes

### 14. `GetTopFiveSpecializations` — now done in the database
- Was: load all completed bookings → group/sort/Take(5) in memory
- Now: `GroupBy`, `OrderByDescending`, `Take(5)` pushed to SQL

### 15. `GetTopTenDoctors` — now done in the database
- Same pattern as above; grouping now happens in SQL

### 16. `GetNumberOfRequests` — now uses `CountAsync` per status
- Was: load all booking statuses into a `List<BookingStatus>` then count in memory
- Now: three `CountAsync` calls directly on the database

### 17. `GetPatientSpecificBookings` — filter now in the database
- Was: `GetPatientBookings()` loaded ALL bookings then filtered client-side
- New `GetPatientBookingsByPatientId(patientId)` adds a `WHERE` clause to the query

## 🎨 Code Style Fixes

### 18. HTTP verb corrections
- `GetAllDoctors` POST → GET (with `[FromQuery]`)
- `GetAllPatients` POST → GET (with `[FromQuery]`)
- `GetDoctorAppointments` POST → GET (with `[FromQuery]`)
- `DeleteCoupon` POST → DELETE
- `UpdateCoupon` POST → PUT

### 19. `PatientController` base class fixed
- Was `Controller` (MVC/Razor) → now `ControllerBase` (API only)

### 20. `DoctorUpdateDTO.doctorId` → `DoctorId` (PascalCase)

### 21. `CreateBookingDTO.appointmentId` → `AppointmentId` (PascalCase)

### 22. `CouponController` response body fixed
- Was `Ok($"... {model} ...")` → now `Ok($"... {model.CouponId} ...")`

### 23. Log file path changed from hardcoded absolute path to relative `logs/` folder

### 24. Dead code removed from `Program.cs`
- Removed commented-out data seeder block
- Removed unused `scope` variable

### 25. Duplicate `AddBookingAsync` method removed from `PatientRepository`

## ⚙️ Setup Notes

1. Update `appsettings.json`:
   - Set `ConnectionStrings:MyConnectionString` to your SQL Server
   - Set `JwtSettings:SecretKey` to a long random string (32+ chars)
   - Set `SmtpSettings` with your email credentials

2. Run migrations:
   ```
   add-migration <name>
   update-database
   ```

3. Seed the Admin role and user (use `DataSeeder` — uncomment in `Program.cs` for first run)

4. Use Swagger UI to test: Login first, copy the token, click "Authorize", paste `Bearer <token>`
