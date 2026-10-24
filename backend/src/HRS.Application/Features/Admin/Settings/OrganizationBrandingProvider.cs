using HRS.Application.Common.Interfaces;
using HRS.Domain.Admin;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Admin.Settings;

public sealed class OrganizationBrandingProvider(IAppDbContext db, IFileStorage fileStorage) : IOrganizationBrandingProvider
{
    private OrganizationBranding? _withoutLogo;
    private OrganizationBranding? _withLogo;

    public async Task<OrganizationBranding> GetAsync(CancellationToken cancellationToken, bool includeLogo = false)
    {
        var cached = includeLogo ? _withLogo : _withoutLogo;
        if (cached is not null)
        {
            return cached;
        }

        var settings = await db.OrganizationSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            return OrganizationBranding.Default;
        }

        var palette = BrandColors.For(settings.BrandColor);
        byte[]? logo = null;
        if (includeLogo && settings.LogoStorageKey is { } key)
        {
            await using var stored = await fileStorage.GetAsync(key, cancellationToken);
            if (stored is not null)
            {
                using var buffer = new MemoryStream();
                await stored.Content.CopyToAsync(buffer, cancellationToken);
                logo = buffer.ToArray();
            }
        }

        var branding = new OrganizationBranding(
            settings.Name, logo, logo is null ? null : settings.LogoContentType, palette.Primary, palette.Dark,
            OrganizationBranding.Resolve(settings.TimeZone), settings.DateFormat);
        if (includeLogo)
        {
            _withLogo = branding;
        }
        else
        {
            _withoutLogo = branding;
        }

        return branding;
    }
}
