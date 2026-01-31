-- =============================================
-- Notifications Table Migration
-- Description: Creates the Notifications table for the Report Management System
-- Author: Claude Code
-- Date: 2026-01-26
-- =============================================

-- Create Notifications table
CREATE TABLE [Reports].[Notifications] (
    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    [Type] INT NOT NULL,
    [RecipientId] UNIQUEIDENTIFIER NOT NULL,
    [RecipientName] NVARCHAR(200) NOT NULL,
    [Title] NVARCHAR(200) NOT NULL,
    [Message] NVARCHAR(1000) NOT NULL,
    [RelatedEntityId] UNIQUEIDENTIFIER NULL,
    [RelatedEntityType] NVARCHAR(100) NULL,
    [IsRead] BIT NOT NULL DEFAULT 0,
    [ReadOn] DATETIME2 NULL,
    [Priority] INT NOT NULL DEFAULT 2, -- Normal priority
    [CreatedOn] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] UNIQUEIDENTIFIER NULL,
    [LastModifiedOn] DATETIME2 NULL,
    [LastModifiedBy] UNIQUEIDENTIFIER NULL
);

-- Create indexes for efficient querying
CREATE NONCLUSTERED INDEX [IX_Notifications_RecipientId]
    ON [Reports].[Notifications] ([RecipientId]);

CREATE NONCLUSTERED INDEX [IX_Notifications_RecipientId_IsRead]
    ON [Reports].[Notifications] ([RecipientId], [IsRead]);

CREATE NONCLUSTERED INDEX [IX_Notifications_CreatedOn]
    ON [Reports].[Notifications] ([CreatedOn] DESC);

CREATE NONCLUSTERED INDEX [IX_Notifications_RelatedEntity]
    ON [Reports].[Notifications] ([RelatedEntityId], [RelatedEntityType]);

-- Add comments to document the table
EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Stores notifications sent to users about report-related events',
    @level0type = N'SCHEMA', @level0name = N'Reports',
    @level1type = N'TABLE', @level1name = N'Notifications';

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Notification type: 1=WindowOpened, 2=DeadlineReminder, 3=WindowExtended, 4=WindowClosed, 5=SubmissionApproved, 6=SubmissionRejected, 7=CommentAdded, 8=SubordinateSubmission, 9=BulkApproved, 10=BulkRejected, 11=OverdueAlert',
    @level0type = N'SCHEMA', @level0name = N'Reports',
    @level1type = N'TABLE', @level1name = N'Notifications',
    @level2type = N'COLUMN', @level2name = N'Type';

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Priority level: 1=Low, 2=Normal, 3=High, 4=Urgent',
    @level0type = N'SCHEMA', @level0name = N'Reports',
    @level1type = N'TABLE', @level1name = N'Notifications',
    @level2type = N'COLUMN', @level2name = N'Priority';

-- Verify the table was created
SELECT 'Notifications table created successfully' AS Status;
SELECT COUNT(*) AS IndexCount
FROM sys.indexes
WHERE object_id = OBJECT_ID('[Reports].[Notifications]');
