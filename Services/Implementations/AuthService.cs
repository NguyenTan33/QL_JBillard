using Microsoft.EntityFrameworkCore;
using QL_JBillard.Data;
using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;
using QL_JBillard.Helpers;
using QL_JBillard.Services.Interfaces;

namespace QL_JBillard.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IAuditService _auditService;

    public User? CurrentUser { get; private set; }
    public event Action? OnAuthStateChanged;

    public AuthService(IDbContextFactory<AppDbContext> contextFactory, IAuditService _audit)
    {
        _contextFactory = contextFactory;
        _auditService = _audit;
    }

    public bool Login(string username, string password)
    {
        using var context = _contextFactory.CreateDbContext();
        var user = context.Users.FirstOrDefault(u => u.Username.ToLower() == username.Trim().ToLower() && u.IsActive);

        if (user == null)
        {
            _auditService.Log(null, "LOGIN_FAILED", "User", null, new { Username = username }, "Tên tài khoản không tồn tại hoặc bị khóa");
            return false;
        }

        bool isValid = SecurityHelper.VerifyPassword(password, user.PasswordHash, user.Salt);
        if (!isValid)
        {
            _auditService.Log(user.UserId, "LOGIN_FAILED", "User", user.UserId.ToString(), null, "Mật khẩu không chính xác");
            return false;
        }

        CurrentUser = user;
        _auditService.Log(user.UserId, "LOGIN_SUCCESS", "User", user.UserId.ToString(), null, $"Đăng nhập thành công với vai trò: {user.Role}");
        OnAuthStateChanged?.Invoke();
        return true;
    }

    public void Logout()
    {
        if (CurrentUser != null)
        {
            _auditService.Log(CurrentUser.UserId, "LOGOUT", "User", CurrentUser.UserId.ToString(), null, "Đăng xuất khỏi hệ thống");
        }
        CurrentUser = null;
        OnAuthStateChanged?.Invoke();
    }

    public bool IsInRole(params UserRole[] roles)
    {
        if (CurrentUser == null) return false;
        return roles.Contains(CurrentUser.Role);
    }

    public List<User> GetAllUsers()
    {
        using var context = _contextFactory.CreateDbContext();
        return context.Users.OrderBy(u => u.Role).ThenBy(u => u.FullName).ToList();
    }

    public bool CreateUser(string username, string password, string fullName, string? phone, UserRole role)
    {
        using var context = _contextFactory.CreateDbContext();
        if (context.Users.Any(u => u.Username.ToLower() == username.Trim().ToLower()))
        {
            return false;
        }

        string salt = SecurityHelper.GenerateSalt();
        string hash = SecurityHelper.HashPassword(password, salt);

        var newUser = new User
        {
            Username = username.Trim(),
            PasswordHash = hash,
            Salt = salt,
            FullName = fullName.Trim(),
            PhoneNumber = phone?.Trim(),
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.Now
        };

        context.Users.Add(newUser);
        context.SaveChanges();

        _auditService.Log(CurrentUser?.UserId, "CREATE_USER", "User", newUser.UserId.ToString(), null, new { newUser.Username, newUser.Role, newUser.FullName });
        return true;
    }

    public User? GetUserById(int userId)
    {
        using var context = _contextFactory.CreateDbContext();
        return context.Users.Find(userId);
    }

    public bool UpdateUser(int userId, string fullName, string? phone, UserRole role, bool isActive, string? newPassword = null)
    {
        using var context = _contextFactory.CreateDbContext();
        var user = context.Users.Find(userId);
        if (user == null) return false;

        user.FullName = fullName.Trim();
        user.PhoneNumber = phone?.Trim();
        user.Role = role;
        user.IsActive = isActive;

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            string newSalt = SecurityHelper.GenerateSalt();
            user.Salt = newSalt;
            user.PasswordHash = SecurityHelper.HashPassword(newPassword.Trim(), newSalt);
        }

        context.SaveChanges();
        _auditService.Log(CurrentUser?.UserId, "UPDATE_USER", "User", user.UserId.ToString(), null, new { user.Username, user.FullName, user.Role, user.IsActive });
        return true;
    }

    public bool ToggleUserStatus(int userId)
    {
        using var context = _contextFactory.CreateDbContext();
        var user = context.Users.Find(userId);
        if (user == null) return false;

        // Không cho khóa chính tài khoản admin đang đăng nhập
        if (user.UserId == CurrentUser?.UserId) return false;

        user.IsActive = !user.IsActive;
        context.SaveChanges();
        _auditService.Log(CurrentUser?.UserId, "TOGGLE_USER_STATUS", "User", user.UserId.ToString(), null, new { user.Username, user.IsActive });
        return true;
    }

    public bool DeleteUser(int userId)
    {
        using var context = _contextFactory.CreateDbContext();
        var user = context.Users.Find(userId);
        if (user == null) return false;

        // Không cho xóa chính mình
        if (user.UserId == CurrentUser?.UserId) return false;

        // Không cho xóa tài khoản admin gốc
        if (user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase)) return false;

        // Kiểm tra nếu đã có ca hoặc hóa đơn thì chuyển sang khóa thay vì xóa vật lý để giữ toàn vẹn dữ liệu
        bool hasHistory = context.Shifts.Any(s => s.UserId == userId) || context.Invoices.Any(i => i.CashierUserId == userId);
        if (hasHistory)
        {
            user.IsActive = false;
            context.SaveChanges();
            _auditService.Log(CurrentUser?.UserId, "DEACTIVATE_USER", "User", user.UserId.ToString(), null, "Tài khoản có lịch sử giao dịch nên được khóa bảo mật thay vì xóa");
            return true;
        }

        context.Users.Remove(user);
        context.SaveChanges();
        _auditService.Log(CurrentUser?.UserId, "DELETE_USER", "User", userId.ToString(), null, $"Đã xóa tài khoản {user.Username}");
        return true;
    }

    public bool ChangePassword(int userId, string oldPassword, string newPassword)
    {
        using var context = _contextFactory.CreateDbContext();
        var user = context.Users.Find(userId);
        if (user == null) return false;

        // Nếu người đổi không phải Admin và cũng không phải chính mình thì chặn
        if (CurrentUser?.Role != UserRole.Admin && CurrentUser?.UserId != userId)
        {
            return false;
        }

        // Nếu chính mình đổi thì bắt buộc phải kiểm tra mật khẩu cũ đúng
        if (CurrentUser?.UserId == userId)
        {
            if (!SecurityHelper.VerifyPassword(oldPassword, user.PasswordHash, user.Salt))
            {
                return false;
            }
        }

        string newSalt = SecurityHelper.GenerateSalt();
        user.Salt = newSalt;
        user.PasswordHash = SecurityHelper.HashPassword(newPassword, newSalt);

        context.SaveChanges();
        _auditService.Log(CurrentUser?.UserId, "CHANGE_PASSWORD", "User", user.UserId.ToString(), null, "Đã đổi mật khẩu thành công");
        return true;
    }
}
