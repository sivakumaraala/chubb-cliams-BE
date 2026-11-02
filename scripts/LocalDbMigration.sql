IF DB_NAME() <> N'ChubbClaims'
    THROW 50000, 'Select the ChubbClaims database before running this script.', 1;
GO

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102111333_InitialClaims'
)
BEGIN
    CREATE TABLE [Claims] (
        [Id] uniqueidentifier NOT NULL,
        [Reference] nvarchar(40) NOT NULL,
        [OwnerId] nvarchar(64) NOT NULL,
        [Type] nvarchar(20) NOT NULL,
        [IncidentDate] date NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [Market] nvarchar(64) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [ClaimedAmount] decimal(18,2) NOT NULL,
        [ApprovedAmount] decimal(18,2) NULL,
        [ReserveAmount] decimal(18,2) NOT NULL,
        [AssignedOfficerId] nvarchar(64) NULL,
        [Status] nvarchar(30) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [UpdatedAtUtc] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Claims] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102111333_InitialClaims'
)
BEGIN
    CREATE TABLE [OutboxMessages] (
        [Id] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [Payload] nvarchar(max) NOT NULL,
        [CorrelationId] nvarchar(128) NOT NULL,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        [PublishedAtUtc] datetimeoffset NULL,
        [Attempts] int NOT NULL,
        [NextAttemptAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102111333_InitialClaims'
)
BEGIN
    CREATE TABLE [ProcessedEvents] (
        [EventId] uniqueidentifier NOT NULL,
        [ProcessedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ProcessedEvents] PRIMARY KEY ([EventId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102111333_InitialClaims'
)
BEGIN
    CREATE TABLE [ClaimDecisions] (
        [Id] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [OfficerId] nvarchar(64) NOT NULL,
        [Outcome] nvarchar(30) NOT NULL,
        [ApprovedAmount] decimal(18,2) NULL,
        [Reason] nvarchar(2000) NOT NULL,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ClaimDecisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClaimDecisions_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [Claims] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102111333_InitialClaims'
)
BEGIN
    CREATE TABLE [ClaimHistory] (
        [Id] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [ActorId] nvarchar(64) NOT NULL,
        [Action] nvarchar(30) NOT NULL,
        [FromStatus] nvarchar(30) NULL,
        [ToStatus] nvarchar(30) NOT NULL,
        [ReserveBefore] decimal(18,2) NULL,
        [ReserveAfter] decimal(18,2) NOT NULL,
        [OccurredAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ClaimHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClaimHistory_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [Claims] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102111333_InitialClaims'
)
BEGIN
    CREATE TABLE [ClaimNotifications] (
        [Id] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [OwnerId] nvarchar(64) NOT NULL,
        [Message] nvarchar(500) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ClaimNotifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClaimNotifications_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [Claims] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102111333_InitialClaims'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ClaimDecisions_ClaimId] ON [ClaimDecisions] ([ClaimId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102111333_InitialClaims'
)
BEGIN
    CREATE INDEX [IX_ClaimHistory_ClaimId_OccurredAtUtc] ON [ClaimHistory] ([ClaimId], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102111333_InitialClaims'
)
BEGIN
    CREATE INDEX [IX_ClaimNotifications_ClaimId_CreatedAtUtc] ON [ClaimNotifications] ([ClaimId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102111333_InitialClaims'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ClaimNotifications_EventId] ON [ClaimNotifications] ([EventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102111333_InitialClaims'
)
BEGIN
    CREATE INDEX [IX_Claims_OwnerId] ON [Claims] ([OwnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102111333_InitialClaims'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Claims_Reference] ON [Claims] ([Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102111333_InitialClaims'
)
BEGIN
    CREATE INDEX [IX_Claims_Status_AssignedOfficerId] ON [Claims] ([Status], [AssignedOfficerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102111333_InitialClaims'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_PublishedAtUtc_NextAttemptAtUtc] ON [OutboxMessages] ([PublishedAtUtc], [NextAttemptAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102111333_InitialClaims'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102111333_InitialClaims', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102191056_InformationRequests'
)
BEGIN
    CREATE TABLE [ClaimInformationRequests] (
        [Id] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [OfficerId] nvarchar(64) NOT NULL,
        [Question] nvarchar(2000) NOT NULL,
        [RequestedAtUtc] datetimeoffset NOT NULL,
        [Response] nvarchar(2000) NULL,
        [RespondedAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_ClaimInformationRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClaimInformationRequests_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [Claims] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102191056_InformationRequests'
)
BEGIN
    CREATE INDEX [IX_ClaimInformationRequests_ClaimId_RequestedAtUtc] ON [ClaimInformationRequests] ([ClaimId], [RequestedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102191056_InformationRequests'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102191056_InformationRequests', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102193748_SettlementPayments'
)
BEGIN
    CREATE TABLE [ClaimSettlementPayments] (
        [Id] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [OfficerId] nvarchar(64) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [ExternalReference] nvarchar(100) NOT NULL,
        [PaymentDate] date NOT NULL,
        [RecordedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ClaimSettlementPayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClaimSettlementPayments_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [Claims] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102193748_SettlementPayments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ClaimSettlementPayments_ClaimId_ExternalReference] ON [ClaimSettlementPayments] ([ClaimId], [ExternalReference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102193748_SettlementPayments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102193748_SettlementPayments', N'10.0.12');
END;

COMMIT;
GO


