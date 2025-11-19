using RemoteFileManager.Core.Entities;
using RemoteFileManager.Core.Interfaces.Repositories;
using RemoteFileManager.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
// Thêm các namespace này cho JWT
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace RemoteFileManager.Application.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuthenticationService> _logger;

    // XÓA Dictionary _activeTokens (Không cần lưu trong RAM nữa)

    private readonly string _secretKey;
    private readonly int _tokenExpirationMinutes;
    private readonly string _issuer;
    private readonly string _audience;

    public AuthenticationService(
        IUnitOfWork unitOfWork,
        ILogger<AuthenticationService> logger,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;

        // Cấu hình Key, Issuer, Audience
        _secretKey = configuration["Jwt:Key"] ?? "DayLaMotCaiKeyRatDaiVaBiMat1234567890"; // Key phải >= 16 ký tự
        _tokenExpirationMinutes = configuration.GetValue<int>("Jwt:ExpirationMinutes", 60 * 24 * 7); // Mặc định 7 ngày
        _issuer = configuration["Jwt:Issuer"] ?? "RemoteFileManager";
        _audience = configuration["Jwt:Audience"] ?? "RemoteFileManagerUser";
    }

    public async Task<(bool Success, string Token, User? User)> LoginAsync(string username, string password)
    {
        try
        {
            _logger.LogInformation("Login attempt for username: {Username}", username);

            var user = await _unitOfWork.Users.GetByUsernameAsync(username);

            if (user == null || !user.IsActive)
            {
                _logger.LogWarning("Login failed: User not found or inactive - {Username}", username);
                return (false, string.Empty, null);
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed: Invalid password - {Username}", username);
                return (false, string.Empty, null);
            }

            // Tạo JWT Token thật
            var token = GenerateToken(user);

            // Update last login
            await _unitOfWork.Users.UpdateLastLoginAsync(user.Id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Login successful for user: {Username}", username);

            return (true, token, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for username: {Username}", username);
            return (false, string.Empty, null);
        }
    }

    public string GenerateToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secretKey);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, "User") // Có thể thêm role Admin nếu có
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_tokenExpirationMinutes),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secretKey);

        try
        {
            // Validate chữ ký và thời gian hết hạn
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),

                ValidateIssuer = true,
                ValidIssuer = _issuer,

                ValidateAudience = true,
                ValidAudience = _audience,

                // ClockSkew = TimeSpan.Zero: Không cho phép lệch giờ (chặt chẽ)
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            return true; // Token hợp lệ
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Token validation failed: {ex.Message}");
            return false;
        }
    }

    public async Task<User?> GetUserFromTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            // Đọc nội dung token
            var jwtToken = tokenHandler.ReadJwtToken(token);

            // SỬA ĐOẠN NÀY:
            // Tìm claim có type là "nameid" (chuẩn JWT) HOẶC ClaimTypes.NameIdentifier (dự phòng)
            var userIdClaim = jwtToken.Claims.FirstOrDefault(x =>
                x.Type == "nameid" ||
                x.Type == ClaimTypes.NameIdentifier)?.Value;

            // Log ra để debug nếu vẫn lỗi
            if (userIdClaim == null)
            {
                _logger.LogWarning("Token parsed but 'nameid' claim not found. Available claims: {Claims}",
                    string.Join(", ", jwtToken.Claims.Select(c => c.Type)));
                return null;
            }

            if (int.TryParse(userIdClaim, out int userId))
            {
                return await _unitOfWork.Users.GetByIdAsync(userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error parsing token to get user: {Message}", ex.Message);
            return null;
        }
        return null;
    }

    public async Task LogoutAsync(string token)
    {
        // Với JWT stateless, logout thực sự cần lưu blacklist vào Redis/Database.
        // Ở mức độ đơn giản, Client chỉ cần xóa token là coi như logout.
        // Hàm này giữ lại để đúng Interface, return luôn.
        await Task.CompletedTask;
        _logger.LogInformation("User logged out (Client side removal)");
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch
        {
            return false;
        }
    }
}