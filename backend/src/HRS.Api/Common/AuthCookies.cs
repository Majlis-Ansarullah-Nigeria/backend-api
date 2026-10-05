using HRS.Application.Common.Interfaces;

namespace HRS.Api.Common;

public static class AuthCookies
{
    public const string AccessToken = "access_token";
    public const string RefreshToken = "refresh_token";

    private const string RefreshTokenPath = "/api/v1/auth";

    public static void SetAuthCookies(this HttpResponse response, AuthTokens tokens)
    {
        response.Cookies.Append(AccessToken, tokens.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = tokens.AccessTokenExpiresAt,
        });

        // Scoped to the auth endpoints only, so the refresh token isn't sent on every API call.
        response.Cookies.Append(RefreshToken, tokens.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = RefreshTokenPath,
            Expires = tokens.RefreshTokenExpiresAt,
        });
    }

    public static void ClearAuthCookies(this HttpResponse response)
    {
        response.Cookies.Delete(AccessToken, new CookieOptions { Path = "/" });
        response.Cookies.Delete(RefreshToken, new CookieOptions { Path = RefreshTokenPath });
    }
}
