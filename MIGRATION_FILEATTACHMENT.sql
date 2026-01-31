-- Migration Script for FileAttachment Feature
-- Run this script after building the project to create the FileAttachments table

-- Create FileAttachments table
CREATE TABLE Reports.FileAttachments (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ReportSubmissionId UNIQUEIDENTIFIER NOT NULL,
    QuestionId UNIQUEIDENTIFIER NOT NULL,
    FileName NVARCHAR(255) NOT NULL,
    ContentType NVARCHAR(100) NOT NULL,
    FileSize BIGINT NOT NULL,
    FileData VARBINARY(MAX) NOT NULL,
    Description NVARCHAR(500) NULL,
    CreatedOn DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    LastModifiedOn DATETIME2 NULL,
    LastModifiedBy UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_FileAttachments_ReportSubmissions FOREIGN KEY (ReportSubmissionId)
        REFERENCES Reports.ReportSubmissions(Id) ON DELETE CASCADE
);

-- Create indexes for better query performance
CREATE INDEX IX_FileAttachments_ReportSubmissionId ON Reports.FileAttachments(ReportSubmissionId);
CREATE INDEX IX_FileAttachments_QuestionId ON Reports.FileAttachments(QuestionId);

-- Note: Alternatively, you can run the Entity Framework migration:
-- From ManagementApi/src/Migrators/Migrators.PostgreSQL (or your chosen DB provider):
-- dotnet ef migrations add AddFileAttachmentEntity --startup-project ../../Host
-- dotnet ef database update --startup-project ../../Host
