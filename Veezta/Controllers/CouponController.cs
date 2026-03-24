using Application.Contracts;
using Application.DTOS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Veezta.Controllers
{
    [Route("api/Settings")]
    [ApiController]
    [Authorize(Roles = "Admin")]   // FIX: coupon management is admin-only
    public class CouponController : ControllerBase
    {
        private readonly ICouponService _couponService;

        public CouponController(ICouponService couponService)
        {
            _couponService = couponService;
        }

        /// <summary>
        /// Creates a new discount coupon.
        /// </summary>
        [HttpPost("CreateCoupon")]
        public async Task<IActionResult> CreateCoupon([FromForm] CreateCouponDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _couponService.CreateCouponAsync(model);
            return Ok(new
            {
                CouponCode = model.CouponCode,
                CouponName = model.CouponName,
                IsActive = model.IsActive
            });
        }

        /// <summary>
        /// Deactivates a coupon so it can no longer be redeemed.
        /// </summary>
        [HttpPost("DeactivateCoupon/{couponId:int}")]
        public async Task<IActionResult> DeactivateCoupon(int couponId)
        {
            await _couponService.DeactivateCouponAsync(couponId);
            return Ok($"Coupon ID {couponId} has been deactivated.");
        }

        /// <summary>
        /// Permanently deletes a coupon.
        /// </summary>
        [HttpDelete("DeleteCoupon/{couponId:int}")]
        // FIX: changed from POST to DELETE — deletion should use the DELETE verb
        public async Task<IActionResult> DeleteCoupon(int couponId)
        {
            await _couponService.DeleteCouponAsync(couponId);
            return Ok($"Coupon ID {couponId} was deleted successfully.");
        }

        /// <summary>
        /// Updates an existing coupon's details. Only inactive coupons can be updated.
        /// FIX: Response body now shows coupon ID, not the model's ToString().
        /// </summary>
        [HttpPut("UpdateCoupon")]
        // FIX: changed from POST to PUT — updates should use PUT
        public async Task<IActionResult> UpdateCoupon([FromForm] CouponUpdateDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _couponService.UpdateCouponAsync(model);
            return Ok($"Coupon ID {model.CouponId} was updated successfully.");
        }
    }
}
