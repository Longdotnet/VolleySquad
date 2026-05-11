// ============================================================
// DOMAIN EXCEPTION - Custom Exception cho Domain Layer
// ============================================================
// Tại sao cần Custom Exception riêng?
//
// Vấn đề với cách dùng Exception thông thường:
//   throw new Exception("Sân đã đầy slot!");
//   → Controller không phân biệt được đây là lỗi DOMAIN (business rule violated)
//     hay lỗi SYSTEM (null reference, DB connection failed...).
//   → Dẫn đến việc catch Exception quá rộng hoặc trả về 500 cho lỗi business.
//
// Giải pháp: Domain Exception phân tầng rõ ràng:
//   DomainException   → Lỗi business rule (400 Bad Request hoặc 422 Unprocessable)
//   Exception         → Lỗi hệ thống không mong đợi (500 Internal Server Error)
//
// Trong Controller:
//   try { ... }
//   catch (DomainException ex) { return BadRequest(ex.Message); }     // 400
//   catch (Exception)          { return StatusCode(500, "..."); }       // 500
//
// INTERVIEW NOTE: Đây là phần "Exception Handling Strategy" trong Clean Architecture.
// Nâng cao hơn có thể dùng Result<T> pattern (không throw exception, return object):
//   public Result<Match> TryRegisterMember(...) { return Result.Failure("..."); }
// → Functional approach, tránh expensive stack unwind của exception.
namespace VolleySquad.Api.Domain.Exceptions
{
    /// <summary>
    /// Exception đại diện cho vi phạm Business Rule trong Domain Layer.
    /// Controller nên catch loại này và trả về HTTP 400/422, KHÔNG phải 500.
    /// </summary>
    public class DomainException : Exception
    {
        // ErrorCode tùy chọn: Dùng để frontend hiển thị message đa ngôn ngữ
        // hoặc để log/monitor phân loại lỗi theo category.
        public string? ErrorCode { get; }

        public DomainException(string message) : base(message) { }

        public DomainException(string message, string errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }

        // InnerException: Bọc exception gốc vào DomainException khi muốn giữ stack trace.
        // Dùng khi catch exception từ layer dưới và re-throw lên dưới dạng domain error.
        public DomainException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
