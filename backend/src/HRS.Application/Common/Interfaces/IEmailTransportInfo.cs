namespace HRS.Application.Common.Interfaces;

/// <summary>What the settings page's Integrations card shows for email: the configured transport
/// (never credentials).</summary>
public interface IEmailTransportInfo
{
    string Description { get; }
}
