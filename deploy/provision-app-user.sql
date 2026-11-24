SET NOCOUNT ON;

DECLARE @appPassword nvarchar(128) = N'$(AppPassword)';
DECLARE @loginCommand nvarchar(max);

IF SUSER_ID(N'OnlineLearningPlatformApp') IS NULL
BEGIN
    SET @loginCommand = N'CREATE LOGIN [OnlineLearningPlatformApp] WITH PASSWORD = '
        + QUOTENAME(@appPassword, '''')
        + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
END
ELSE
BEGIN
    SET @loginCommand = N'ALTER LOGIN [OnlineLearningPlatformApp] WITH PASSWORD = '
        + QUOTENAME(@appPassword, '''')
        + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
END;

EXEC (@loginCommand);
GO

IF DB_ID(N'OnlineLearningPlatform') IS NULL
    EXEC (N'CREATE DATABASE [OnlineLearningPlatform]');
GO

USE [OnlineLearningPlatform];
GO

IF USER_ID(N'OnlineLearningPlatformApp') IS NULL
    CREATE USER [OnlineLearningPlatformApp] FOR LOGIN [OnlineLearningPlatformApp];
GO

IF IS_ROLEMEMBER(N'db_datareader', N'OnlineLearningPlatformApp') <> 1
    ALTER ROLE [db_datareader] ADD MEMBER [OnlineLearningPlatformApp];
IF IS_ROLEMEMBER(N'db_datawriter', N'OnlineLearningPlatformApp') <> 1
    ALTER ROLE [db_datawriter] ADD MEMBER [OnlineLearningPlatformApp];
GO
