SET NOCOUNT ON;

IF DB_ID(N'OnlineLearningPlatform') IS NULL
    EXEC (N'CREATE DATABASE [OnlineLearningPlatform]');
GO

DECLARE @appPassword nvarchar(128) = N'$(AppPassword)';
DECLARE @command nvarchar(max);
IF SUSER_ID(N'OnlineLearningPlatformApp') IS NULL
    SET @command = N'CREATE LOGIN [OnlineLearningPlatformApp] WITH PASSWORD = ' + QUOTENAME(@appPassword, '''') + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
ELSE
    SET @command = N'ALTER LOGIN [OnlineLearningPlatformApp] WITH PASSWORD = ' + QUOTENAME(@appPassword, '''') + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
EXEC (@command);
GO

DECLARE @migratorPassword nvarchar(128) = N'$(MigratorPassword)';
DECLARE @command nvarchar(max);
IF SUSER_ID(N'OnlineLearningPlatformMigrator') IS NULL
    SET @command = N'CREATE LOGIN [OnlineLearningPlatformMigrator] WITH PASSWORD = ' + QUOTENAME(@migratorPassword, '''') + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
ELSE
    SET @command = N'ALTER LOGIN [OnlineLearningPlatformMigrator] WITH PASSWORD = ' + QUOTENAME(@migratorPassword, '''') + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
EXEC (@command);
GO

USE [OnlineLearningPlatform];
GO

IF USER_ID(N'OnlineLearningPlatformApp') IS NULL
    CREATE USER [OnlineLearningPlatformApp] FOR LOGIN [OnlineLearningPlatformApp];
IF IS_ROLEMEMBER(N'db_datareader', N'OnlineLearningPlatformApp') <> 1
    ALTER ROLE [db_datareader] ADD MEMBER [OnlineLearningPlatformApp];
IF IS_ROLEMEMBER(N'db_datawriter', N'OnlineLearningPlatformApp') <> 1
    ALTER ROLE [db_datawriter] ADD MEMBER [OnlineLearningPlatformApp];
GO

IF USER_ID(N'OnlineLearningPlatformMigrator') IS NULL
    CREATE USER [OnlineLearningPlatformMigrator] FOR LOGIN [OnlineLearningPlatformMigrator];
IF IS_ROLEMEMBER(N'db_datareader', N'OnlineLearningPlatformMigrator') <> 1
    ALTER ROLE [db_datareader] ADD MEMBER [OnlineLearningPlatformMigrator];
IF IS_ROLEMEMBER(N'db_datawriter', N'OnlineLearningPlatformMigrator') <> 1
    ALTER ROLE [db_datawriter] ADD MEMBER [OnlineLearningPlatformMigrator];
IF IS_ROLEMEMBER(N'db_ddladmin', N'OnlineLearningPlatformMigrator') <> 1
    ALTER ROLE [db_ddladmin] ADD MEMBER [OnlineLearningPlatformMigrator];
GO
