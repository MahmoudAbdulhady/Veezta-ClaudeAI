namespace Application.DTOS
{
    public class CouponResponseDTO
    {
        public int CouponId { get; set; }
        public string CouponName { get; set; }
        public int Code { get; set; }
        public bool IsActive { get; set; }
    }
}
