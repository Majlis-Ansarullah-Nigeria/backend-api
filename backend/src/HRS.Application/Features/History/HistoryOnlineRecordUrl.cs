namespace HRS.Application.Features.History;

/// <summary>The same own-vs-observed route split the /history screen's own "View" link uses
/// (M9.1) — centralized here since the PDF's QR code, the Excel download, and a future compare
/// link all need the identical URL.</summary>
public static class HistoryOnlineRecordUrl
{
    public static string Build(string publicUrl, Guid unitId, Guid instanceId, bool isObserved)
    {
        var baseUrl = publicUrl.TrimEnd('/');
        return isObserved ? $"{baseUrl}/units/{unitId}/reports/{instanceId}" : $"{baseUrl}/reports/{instanceId}/edit?unit={unitId}";
    }
}
