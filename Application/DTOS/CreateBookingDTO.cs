namespace Application.DTOS
{
    public class CreateBookingDTO
    {
        // FIX: renamed from appointmentId (camelCase) to AppointmentId (PascalCase)
        public int AppointmentId { get; set; }

        // FIX: PatientId removed — it is now derived from the JWT claim in the controller
        // so a patient cannot book on behalf of someone else.

        public string? CouponName { get; set; }
    }
}
