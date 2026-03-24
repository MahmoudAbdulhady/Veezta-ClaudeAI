using Domain.Entities;

namespace Domain.Interfaces
{
    public interface ICouponRepository
    {
        Task<Coupon> CreateCouponAsync(Coupon coupon);
        Task<Coupon> FindCouponById(int couponId);
        Task<Coupon> DeactivateCoupon(Coupon coupon);
        Task<Coupon> DeleteCoupon(Coupon coupon);
        Task<Coupon> UpdateCoupon(Coupon coupon);
        Task<Coupon> FindCouponByName(string name);
        Task<int> GetNumberOfCompletedRequestByPatientId(string patientId);

        // FIX: new methods to support corrected coupon validation logic
        Task<bool> HasPatientUsedCoupon(string patientId, int couponId);
        Task MarkCouponAsUsed(string patientId, int couponId);
    }
}
