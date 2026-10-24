using HRS.Application.Features.Review.Compiled;

namespace HRS.Application.Common.Interfaces;

/// <summary>FR-REV-006's exports of the compiled national report: "exportable to Word and PDF".</summary>
public interface ICompiledReportRenderer
{
    byte[] RenderPdf(CompiledReportView report, OrganizationBranding branding);

    byte[] RenderWord(CompiledReportView report, OrganizationBranding branding);
}
