using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastrucutre.Repositories
{
    public class CouponRepository : ICouponRepository
    {
        private readonly VeeztaDbContext _veeztaDbContext;

        public CouponRepository(VeeztaDbContext veeztaDbContext)
        {
            _veeztaDbContext = veeztaDbContext;
        }

        public async Task<Coupon> CreateCouponAsync(Coupon coupon)
        {
            await _veeztaDbContext.Coupons.AddAsync(coupon);
            await _veeztaDbContext.SaveChangesAsync();
            return coupon;
        }

        public async Task<Coupon> DeactivateCoupon(Coupon coupon)
        {
            coupon.IsActive = false;
            await _veeztaDbContext.SaveChangesAsync();
            return coupon;
        }

        public async Task<Coupon> DeleteCoupon(Coupon coupon)
        {
            _veeztaDbContext.Coupons.Remove(coupon);
            await _veeztaDbContext.SaveChangesAsync();
            return coupon;
        }

        public async Task<Coupon> FindCouponById(int couponId)
        {
            return await _veeztaDbContext.Coupons
                .FirstOrDefaultAsync(c => c.CouponId == couponId);
        }

        public async Task<Coupon> FindCouponByName(string name)
        {
            return await _veeztaDbContext.Coupons
                .FirstOrDefaultAsync(c => c.CouponName == name && c.IsActive);
        }

        public async Task<int> GetNumberOfCompletedRequestByPatientId(string patientId)
        {
            return await _veeztaDbContext.Bookings
                .CountAsync(b => b.Status == BookingStatus.Completed && b.PatientId == patientId);
        }

        /// <summary>
        /// FIX: Checks whether a patient has already used a specific coupon.
        /// Uses the PatientId foreign key on the Coupon entity to track usage.
        /// </summary>
        public async Task<bool> HasPatientUsedCoupon(string patientId, int couponId)
        {
            return await _veeztaDbContext.Coupons
                .AnyAsync(c => c.CouponId == couponId && c.PatientId == patientId);
        }

        /// <summary>
        /// FIX: Records that a patient has used a coupon by setting PatientId on the coupon record.
        /// </summary>
        public async Task MarkCouponAsUsed(string patientId, int couponId)
        {
            var coupon = await _veeztaDbContext.Coupons
                .FirstOrDefaultAsync(c => c.CouponId == couponId);
            if (coupon != null)
            {
                coupon.PatientId = patientId;
                await _veeztaDbContext.SaveChangesAsync();
            }
        }

        public async Task<Coupon> UpdateCoupon(Coupon coupon)
        {
            _veeztaDbContext.Coupons.Update(coupon);
            await _veeztaDbContext.SaveChangesAsync();
            return coupon;
        }
    }
}
