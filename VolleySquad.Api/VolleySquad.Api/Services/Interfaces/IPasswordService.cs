// ============================================================
// IPASSWORD SERVICE - Abstraction cho password hashing/verification
// ============================================================
// Tại sao không hash trực tiếp trong Controller?
//   1. Single Responsibility: Controller chỉ nên xử lý HTTP, không giữ crypto logic.
//   2. Testability: Có thể mock VerifyPassword() trong unit test.
//   3. Maintainability: Muốn đổi PBKDF2 -> Argon2/bcrypt thì sửa 1 chỗ.
namespace VolleySquad.Api.Services.Interfaces
{
    public interface IPasswordService
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string storedHash);
    }
}