namespace Application.DTOS
{
    public class DoctorScheduleSlotDTO
    {
        public int AppointmentId { get; set; }
        public string Day { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public bool IsBooked { get; set; }
    }
}
