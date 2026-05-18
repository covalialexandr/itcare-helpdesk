-- ================================================================
--  ITCareHelpdesk — SCRIPT PRINCIPAL
--  Creeaza baza de date, tabele, view-uri, proceduri stocate.
--  Ruleaza PRIMUL, INAINTE de 02_extensions.sql si 03_seed.sql.
--  Comanda: sqlcmd -S localhost\SQLEXPRESS -E -i 01_main.sql
-- ================================================================

-- ----------------------------------------------------------------
-- 0. RESET COMPLET — drop si recreate al bazei de date
-- ----------------------------------------------------------------
-- ATENTIE: acesta este un script de DEV. Sterge COMPLET baza ITCareHelpdesk
-- daca exista, ca sa garantam ca repornim de la schema curata. In productie
-- aceasta abordare e periculoasa — acolo se folosesc migratii incrementale.
-- Aici reset-ul total e ok pentru ca scriptul re-creeaza schema + seed in cateva secunde.
USE master;
GO

IF DB_ID(N'ITCareHelpdesk') IS NOT NULL
BEGIN
    PRINT N'Drop ITCareHelpdesk existent (single-user pentru a forta inchiderea conexiunilor)...';
    ALTER DATABASE ITCareHelpdesk SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE ITCareHelpdesk;
END
GO

PRINT N'Creez baza de date ITCareHelpdesk...';
CREATE DATABASE ITCareHelpdesk;
GO

USE ITCareHelpdesk;
GO

-- Setam nivelul de izolare standard ca tranzactiile sa nu se calce in picioare
ALTER DATABASE ITCareHelpdesk SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

-- ================================================================
-- 2. TABELE
-- ================================================================

-- ----- Clienti -----
-- Clientul este o "companie" cu contract; tichetele si asset-urile sunt legate de el.
CREATE TABLE Clienti (
    client_id      INT IDENTITY(1,1) NOT NULL,
    nume_companie  NVARCHAR(150) NOT NULL,
    cui            NVARCHAR(20)  NULL,
    industrie      NVARCHAR(50)  NULL,
    oras           NVARCHAR(50)  NULL,
    adresa         NVARCHAR(255) NULL,
    telefon        NVARCHAR(20)  NULL,
    email_contact  NVARCHAR(100) NULL,
    data_contract  DATE          NOT NULL DEFAULT GETDATE(),
    activ          BIT           NOT NULL DEFAULT 1,

    CONSTRAINT PK_Clienti PRIMARY KEY (client_id),
    CONSTRAINT UQ_Clienti_NumeCompanie UNIQUE (nume_companie)
);
GO

-- ----- Tehnicieni -----
-- Tehnicianul este o resursa umana; un Utilizator de tip Technician are FK aici.
CREATE TABLE Tehnicieni (
    tehnician_id    INT IDENTITY(1,1) NOT NULL,
    cod_tehnician   NVARCHAR(20)  NOT NULL,
    nume            NVARCHAR(50)  NOT NULL,
    prenume         NVARCHAR(50)  NOT NULL,
    email           NVARCHAR(100) NOT NULL,
    telefon         NVARCHAR(20)  NULL,
    specializare    NVARCHAR(50)  NOT NULL,
    nivel           NVARCHAR(20)  NOT NULL,
    data_angajare   DATE          NOT NULL DEFAULT GETDATE(),
    activ           BIT           NOT NULL DEFAULT 1,

    CONSTRAINT PK_Tehnicieni PRIMARY KEY (tehnician_id),
    CONSTRAINT UQ_Tehnicieni_Cod UNIQUE (cod_tehnician),
    CONSTRAINT UQ_Tehnicieni_Email UNIQUE (email),
    CONSTRAINT CK_Tehnicieni_Nivel CHECK (nivel IN (N'Junior', N'Mid', N'Senior', N'Lead'))
);
GO

-- ----- Utilizatori -----
-- Utilizator = cont de aplicatie; rolul defineste ce vede in UI.
-- Tehnician_id este nullable: Admin/Manager nu sunt obligatoriu tehnicieni operationali.
CREATE TABLE Utilizatori (
    user_id        INT IDENTITY(1,1) NOT NULL,
    username       NVARCHAR(50)  NOT NULL,
    parola_hash    NVARCHAR(255) NOT NULL,
    rol            NVARCHAR(20)  NOT NULL,
    tehnician_id   INT           NULL,
    nume_complet   NVARCHAR(100) NULL,
    email          NVARCHAR(100) NULL,
    activ          BIT           NOT NULL DEFAULT 1,
    data_creare    DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT PK_Utilizatori PRIMARY KEY (user_id),
    CONSTRAINT UQ_Utilizatori_Username UNIQUE (username),
    CONSTRAINT UQ_Utilizatori_Email UNIQUE (email),
    CONSTRAINT FK_Utilizatori_Tehnician FOREIGN KEY (tehnician_id) REFERENCES Tehnicieni(tehnician_id),
    CONSTRAINT CK_Utilizatori_Rol CHECK (rol IN (N'Admin', N'Manager', N'Technician'))
);
GO

-- ----- Departamente / Categorii -----
-- Departamente: "Network", "Hardware", "Software", "Security".
-- Categoria este sub-tema: "Conectivitate VPN", "Imprimanta", "Office 365" etc.
CREATE TABLE Departamente (
    departament_id    INT IDENTITY(1,1) NOT NULL,
    nume_departament  NVARCHAR(50) NOT NULL,
    CONSTRAINT PK_Departamente PRIMARY KEY (departament_id),
    CONSTRAINT UQ_Departamente_Nume UNIQUE (nume_departament)
);
GO

CREATE TABLE Categorii (
    categorie_id      INT IDENTITY(1,1) NOT NULL,
    nume_categorie    NVARCHAR(100) NOT NULL,
    departament_id    INT           NOT NULL,
    CONSTRAINT PK_Categorii PRIMARY KEY (categorie_id),
    CONSTRAINT FK_Categorii_Departament FOREIGN KEY (departament_id) REFERENCES Departamente(departament_id)
);
GO

-- ----- ContracteSLA -----
-- Un SLA defineste timpii maximi pe (tip_contract, prioritate). Tichetul "atrage" un SLA
-- in functie de tipul contractului clientului si prioritatea tichetului.
CREATE TABLE ContracteSLA (
    sla_id              INT IDENTITY(1,1) NOT NULL,
    tip_sla             NVARCHAR(20) NOT NULL,
    prioritate          NVARCHAR(20) NOT NULL,
    timp_raspuns_ore    INT          NOT NULL,
    timp_rezolvare_ore  INT          NOT NULL,

    CONSTRAINT PK_ContracteSLA PRIMARY KEY (sla_id),
    CONSTRAINT UQ_ContracteSLA UNIQUE (tip_sla, prioritate),
    CONSTRAINT CK_SLA_Tip CHECK (tip_sla IN (N'BRONZE', N'SILVER', N'GOLD', N'PLATINUM')),
    CONSTRAINT CK_SLA_Prio CHECK (prioritate IN (N'CRITICAL', N'HIGH', N'MEDIUM', N'LOW'))
);
GO

-- ----- Assets -----
-- Assets: laptop-uri, servere, switch-uri, imprimante. Apartin unui client.
CREATE TABLE Assets (
    asset_id        INT IDENTITY(1,1) NOT NULL,
    cod_asset       NVARCHAR(20)  NOT NULL,
    denumire        NVARCHAR(150) NOT NULL,
    tip             NVARCHAR(50)  NOT NULL,
    producator      NVARCHAR(50)  NULL,
    model           NVARCHAR(100) NULL,
    serial_number   NVARCHAR(100) NULL,
    client_id       INT           NOT NULL,
    locatie         NVARCHAR(100) NULL,
    status          NVARCHAR(20)  NOT NULL DEFAULT N'ACTIVE',
    data_achizitie  DATE          NULL,
    garantie_pana   DATE          NULL,

    CONSTRAINT PK_Assets PRIMARY KEY (asset_id),
    CONSTRAINT UQ_Assets_Cod UNIQUE (cod_asset),
    CONSTRAINT FK_Assets_Client FOREIGN KEY (client_id) REFERENCES Clienti(client_id),
    CONSTRAINT CK_Asset_Status CHECK (status IN (N'ACTIVE', N'MAINTENANCE', N'DECOMMISSIONED', N'RETIRED'))
);
GO

-- ----- Tichete -----
-- Inima sistemului. Generam numar_tichet automat la insert prin sp_DeschideTichet.
CREATE TABLE Tichete (
    tichet_id        INT IDENTITY(1,1) NOT NULL,
    numar_tichet     NVARCHAR(20)  NOT NULL,
    titlu            NVARCHAR(255) NOT NULL,
    descriere        NVARCHAR(MAX) NULL,
    client_id        INT           NOT NULL,
    categorie_id     INT           NOT NULL,
    tehnician_id     INT           NULL,
    prioritate       NVARCHAR(20)  NOT NULL DEFAULT N'MEDIUM',
    status           NVARCHAR(20)  NOT NULL DEFAULT N'OPEN',
    tip              NVARCHAR(30)  NOT NULL DEFAULT N'INCIDENT',
    sla_id           INT           NULL,
    data_deschidere  DATETIME      NOT NULL DEFAULT GETDATE(),
    data_rezolvare   DATETIME      NULL,
    data_inchidere   DATETIME      NULL,
    ore_lucrate      DECIMAL(6,2)  NULL,
    rating_client    INT           NULL,
    note_inchidere   NVARCHAR(MAX) NULL,
    creat_de         INT           NULL,
    inchis_de        INT           NULL,

    CONSTRAINT PK_Tichete PRIMARY KEY (tichet_id),
    CONSTRAINT UQ_Tichete_Numar UNIQUE (numar_tichet),
    CONSTRAINT FK_Tichete_Client     FOREIGN KEY (client_id)    REFERENCES Clienti(client_id),
    CONSTRAINT FK_Tichete_Categorie  FOREIGN KEY (categorie_id) REFERENCES Categorii(categorie_id),
    CONSTRAINT FK_Tichete_Tehnician  FOREIGN KEY (tehnician_id) REFERENCES Tehnicieni(tehnician_id),
    CONSTRAINT FK_Tichete_SLA        FOREIGN KEY (sla_id)       REFERENCES ContracteSLA(sla_id),
    CONSTRAINT FK_Tichete_CreatDe    FOREIGN KEY (creat_de)     REFERENCES Utilizatori(user_id),
    CONSTRAINT FK_Tichete_InchisDe   FOREIGN KEY (inchis_de)    REFERENCES Utilizatori(user_id),
    CONSTRAINT CK_Tichete_Prio   CHECK (prioritate IN (N'CRITICAL', N'HIGH', N'MEDIUM', N'LOW')),
    CONSTRAINT CK_Tichete_Status CHECK (status IN (N'OPEN', N'IN_PROGRESS', N'PENDING', N'RESOLVED', N'CLOSED', N'CANCELLED')),
    CONSTRAINT CK_Tichete_Tip    CHECK (tip IN (N'INCIDENT', N'REQUEST', N'CHANGE', N'PROBLEM')),
    CONSTRAINT CK_Tichete_Rating CHECK (rating_client IS NULL OR rating_client BETWEEN 1 AND 5)
);
GO

CREATE INDEX IX_Tichete_Status      ON Tichete (status, data_deschidere DESC);
CREATE INDEX IX_Tichete_Tehnician   ON Tichete (tehnician_id, status);
CREATE INDEX IX_Tichete_Client      ON Tichete (client_id, status);
CREATE INDEX IX_Tichete_DataResolve ON Tichete (data_rezolvare) WHERE data_rezolvare IS NOT NULL;
GO

-- ----- IstoricActivitate -----
-- Log de business pe tichet — commenturi, modificari de status, time-tracking.
-- E intentionat DIFERIT de AuditTrail (din 02_extensions) care e un audit tehnic generic.
CREATE TABLE IstoricActivitate (
    istoric_id        INT IDENTITY(1,1) NOT NULL,
    tichet_id         INT           NOT NULL,
    tip_activitate    NVARCHAR(50)  NOT NULL,
    mesaj             NVARCHAR(MAX) NOT NULL,
    status_vechi      NVARCHAR(20)  NULL,
    status_nou        NVARCHAR(20)  NULL,
    efectuat_de       INT           NULL,
    ore_lucrate       DECIMAL(6,2)  NULL,
    data_activitate   DATETIME      NOT NULL DEFAULT GETDATE(),

    CONSTRAINT PK_IstoricActivitate PRIMARY KEY (istoric_id),
    CONSTRAINT FK_Istoric_Tichet  FOREIGN KEY (tichet_id)   REFERENCES Tichete(tichet_id) ON DELETE CASCADE,
    CONSTRAINT FK_Istoric_User    FOREIGN KEY (efectuat_de) REFERENCES Utilizatori(user_id)
);
GO

CREATE INDEX IX_Istoric_Tichet ON IstoricActivitate (tichet_id, data_activitate DESC);
CREATE INDEX IX_Istoric_Data   ON IstoricActivitate (data_activitate DESC);
GO

-- ================================================================
-- 3. VIEW-URI
-- ================================================================

-- ----- vw_TicheteActive -----
-- Denormalizat: contine deja stringurile pe care UI-ul le citeste direct,
-- ca sa nu mai facem 5 JOINuri din C# pe fiecare row.
IF OBJECT_ID('vw_TicheteActive', 'V') IS NOT NULL DROP VIEW vw_TicheteActive;
GO
CREATE VIEW vw_TicheteActive AS
SELECT
    t.tichet_id,
    t.numar_tichet,
    t.titlu,
    c.nume_companie                                       AS client,
    cat.nume_categorie                                    AS categorie,
    d.nume_departament                                    AS departament,
    t.prioritate,
    t.status,
    t.tip,
    -- Tehnician-ul concatenat ca string sau "—" daca neasignat
    ISNULL(teh.prenume + N' ' + teh.nume, N'—')           AS tehnician,
    teh.nivel                                              AS nivel_tehnician,
    sla.tip_sla                                            AS tip_sla,
    sla.timp_raspuns_ore,
    sla.timp_rezolvare_ore,
    DATEDIFF(HOUR, t.data_deschidere, GETDATE())          AS ore_deschis,
    -- sla_depasit: 1 daca tichetul depaseste timpul de rezolvare definit in SLA si nu este inca rezolvat
    CASE
        WHEN sla.sla_id IS NULL THEN 0
        WHEN t.data_rezolvare IS NOT NULL THEN 0
        WHEN DATEDIFF(HOUR, t.data_deschidere, GETDATE()) > sla.timp_rezolvare_ore THEN 1
        ELSE 0
    END                                                    AS sla_depasit,
    t.data_deschidere,
    t.ore_lucrate
FROM Tichete t
JOIN Clienti c          ON t.client_id    = c.client_id
JOIN Categorii cat      ON t.categorie_id = cat.categorie_id
JOIN Departamente d     ON cat.departament_id = d.departament_id
LEFT JOIN Tehnicieni teh ON t.tehnician_id  = teh.tehnician_id
LEFT JOIN ContracteSLA sla ON t.sla_id       = sla.sla_id
WHERE t.status NOT IN (N'CLOSED', N'CANCELLED');
GO

-- ----- vw_TicheteIntarziate -----
-- Tichetele care au depasit SLA-ul de rezolvare si nu sunt finalizate.
IF OBJECT_ID('vw_TicheteIntarziate', 'V') IS NOT NULL DROP VIEW vw_TicheteIntarziate;
GO
CREATE VIEW vw_TicheteIntarziate AS
SELECT
    t.tichet_id,
    t.numar_tichet,
    t.titlu,
    c.nume_companie                                       AS client,
    cat.nume_categorie                                    AS categorie,
    d.nume_departament                                    AS departament,
    t.prioritate,
    t.status,
    ISNULL(teh.prenume + N' ' + teh.nume, N'—')           AS tehnician,
    sla.timp_rezolvare_ore                                AS sla_ore,
    DATEDIFF(HOUR, t.data_deschidere, GETDATE())          AS ore_deschis,
    t.data_deschidere
FROM Tichete t
JOIN Clienti c          ON t.client_id    = c.client_id
JOIN Categorii cat      ON t.categorie_id = cat.categorie_id
JOIN Departamente d     ON cat.departament_id = d.departament_id
LEFT JOIN Tehnicieni teh ON t.tehnician_id  = teh.tehnician_id
LEFT JOIN ContracteSLA sla ON t.sla_id       = sla.sla_id
WHERE t.status NOT IN (N'CLOSED', N'CANCELLED', N'RESOLVED')
  AND sla.sla_id IS NOT NULL
  AND DATEDIFF(HOUR, t.data_deschidere, GETDATE()) > sla.timp_rezolvare_ore;
GO

-- ================================================================
-- 4. STORED PROCEDURES
-- ================================================================

-- ----- sp_Login -----
-- Returneaza un rezultset cu (succes, user_id, rol, mesaj). C#-ul citeste 4 coloane in ordine.
-- IMPORTANT: pentru cont blocat sau inactiv tot returnam un row, dar cu succes=0 si mesaj clar.
IF OBJECT_ID('sp_Login', 'P') IS NOT NULL DROP PROCEDURE sp_Login;
GO
CREATE PROCEDURE sp_Login
    @username    NVARCHAR(50),
    @parola_hash NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    -- Anti-bruteforce: 5 incercari esuate in 15 minute => lock.
    -- Functia fn_IsAccountLocked vine din 02_extensions.sql.
    DECLARE @locked BIT = 0;
    IF OBJECT_ID('dbo.fn_IsAccountLocked', 'FN') IS NOT NULL
        SET @locked = dbo.fn_IsAccountLocked(@username, 5, 15);

    IF @locked = 1
    BEGIN
        SELECT 0 AS succes, CAST(NULL AS INT) AS user_id, CAST(NULL AS NVARCHAR(20)) AS rol,
               N'Cont blocat temporar din cauza incercarilor repetate. Asteapta 15 minute.' AS mesaj;
        RETURN;
    END

    DECLARE @user_id INT, @rol NVARCHAR(20), @activ BIT;
    SELECT TOP 1 @user_id = user_id, @rol = rol, @activ = activ
    FROM Utilizatori
    WHERE username = @username AND parola_hash = @parola_hash;

    IF @user_id IS NULL
    BEGIN
        -- Logam incercarea esuata pentru lockout tracking
        IF OBJECT_ID('sp_LogLoginAttempt', 'P') IS NOT NULL
            EXEC sp_LogLoginAttempt @username = @username, @succes = 0;

        SELECT 0 AS succes, CAST(NULL AS INT) AS user_id, CAST(NULL AS NVARCHAR(20)) AS rol,
               N'Username sau parola gresita.' AS mesaj;
        RETURN;
    END

    IF @activ = 0
    BEGIN
        SELECT 0 AS succes, @user_id AS user_id, @rol AS rol,
               N'Cont inactiv. Contacteaza administratorul.' AS mesaj;
        RETURN;
    END

    -- Success path
    IF OBJECT_ID('sp_LogLoginAttempt', 'P') IS NOT NULL
        EXEC sp_LogLoginAttempt @username = @username, @succes = 1;

    SELECT 1 AS succes, @user_id AS user_id, @rol AS rol,
           N'Autentificare reusita.' AS mesaj;
END;
GO

-- ----- sp_DeschideTichet -----
-- Genereaza un numar_tichet de tipul TKT-2026-00001 si insereaza tichetul.
-- Atribuie automat sla_id pe baza prioritatii (cel mai apropiat SLA care contine prioritatea).
IF OBJECT_ID('sp_DeschideTichet', 'P') IS NOT NULL DROP PROCEDURE sp_DeschideTichet;
GO
CREATE PROCEDURE sp_DeschideTichet
    @titlu          NVARCHAR(255),
    @descriere      NVARCHAR(MAX) = NULL,
    @client_id      INT,
    @categorie_id   INT,
    @prioritate     NVARCHAR(20)  = N'MEDIUM',
    @tip            NVARCHAR(30)  = N'INCIDENT',
    @tehnician_id   INT           = NULL,
    @creat_de       INT           = NULL,
    @tichet_id_out  INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Generare numar_tichet pe pattern TKT-YYYY-NNNNN, incrementand secventa pe an
    DECLARE @year NVARCHAR(4) = CONVERT(NVARCHAR(4), YEAR(GETDATE()));
    DECLARE @last_seq INT;

    SELECT @last_seq = ISNULL(MAX(CAST(SUBSTRING(numar_tichet, 10, 5) AS INT)), 0)
    FROM Tichete
    WHERE numar_tichet LIKE N'TKT-' + @year + N'-%';

    DECLARE @numar NVARCHAR(20) = N'TKT-' + @year + N'-' + RIGHT(N'00000' + CAST((@last_seq + 1) AS NVARCHAR(10)), 5);

    -- Detectam un SLA potrivit pe baza prioritatii — luam primul SLA GOLD pentru prioritatea data;
    -- in productie ar trebui mapat prin contract_id de pe client, dar pentru demo este suficient.
    DECLARE @sla_id INT;
    SELECT TOP 1 @sla_id = sla_id
    FROM ContracteSLA
    WHERE prioritate = @prioritate
    ORDER BY CASE tip_sla
        WHEN N'PLATINUM' THEN 1
        WHEN N'GOLD'     THEN 2
        WHEN N'SILVER'   THEN 3
        ELSE 4
    END;

    INSERT INTO Tichete (numar_tichet, titlu, descriere, client_id, categorie_id, tehnician_id,
                         prioritate, status, tip, sla_id, creat_de)
    VALUES (@numar, @titlu, @descriere, @client_id, @categorie_id, @tehnician_id,
            @prioritate, N'OPEN', @tip, @sla_id, @creat_de);

    SET @tichet_id_out = SCOPE_IDENTITY();

    -- Log in IstoricActivitate
    INSERT INTO IstoricActivitate (tichet_id, tip_activitate, mesaj, status_nou, efectuat_de)
    VALUES (@tichet_id_out, N'CREATE', N'Tichet deschis: ' + @titlu, N'OPEN', @creat_de);
END;
GO

-- ----- sp_InchideTichet -----
IF OBJECT_ID('sp_InchideTichet', 'P') IS NOT NULL DROP PROCEDURE sp_InchideTichet;
GO
CREATE PROCEDURE sp_InchideTichet
    @tichet_id      INT,
    @note_inchidere NVARCHAR(MAX) = NULL,
    @rating_client  INT           = NULL,
    @ore_lucrate    DECIMAL(6,2)  = NULL,
    @inchis_de      INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @old_status NVARCHAR(20);
    SELECT @old_status = status FROM Tichete WHERE tichet_id = @tichet_id;

    UPDATE Tichete
    SET status         = N'CLOSED',
        data_rezolvare = ISNULL(data_rezolvare, GETDATE()),
        data_inchidere = GETDATE(),
        note_inchidere = @note_inchidere,
        rating_client  = @rating_client,
        ore_lucrate    = ISNULL(@ore_lucrate, ore_lucrate),
        inchis_de      = @inchis_de
    WHERE tichet_id = @tichet_id;

    INSERT INTO IstoricActivitate (tichet_id, tip_activitate, mesaj, status_vechi, status_nou, efectuat_de, ore_lucrate)
    VALUES (@tichet_id, N'CLOSE', ISNULL(@note_inchidere, N'Tichet inchis'), @old_status, N'CLOSED', @inchis_de, @ore_lucrate);
END;
GO

-- ----- sp_GetTicheteCritice -----
-- Top tichetele cu prioritate inalta (CRITICAL/HIGH) si timp deschis crescut.
IF OBJECT_ID('sp_GetTicheteCritice', 'P') IS NOT NULL DROP PROCEDURE sp_GetTicheteCritice;
GO
CREATE PROCEDURE sp_GetTicheteCritice
    @ore_raspuns_max INT = 4
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 20
        t.numar_tichet,
        t.titlu,
        c.nume_companie                              AS client,
        cat.nume_categorie                           AS categorie,
        t.prioritate,
        t.status,
        ISNULL(teh.prenume + N' ' + teh.nume, N'—') AS tehnician,
        DATEDIFF(HOUR, t.data_deschidere, GETDATE()) AS ore_deschis,
        sla.timp_rezolvare_ore                       AS sla_ore,
        t.data_deschidere
    FROM Tichete t
    JOIN Clienti c       ON t.client_id    = c.client_id
    JOIN Categorii cat   ON t.categorie_id = cat.categorie_id
    LEFT JOIN Tehnicieni teh ON t.tehnician_id = teh.tehnician_id
    LEFT JOIN ContracteSLA sla ON t.sla_id = sla.sla_id
    WHERE t.prioritate IN (N'CRITICAL', N'HIGH')
      AND t.status NOT IN (N'CLOSED', N'CANCELLED', N'RESOLVED')
    ORDER BY
        CASE t.prioritate WHEN N'CRITICAL' THEN 1 WHEN N'HIGH' THEN 2 ELSE 3 END,
        t.data_deschidere ASC;
END;
GO

-- ----- sp_GetTicheteIntarziate -----
IF OBJECT_ID('sp_GetTicheteIntarziate', 'P') IS NOT NULL DROP PROCEDURE sp_GetTicheteIntarziate;
GO
CREATE PROCEDURE sp_GetTicheteIntarziate
    @departament NVARCHAR(50) = NULL,
    @prioritate  NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        v.numar_tichet,
        v.titlu,
        v.client,
        v.categorie,
        v.departament,
        v.prioritate,
        v.status,
        v.tehnician,
        v.sla_ore,
        v.ore_deschis,
        v.data_deschidere
    FROM vw_TicheteIntarziate v
    WHERE (@departament IS NULL OR v.departament = @departament)
      AND (@prioritate  IS NULL OR v.prioritate  = @prioritate)
    ORDER BY v.ore_deschis DESC;
END;
GO

-- ----- sp_GetIstoricActivitate -----
-- Pe (client_id?, tichet_id?, zile_inapoi). Returneaza data_activitate ca string formatat
-- ca UI-ul sa nu fie nevoit sa formateze nativ DateTime in template.
IF OBJECT_ID('sp_GetIstoricActivitate', 'P') IS NOT NULL DROP PROCEDURE sp_GetIstoricActivitate;
GO
CREATE PROCEDURE sp_GetIstoricActivitate
    @client_id   INT = NULL,
    @tichet_id   INT = NULL,
    @zile_inapoi INT = 7
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        t.numar_tichet,
        c.nume_companie                                                     AS client,
        i.tip_activitate,
        i.mesaj,
        i.status_vechi,
        i.status_nou,
        ISNULL(u.nume_complet, ISNULL(u.username, N'sistem'))               AS efectuat_de,
        i.ore_lucrate,
        FORMAT(i.data_activitate, 'dd MMM yyyy HH:mm', 'ro-RO')              AS data_activitate
    FROM IstoricActivitate i
    JOIN Tichete t ON i.tichet_id = t.tichet_id
    JOIN Clienti c ON t.client_id = c.client_id
    LEFT JOIN Utilizatori u ON i.efectuat_de = u.user_id
    WHERE i.data_activitate >= DATEADD(DAY, -@zile_inapoi, GETDATE())
      AND (@client_id IS NULL OR t.client_id = @client_id)
      AND (@tichet_id IS NULL OR t.tichet_id = @tichet_id)
    ORDER BY i.data_activitate DESC;
END;
GO

-- ----- sp_GetStatisticiTehnicieni -----
-- Aggregat pe tehnician — tichete total/rezolvate/deschise, timp mediu, rating mediu.
IF OBJECT_ID('sp_GetStatisticiTehnicieni', 'P') IS NOT NULL DROP PROCEDURE sp_GetStatisticiTehnicieni;
GO
CREATE PROCEDURE sp_GetStatisticiTehnicieni
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        teh.cod_tehnician,
        (teh.prenume + N' ' + teh.nume)                          AS tehnician,
        teh.specializare,
        teh.nivel,
        COUNT(t.tichet_id)                                        AS total_tichete,
        SUM(CASE WHEN t.status IN (N'CLOSED', N'RESOLVED') THEN 1 ELSE 0 END) AS tichete_rezolvate,
        SUM(CASE WHEN t.status IN (N'OPEN', N'IN_PROGRESS', N'PENDING') THEN 1 ELSE 0 END) AS tichete_deschise,
        AVG(CASE WHEN t.data_rezolvare IS NOT NULL
                 THEN CAST(DATEDIFF(HOUR, t.data_deschidere, t.data_rezolvare) AS DECIMAL(10,2))
                 END)                                              AS timp_mediu_rezolvare_ore,
        AVG(CAST(t.rating_client AS DECIMAL(3,2)))                AS rating_mediu
    FROM Tehnicieni teh
    LEFT JOIN Tichete t ON teh.tehnician_id = t.tehnician_id
    WHERE teh.activ = 1
    GROUP BY teh.tehnician_id, teh.cod_tehnician, teh.prenume, teh.nume, teh.specializare, teh.nivel
    ORDER BY tichete_rezolvate DESC;
END;
GO

-- ----- sp_GetTopTehnicieni -----
-- Acelasi shape ca sp_GetStatisticiTehnicieni dar cu loc_clasament (BIGINT — codul C# foloseste GetInt64).
IF OBJECT_ID('sp_GetTopTehnicieni', 'P') IS NOT NULL DROP PROCEDURE sp_GetTopTehnicieni;
GO
CREATE PROCEDURE sp_GetTopTehnicieni
    @top_n INT = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@top_n) *
    FROM (
        SELECT
            teh.cod_tehnician,
            (teh.prenume + N' ' + teh.nume)                          AS tehnician,
            teh.specializare,
            teh.nivel,
            SUM(CASE WHEN t.status IN (N'CLOSED', N'RESOLVED') THEN 1 ELSE 0 END) AS tichete_rezolvate,
            SUM(CASE WHEN t.status IN (N'OPEN', N'IN_PROGRESS', N'PENDING') THEN 1 ELSE 0 END) AS tichete_deschise,
            AVG(CASE WHEN t.data_rezolvare IS NOT NULL
                     THEN CAST(DATEDIFF(HOUR, t.data_deschidere, t.data_rezolvare) AS DECIMAL(10,2))
                     END)                                              AS timp_mediu_rezolvare_ore,
            AVG(CAST(t.rating_client AS DECIMAL(3,2)))                AS rating_mediu,
            -- loc_clasament intors ca BIGINT pentru ca C#-ul foloseste GetInt64;
            -- ROW_NUMBER() in SQL Server intoarce BIGINT, deci tipul se aliniaza natural.
            ROW_NUMBER() OVER (
                ORDER BY SUM(CASE WHEN t.status IN (N'CLOSED', N'RESOLVED') THEN 1 ELSE 0 END) DESC
            ) AS loc_clasament
        FROM Tehnicieni teh
        LEFT JOIN Tichete t ON teh.tehnician_id = t.tehnician_id
        WHERE teh.activ = 1
        GROUP BY teh.tehnician_id, teh.cod_tehnician, teh.prenume, teh.nume, teh.specializare, teh.nivel
    ) ranked
    ORDER BY loc_clasament;
END;
GO

-- ----- sp_GetTimpiMediiPerCategorie -----
IF OBJECT_ID('sp_GetTimpiMediiPerCategorie', 'P') IS NOT NULL DROP PROCEDURE sp_GetTimpiMediiPerCategorie;
GO
CREATE PROCEDURE sp_GetTimpiMediiPerCategorie
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        cat.nume_categorie                                                    AS categorie,
        d.nume_departament                                                     AS departament,
        COUNT(t.tichet_id)                                                     AS total_tichete,
        SUM(CASE WHEN t.status IN (N'CLOSED', N'RESOLVED') THEN 1 ELSE 0 END)  AS rezolvate,
        CAST(
            CASE WHEN COUNT(t.tichet_id) = 0 THEN 0
                 ELSE 100.0 * SUM(CASE WHEN t.status IN (N'CLOSED', N'RESOLVED') THEN 1 ELSE 0 END) / COUNT(t.tichet_id)
            END AS DECIMAL(5,2)
        )                                                                       AS procent_rezolvate,
        AVG(CASE WHEN t.data_rezolvare IS NOT NULL
                 THEN CAST(DATEDIFF(HOUR, t.data_deschidere, t.data_rezolvare) AS DECIMAL(10,2))
                 END)                                                           AS timp_mediu_ore,
        SUM(t.ore_lucrate)                                                      AS total_ore_lucrate
    FROM Categorii cat
    JOIN Departamente d ON cat.departament_id = d.departament_id
    LEFT JOIN Tichete t ON t.categorie_id = cat.categorie_id
    GROUP BY cat.categorie_id, cat.nume_categorie, d.nume_departament
    ORDER BY total_tichete DESC;
END;
GO

-- ----------------------------------------------------------------
-- 5. Confirmare
-- ----------------------------------------------------------------
PRINT N'';
PRINT N'================================================================';
PRINT N'  SCHEMA PRINCIPALA — instalata cu succes';
PRINT N'  Tabele: Clienti, Tehnicieni, Utilizatori, Departamente,';
PRINT N'          Categorii, ContracteSLA, Assets, Tichete, IstoricActivitate';
PRINT N'  View-uri: vw_TicheteActive, vw_TicheteIntarziate';
PRINT N'  SP-uri: sp_Login, sp_DeschideTichet, sp_InchideTichet,';
PRINT N'          sp_GetTicheteCritice, sp_GetTicheteIntarziate,';
PRINT N'          sp_GetIstoricActivitate, sp_GetStatisticiTehnicieni,';
PRINT N'          sp_GetTopTehnicieni, sp_GetTimpiMediiPerCategorie';
PRINT N'';
PRINT N'  Urmeaza: ruleaza 02_extensions.sql apoi 03_seed.sql';
PRINT N'================================================================';
GO
