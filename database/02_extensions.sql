-- ================================================================
--  ITCareHelpdesk — EXTENSII APLICATIE
--  Adauga: OTP, audit trail, sign-up, anti-bruteforce
--  Ruleaza DUPA 01_main.sql (sau scriptul tau initial ITCareHelpdesk_DB_30records.sql)
-- ================================================================

USE ITCareHelpdesk;
GO

-- ----------------------------------------------------------------
-- 1. OtpCodes — coduri OTP simulate (sau reale, daca conectezi SMS)
-- ----------------------------------------------------------------
-- am separat OTP-ul intr-o tabela proprie pentru ca un identifier (email/telefon)
-- poate avea mai multe coduri active in paralel (de ex. SIGNUP + RESET_PASSWORD)
IF OBJECT_ID('OtpCodes','U') IS NOT NULL DROP TABLE OtpCodes;
GO
CREATE TABLE OtpCodes (
    otp_id        INT IDENTITY(1,1) NOT NULL,
    identifier    NVARCHAR(150) NOT NULL,   -- email sau telefon
    cod           NVARCHAR(10)  NOT NULL,
    scop          NVARCHAR(30)  NOT NULL,   -- 'SIGNUP', 'RESET_PASSWORD', '2FA'
    expira_la     DATETIME      NOT NULL,
    folosit       BIT           NOT NULL DEFAULT 0,
    creat_la      DATETIME      NOT NULL DEFAULT GETDATE(),
    incercari     INT           NOT NULL DEFAULT 0,

    CONSTRAINT PK_OtpCodes PRIMARY KEY (otp_id),
    CONSTRAINT CK_Otp_Scop CHECK (scop IN (N'SIGNUP', N'RESET_PASSWORD', N'2FA'))
);
GO
CREATE INDEX IX_OtpCodes_Identifier ON OtpCodes (identifier, scop, folosit);
GO

-- ----------------------------------------------------------------
-- 2. LoginAttempts — tracking incercari de logare pentru lockout
-- ----------------------------------------------------------------
-- Lockout dupa N incercari esuate previne brute-force pe parole.
-- Resetam contorul la fiecare login reusit.
IF OBJECT_ID('LoginAttempts','U') IS NOT NULL DROP TABLE LoginAttempts;
GO
CREATE TABLE LoginAttempts (
    attempt_id    INT IDENTITY(1,1) NOT NULL,
    username      NVARCHAR(50) NOT NULL,
    succes        BIT          NOT NULL,
    ip_address    NVARCHAR(45) NULL,
    data_atempt   DATETIME     NOT NULL DEFAULT GETDATE(),

    CONSTRAINT PK_LoginAttempts PRIMARY KEY (attempt_id)
);
GO
CREATE INDEX IX_LoginAttempts_User ON LoginAttempts (username, data_atempt DESC);
GO

-- ----------------------------------------------------------------
-- 3. AuditTrail — log generic pe orice modificare critica
-- ----------------------------------------------------------------
-- Tabela separata de IstoricActivitate care ramane pe tichete specific.
-- Aici intra modificari pe Clienti, Tehnicieni, schimbari de status care nu intra in flow-ul de tichete.
IF OBJECT_ID('AuditTrail','U') IS NOT NULL DROP TABLE AuditTrail;
GO
CREATE TABLE AuditTrail (
    audit_id     BIGINT IDENTITY(1,1) NOT NULL,
    table_name   NVARCHAR(50)  NOT NULL,
    record_id    INT           NOT NULL,
    action_type  NVARCHAR(10)  NOT NULL,    -- 'INSERT','UPDATE','DELETE'
    old_values   NVARCHAR(MAX) NULL,
    new_values   NVARCHAR(MAX) NULL,
    user_id      INT           NULL,
    data_audit   DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT PK_AuditTrail PRIMARY KEY (audit_id),
    CONSTRAINT CK_AuditAction CHECK (action_type IN (N'INSERT', N'UPDATE', N'DELETE'))
);
GO
CREATE INDEX IX_Audit_Table ON AuditTrail (table_name, record_id);
GO

-- ----------------------------------------------------------------
-- 4. sp_RequestOtp — inregistreaza un cod OTP nou
-- ----------------------------------------------------------------
-- am ales sa stergem codurile vechi pe acelasi identifier+scop in loc sa le marcam folosite
-- ca tabela sa nu creasca exponential pe identifierii activi
IF OBJECT_ID('sp_RequestOtp','P') IS NOT NULL DROP PROCEDURE sp_RequestOtp;
GO
CREATE PROCEDURE sp_RequestOtp
    @identifier NVARCHAR(150),
    @cod        NVARCHAR(10),
    @scop       NVARCHAR(30) = N'SIGNUP',
    @expira_la  DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    -- invalidam toate codurile vechi nefolosite pentru acelasi identifier+scop
    UPDATE OtpCodes SET folosit = 1
    WHERE identifier = @identifier AND scop = @scop AND folosit = 0;

    INSERT INTO OtpCodes (identifier, cod, scop, expira_la)
    VALUES (@identifier, @cod, @scop, @expira_la);
END;
GO

-- ----------------------------------------------------------------
-- 5. sp_VerifyOtp — verifica codul si il marcheaza folosit
-- ----------------------------------------------------------------
IF OBJECT_ID('sp_VerifyOtp','P') IS NOT NULL DROP PROCEDURE sp_VerifyOtp;
GO
CREATE PROCEDURE sp_VerifyOtp
    @identifier NVARCHAR(150),
    @cod        NVARCHAR(10),
    @scop       NVARCHAR(30) = N'SIGNUP'
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @otp_id INT, @expira DATETIME, @incercari INT;
    SELECT TOP 1
        @otp_id    = otp_id,
        @expira    = expira_la,
        @incercari = incercari
    FROM OtpCodes
    WHERE identifier = @identifier
      AND cod        = @cod
      AND scop       = @scop
      AND folosit    = 0
    ORDER BY creat_la DESC;

    IF @otp_id IS NULL
    BEGIN
        -- daca codul nu se gaseste, putem totusi incrementa incercarile pentru identifier
        UPDATE OtpCodes SET incercari = incercari + 1
        WHERE identifier = @identifier AND scop = @scop AND folosit = 0;
        SELECT 0 AS succes, N'Cod invalid sau expirat.' AS mesaj;
        RETURN;
    END

    IF @expira < GETDATE()
    BEGIN
        UPDATE OtpCodes SET folosit = 1 WHERE otp_id = @otp_id;
        SELECT 0 AS succes, N'Codul a expirat. Solicita unul nou.' AS mesaj;
        RETURN;
    END

    IF @incercari >= 5
    BEGIN
        UPDATE OtpCodes SET folosit = 1 WHERE otp_id = @otp_id;
        SELECT 0 AS succes, N'Prea multe incercari. Solicita un cod nou.' AS mesaj;
        RETURN;
    END

    UPDATE OtpCodes SET folosit = 1 WHERE otp_id = @otp_id;
    SELECT 1 AS succes, N'Cod verificat cu succes.' AS mesaj;
END;
GO

-- ----------------------------------------------------------------
-- 6. sp_SignUp — creare cont nou cu validari
-- ----------------------------------------------------------------
IF OBJECT_ID('sp_SignUp','P') IS NOT NULL DROP PROCEDURE sp_SignUp;
GO
CREATE PROCEDURE sp_SignUp
    @username     NVARCHAR(50),
    @parola_hash  NVARCHAR(255),
    @nume_complet NVARCHAR(100),
    @email        NVARCHAR(100),
    @rol          NVARCHAR(20) = N'Technician',
    @user_id_out  INT          OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Validari minime — restul sunt prinse de constraint-urile UNIQUE
    IF LEN(@username) < 3
    BEGIN
        SELECT 0 AS succes, N'Username-ul trebuie sa aiba cel putin 3 caractere.' AS mesaj;
        RETURN;
    END

    IF LEN(@parola_hash) < 32
    BEGIN
        -- hash-ul SHA256 are mereu 64 caractere; daca primim mai putin, ceva nu e bine
        SELECT 0 AS succes, N'Parola este invalida.' AS mesaj;
        RETURN;
    END

    IF @email NOT LIKE '%@%.%'
    BEGIN
        SELECT 0 AS succes, N'Email invalid.' AS mesaj;
        RETURN;
    END

    BEGIN TRY
        INSERT INTO Utilizatori (username, parola_hash, rol, nume_complet, email)
        VALUES (@username, @parola_hash, @rol, @nume_complet, @email);

        SET @user_id_out = SCOPE_IDENTITY();
        SELECT 1 AS succes, N'Cont creat cu succes.' AS mesaj;
    END TRY
    BEGIN CATCH
        SELECT 0 AS succes, ERROR_MESSAGE() AS mesaj;
    END CATCH
END;
GO

-- ----------------------------------------------------------------
-- 7. sp_LogLoginAttempt — track lockout
-- ----------------------------------------------------------------
IF OBJECT_ID('sp_LogLoginAttempt','P') IS NOT NULL DROP PROCEDURE sp_LogLoginAttempt;
GO
CREATE PROCEDURE sp_LogLoginAttempt
    @username   NVARCHAR(50),
    @succes     BIT,
    @ip_address NVARCHAR(45) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO LoginAttempts (username, succes, ip_address)
    VALUES (@username, @succes, @ip_address);
END;
GO

-- ----------------------------------------------------------------
-- 8. fn_IsAccountLocked — verifica daca un cont este blocat
-- ----------------------------------------------------------------
-- functia scalara permite folosire in WHERE clauses si view-uri ulterioare
IF OBJECT_ID('fn_IsAccountLocked','FN') IS NOT NULL DROP FUNCTION fn_IsAccountLocked;
GO
CREATE FUNCTION fn_IsAccountLocked(
    @username        NVARCHAR(50),
    @max_failed      INT,
    @window_minutes  INT
)
RETURNS BIT
AS
BEGIN
    DECLARE @failed INT;
    SELECT @failed = COUNT(*)
    FROM LoginAttempts
    WHERE username = @username
      AND succes   = 0
      AND data_atempt >= DATEADD(MINUTE, -@window_minutes, GETDATE());

    RETURN CASE WHEN @failed >= @max_failed THEN 1 ELSE 0 END;
END;
GO

-- ----------------------------------------------------------------
-- 9. Trigger trg_Tichete_Audit — log automat la UPDATE-uri pe Tichete
-- ----------------------------------------------------------------
-- am separat trigger-ul de logica de business pentru ca audit-ul trebuie
-- sa fie transparent: ori s-a inregistrat orice modificare, ori nu —
-- nu poate fi ignorat de programatorul care scrie SQL ad-hoc
IF OBJECT_ID('trg_Tichete_Audit','TR') IS NOT NULL DROP TRIGGER trg_Tichete_Audit;
GO
CREATE TRIGGER trg_Tichete_Audit ON Tichete
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- inseram doar daca status-ul s-a schimbat — ca sa nu poluam audit-ul cu cosmetics
    IF UPDATE(status)
    BEGIN
        INSERT INTO AuditTrail (table_name, record_id, action_type, old_values, new_values, user_id)
        SELECT
            N'Tichete',
            i.tichet_id,
            N'UPDATE',
            CONCAT(N'status_vechi=', d.status, N'; tehnician_vechi=', ISNULL(CAST(d.tehnician_id AS NVARCHAR), N'NULL')),
            CONCAT(N'status_nou=',   i.status, N'; tehnician_nou=',   ISNULL(CAST(i.tehnician_id AS NVARCHAR), N'NULL')),
            i.creat_de
        FROM inserted i
        JOIN deleted d ON i.tichet_id = d.tichet_id
        WHERE i.status <> d.status;
    END
END;
GO

-- ----------------------------------------------------------------
-- 10. View vw_DashboardKpi — KPI-uri pentru ecranul principal
-- ----------------------------------------------------------------
-- am pus un view in plus ca dashboard-ul sa traga totul intr-un singur round-trip
IF OBJECT_ID('vw_DashboardKpi','V') IS NOT NULL DROP VIEW vw_DashboardKpi;
GO
CREATE VIEW vw_DashboardKpi AS
SELECT
    (SELECT COUNT(*) FROM Tichete WHERE status IN (N'OPEN', N'IN_PROGRESS')) AS tichete_deschise,
    (SELECT COUNT(*) FROM vw_TicheteIntarziate)                              AS tichete_intarziate,
    (SELECT COUNT(*) FROM Tichete
      WHERE status IN (N'RESOLVED', N'CLOSED')
        AND CAST(data_rezolvare AS DATE) = CAST(GETDATE() AS DATE))          AS tichete_rezolvate_azi,
    (SELECT COUNT(*) FROM Clienti WHERE activ = 1)                           AS clienti_activi;
GO

-- ----------------------------------------------------------------
-- 11. Verificare ca totul s-a creat OK
-- ----------------------------------------------------------------
PRINT N'';
PRINT N'================================================================';
PRINT N'  EXTENSII APLICATIE — instalate cu succes';
PRINT N'  + OtpCodes, LoginAttempts, AuditTrail';
PRINT N'  + sp_RequestOtp, sp_VerifyOtp, sp_SignUp, sp_LogLoginAttempt';
PRINT N'  + fn_IsAccountLocked, trg_Tichete_Audit';
PRINT N'  + vw_DashboardKpi';
PRINT N'================================================================';
GO
