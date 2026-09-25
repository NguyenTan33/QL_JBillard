using QL_JBillard.Domain.Entities;
using QL_JBillard.Domain.Enums;

namespace QL_JBillard.Services.Interfaces;

public interface IAuthService
{
    User? CurrentUser { get; }
    bool IsLoggedIn => CurrentUser != null;
    
    event Action? OnAuthStateChanged;

    bool Login(string username, string password);
    void Logout();
    bool IsInRole(params UserRole[] roles);
    List<User> GetAllUsers();
    bool CreateUser(string username, string password, string fullName, string? phone, UserRole role);
    bool UpdateUser(int userId, string fullName, string? phone, UserRole role, bool isActive, string? newPassword = null);
    bool DeleteUser(int userId);
    bool ToggleUserStatus(int userId);
    User? GetUserById(int userId);
    bool ChangePassword(int userId, string oldPassword, string newPassword);
}
