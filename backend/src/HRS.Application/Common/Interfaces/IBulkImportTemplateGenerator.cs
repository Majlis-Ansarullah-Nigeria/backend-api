namespace HRS.Application.Common.Interfaces;

/// <summary>Builds the downloadable .xlsx template for bulk import (FR-ORG-003).</summary>
public interface IBulkImportTemplateGenerator
{
    byte[] Generate();
}
