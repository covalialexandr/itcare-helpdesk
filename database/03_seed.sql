-- ================================================================
--  ITCareHelpdesk — SEED DATA
--  Date demo pentru o primă rulare: useri, departamente, clienti, tichete.
--  Ruleaza DUPA 01_main.sql si 02_extensions.sql.
-- ================================================================

USE ITCareHelpdesk;
GO

SET NOCOUNT ON;
GO

-- ----------------------------------------------------------------
-- Cleanup pentru re-rulare: stergem datele dar nu schema
-- ----------------------------------------------------------------
-- DELETE-urile sunt no-op pe DB proaspat (cazul tipic dupa 01_main). Pe re-rulare ele curata datele
-- vechi. Ordinea respecta dependentele FK — copiii inainte de parinti.
DELETE FROM IstoricActivitate;
DELETE FROM Tichete;
DELETE FROM Assets;
DELETE FROM Utilizatori;
DELETE FROM Tehnicieni;
DELETE FROM ContracteSLA;
DELETE FROM Categorii;
DELETE FROM Departamente;
DELETE FROM Clienti;
DELETE FROM OtpCodes;
DELETE FROM LoginAttempts;
DELETE FROM AuditTrail;
GO

-- NOTA: NU folosim DBCC CHECKIDENT (RESEED, 0) aici. Pe tabele FRESH (nu au avut niciun INSERT),
-- reseed la 0 face ca primul INSERT sa primeasca identity = 0, NU 1 — comportament documentat
-- dar contraintuitiv. Pe un DB drop-uit si recreat de 01_main.sql identity-ul incepe natural
-- de la 1 si nu trebuie atins. Daca vrei reset complet, re-ruleaza si 01_main.sql.

-- ================================================================
-- 1. DEPARTAMENTE & CATEGORII
-- ================================================================
INSERT INTO Departamente (nume_departament) VALUES
    (N'Network'),
    (N'Hardware'),
    (N'Software'),
    (N'Security');
GO

INSERT INTO Categorii (nume_categorie, departament_id) VALUES
    (N'Conectivitate VPN',          1),
    (N'Wi-Fi / Switching',          1),
    (N'Imprimanta / Periferice',    2),
    (N'Laptop / Desktop',           2),
    (N'Server / Storage',           2),
    (N'Office 365 / Email',         3),
    (N'ERP / Aplicatii business',   3),
    (N'Sistem de operare',          3),
    (N'Acces / Conturi',            4),
    (N'Incident securitate',        4),
    (N'Phishing / Malware',         4);
GO

-- ================================================================
-- 2. CONTRACTE SLA
-- ================================================================
-- 4 niveluri (BRONZE -> PLATINUM) x 4 prioritati = 16 randuri.
-- Logica: PLATINUM raspunde rapid si rezolva rapid; BRONZE invers.
INSERT INTO ContracteSLA (tip_sla, prioritate, timp_raspuns_ore, timp_rezolvare_ore) VALUES
    (N'PLATINUM', N'CRITICAL', 1,  4),
    (N'PLATINUM', N'HIGH',     2,  8),
    (N'PLATINUM', N'MEDIUM',   4, 24),
    (N'PLATINUM', N'LOW',      8, 48),

    (N'GOLD',     N'CRITICAL', 2,  8),
    (N'GOLD',     N'HIGH',     4, 16),
    (N'GOLD',     N'MEDIUM',   8, 48),
    (N'GOLD',     N'LOW',     16, 96),

    (N'SILVER',   N'CRITICAL', 4, 16),
    (N'SILVER',   N'HIGH',     8, 32),
    (N'SILVER',   N'MEDIUM',  16, 72),
    (N'SILVER',   N'LOW',     24,120),

    (N'BRONZE',   N'CRITICAL', 8, 24),
    (N'BRONZE',   N'HIGH',    16, 48),
    (N'BRONZE',   N'MEDIUM',  24, 96),
    (N'BRONZE',   N'LOW',     48,168);
GO

-- ================================================================
-- 3. CLIENTI
-- ================================================================
INSERT INTO Clienti (nume_companie, cui, industrie, oras, telefon, email_contact, data_contract, activ) VALUES
    (N'Aurora Industries',      N'RO12345678', N'Productie',           N'Cluj-Napoca', N'0264111111', N'contact@aurora.ro',    '2023-03-15', 1),
    (N'Bramati Logistics',      N'RO23456789', N'Logistica',           N'Bucuresti',   N'0211222333', N'office@bramati.ro',    '2023-06-22', 1),
    (N'Cetatea Energy',         N'RO34567890', N'Energie',             N'Sibiu',       N'0269333444', N'helpdesk@cetatea.ro',  '2022-11-08', 1),
    (N'Delphi Healthcare',      N'RO45678901', N'Medical',             N'Iasi',        N'0232444555', N'it@delphi-health.ro',  '2024-01-12', 1),
    (N'Eternit Construct',      N'RO56789012', N'Constructii',         N'Brasov',      N'0268555666', N'admin@eternit.ro',     '2023-08-30', 1),
    (N'Fortuna Bank',           N'RO67890123', N'Financiar',           N'Bucuresti',   N'0216667777', N'it@fortunabank.ro',    '2021-05-19', 1),
    (N'Granit Retail',          N'RO78901234', N'Retail',              N'Timisoara',   N'0256777888', N'support@granit.ro',    '2024-02-28', 1),
    (N'Helios Solar',           N'RO89012345', N'Energie regenerabila',N'Oradea',      N'0259888999', N'tech@helios-solar.ro', '2023-10-14', 1),
    (N'Iridium Software',       N'RO90123456', N'IT / Software',       N'Cluj-Napoca', N'0264999000', N'devops@iridium.io',    '2024-04-03', 1),
    (N'Juventus Sports Club',   N'RO01234567', N'Sport / Divertisment',N'Bucuresti',   N'0210000111', N'it@juventus.ro',       '2022-09-25', 1),
    (N'Krestal Pharma',         N'RO11223344', N'Farmaceutic',         N'Iasi',        N'0231111222', N'support@krestal.ro',   '2023-12-01', 1),
    (N'Lumina Education',       N'RO22334455', N'Educatie',            N'Cluj-Napoca', N'0264222333', N'helpdesk@lumina.edu',  '2024-03-17', 1);
GO

-- ================================================================
-- 4. TEHNICIENI
-- ================================================================
INSERT INTO Tehnicieni (cod_tehnician, nume, prenume, email, telefon, specializare, nivel, data_angajare, activ) VALUES
    (N'ITC-T001', N'Popescu',   N'Mihai',    N'mihai.popescu@itcare.ro',    N'0712111111', N'Network & VPN',         N'Senior', '2020-04-10', 1),
    (N'ITC-T002', N'Ionescu',   N'Andreea',  N'andreea.ionescu@itcare.ro',  N'0712222222', N'Hardware & Periferice', N'Mid',    '2021-07-22', 1),
    (N'ITC-T003', N'Vasilescu', N'Cristian', N'cristian.vasilescu@itcare.ro',N'0712333333', N'Server & Storage',     N'Senior', '2019-02-18', 1),
    (N'ITC-T004', N'Dumitru',   N'Elena',    N'elena.dumitru@itcare.ro',    N'0712444444', N'Office 365 & Email',    N'Mid',    '2022-09-05', 1),
    (N'ITC-T005', N'Marin',     N'Adrian',   N'adrian.marin@itcare.ro',     N'0712555555', N'Securitate cibernetica',N'Lead',   '2018-11-30', 1),
    (N'ITC-T006', N'Stanescu',  N'Bianca',   N'bianca.stanescu@itcare.ro',  N'0712666666', N'ERP & Aplicatii',       N'Mid',    '2023-01-14', 1),
    (N'ITC-T007', N'Radu',      N'Sorin',    N'sorin.radu@itcare.ro',       N'0712777777', N'Network & Wi-Fi',       N'Junior', '2024-03-01', 1),
    (N'ITC-T008', N'Pop',       N'Diana',    N'diana.pop@itcare.ro',        N'0712888888', N'Sisteme de operare',    N'Senior', '2020-08-19', 1);
GO

-- ================================================================
-- 5. UTILIZATORI
-- ================================================================
-- Folosim HASHBYTES('SHA2_256', CAST(... AS VARCHAR)) ca rezultatul sa matchuiasca exact
-- SHA256-ul lower-hex pe care AuthService.HashPassword il calculeaza pe UTF-8 bytes.
-- Pentru ASCII strings (cazul demo), VARCHAR si UTF-8 bytes sunt identice. Hash-urile produse aici
-- sunt 100% interoperabile cu C#-ul.
DECLARE @h_admin   NVARCHAR(64) = LOWER(CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', CAST(N'admin123'   AS VARCHAR(255))), 2));
DECLARE @h_manager NVARCHAR(64) = LOWER(CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', CAST(N'manager123' AS VARCHAR(255))), 2));
DECLARE @h_tech    NVARCHAR(64) = LOWER(CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', CAST(N'tech123'    AS VARCHAR(255))), 2));
DECLARE @h_demo    NVARCHAR(64) = LOWER(CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', CAST(N'demo123'    AS VARCHAR(255))), 2));

INSERT INTO Utilizatori (username, parola_hash, rol, tehnician_id, nume_complet, email) VALUES
    (N'admin',           @h_admin,   N'Admin',      NULL, N'Administrator Sistem',  N'admin@itcare.ro'),
    (N'manager.ops',     @h_manager, N'Manager',    NULL, N'Razvan Operatiuni',     N'razvan.ops@itcare.ro'),
    (N'mihai.popescu',   @h_tech,    N'Technician', 1,    N'Mihai Popescu',         N'mihai.popescu@itcare.ro'),
    (N'andreea.ionescu', @h_tech,    N'Technician', 2,    N'Andreea Ionescu',       N'andreea.ionescu@itcare.ro'),
    (N'cristian.v',      @h_tech,    N'Technician', 3,    N'Cristian Vasilescu',    N'cristian.vasilescu@itcare.ro'),
    (N'elena.dumitru',   @h_tech,    N'Technician', 4,    N'Elena Dumitru',         N'elena.dumitru@itcare.ro'),
    (N'adrian.marin',    @h_tech,    N'Technician', 5,    N'Adrian Marin',          N'adrian.marin@itcare.ro'),
    (N'bianca.s',        @h_tech,    N'Technician', 6,    N'Bianca Stanescu',       N'bianca.stanescu@itcare.ro'),
    (N'sorin.radu',      @h_tech,    N'Technician', 7,    N'Sorin Radu',            N'sorin.radu@itcare.ro'),
    (N'diana.pop',       @h_tech,    N'Technician', 8,    N'Diana Pop',             N'diana.pop@itcare.ro'),
    (N'demo',            @h_demo,    N'Technician', NULL, N'Cont Demo',             N'demo@itcare.ro');
GO

-- ================================================================
-- 6. ASSETS
-- ================================================================
INSERT INTO Assets (cod_asset, denumire, tip, producator, model, serial_number, client_id, locatie, status, data_achizitie, garantie_pana) VALUES
    (N'ASSET-0001', N'Laptop Dell XPS 13',          N'Laptop',      N'Dell',     N'XPS 13 9320',  N'SN-DLLXPS-001', 1, N'HQ Cluj — Etaj 2',  N'ACTIVE', '2023-03-20', '2026-03-20'),
    (N'ASSET-0002', N'Server Rack PowerEdge R740',  N'Server',      N'Dell',     N'PowerEdge R740',N'SN-DLLPE-100', 1, N'Datacenter Cluj',   N'ACTIVE', '2022-11-10', '2027-11-10'),
    (N'ASSET-0003', N'Switch Cisco Catalyst 9300',  N'Switch',      N'Cisco',    N'Cat 9300-48P', N'SN-CSC-9300',   2, N'Depot Bucuresti',   N'ACTIVE', '2023-06-25', '2028-06-25'),
    (N'ASSET-0004', N'Imprimanta HP LaserJet',      N'Imprimanta',  N'HP',       N'LaserJet 4555',N'SN-HP-4555',    2, N'Birou Bucuresti',   N'ACTIVE', '2024-01-15', '2026-01-15'),
    (N'ASSET-0005', N'Firewall FortiGate 100F',     N'Firewall',    N'Fortinet', N'FortiGate 100F',N'SN-FG-100F',   3, N'Datacenter Sibiu',  N'ACTIVE', '2022-12-01', '2027-12-01'),
    (N'ASSET-0006', N'NAS Synology DS923+',         N'Storage',     N'Synology', N'DS923+',       N'SN-SY-923',     4, N'Spital Iasi — IT', N'ACTIVE', '2024-02-05', '2027-02-05'),
    (N'ASSET-0007', N'Laptop Lenovo ThinkPad',      N'Laptop',      N'Lenovo',   N'ThinkPad T14', N'SN-LN-T14-22',  5, N'Santier Brasov',    N'MAINTENANCE','2023-09-10','2026-09-10'),
    (N'ASSET-0008', N'Server HPE ProLiant DL380',   N'Server',      N'HPE',      N'ProLiant DL380',N'SN-HPE-DL380', 6, N'Bank HQ Bucuresti', N'ACTIVE', '2021-06-12', '2026-06-12'),
    (N'ASSET-0009', N'Access Point Ubiquiti UAP',   N'Access Point',N'Ubiquiti', N'UAP-AC-Pro',   N'SN-UB-UAP-77',  7, N'Mall Iulius Timis', N'ACTIVE', '2024-03-08', '2027-03-08'),
    (N'ASSET-0010', N'Invertor solar Huawei',       N'Invertor',    N'Huawei',   N'SUN2000-30KTL',N'SN-HW-30KTL',   8, N'Ferma solara Bihor',N'ACTIVE', '2023-11-22', '2033-11-22'),
    (N'ASSET-0011', N'Laptop MacBook Pro M3',       N'Laptop',      N'Apple',    N'MacBook Pro M3',N'SN-APL-M3-19', 9, N'Office Cluj',       N'ACTIVE', '2024-05-03', '2025-05-03'),
    (N'ASSET-0012', N'Router Mikrotik CCR2004',     N'Router',      N'Mikrotik', N'CCR2004-1G-12S',N'SN-MK-CCR04', 10, N'Stadium Bucuresti', N'ACTIVE', '2022-10-30', '2027-10-30'),
    (N'ASSET-0013', N'Statie de lucru HP Z2',       N'Desktop',     N'HP',       N'HP Z2 G9',     N'SN-HP-Z2-31',  11, N'Lab Iasi',          N'ACTIVE', '2023-12-15', '2026-12-15'),
    (N'ASSET-0014', N'Smartboard Promethean',       N'Smartboard',  N'Promethean',N'ActivPanel 9',N'SN-PR-AP9-08', 12, N'Sala curs Cluj',    N'ACTIVE', '2024-04-01', '2027-04-01'),
    (N'ASSET-0015', N'Laptop ASUS Vivobook',        N'Laptop',      N'ASUS',     N'Vivobook 15',  N'SN-AS-VB15-12', 1, N'Birou Cluj',        N'DECOMMISSIONED', '2020-01-15', '2023-01-15');
GO

-- ================================================================
-- 7. TICHETE
-- ================================================================
-- Folosim sp_DeschideTichet ca sa generam numerele cu pattern-ul corect si sa attasam SLA-uri.
-- Apoi update-uim manual statusurile/data_rezolvare ca sa avem o paleta variata in dashboard.

DECLARE @tichet_id INT;

-- Tichete CRITICE / HIGH (active si intarziate)
EXEC sp_DeschideTichet @titlu = N'Server ERP nu raspunde - clienti blocati',
    @descriere = N'Userii din toate filialele primesc timeout cand acceseaza ERP-ul. Posibil leak in pool conexiuni.',
    @client_id = 6, @categorie_id = 5, @prioritate = N'CRITICAL', @tip = N'INCIDENT',
    @tehnician_id = 3, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
-- Backdatam ca tichetul sa para mai vechi
UPDATE Tichete SET data_deschidere = DATEADD(HOUR, -12, GETDATE()), status = N'IN_PROGRESS' WHERE tichet_id = @tichet_id;

EXEC sp_DeschideTichet @titlu = N'Atac phishing - link suspect distribuit pe email',
    @descriere = N'Cca. 30 useri au raportat un email cu link suspect care imita login Microsoft.',
    @client_id = 4, @categorie_id = 11, @prioritate = N'CRITICAL', @tip = N'INCIDENT',
    @tehnician_id = 5, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
UPDATE Tichete SET data_deschidere = DATEADD(HOUR, -6, GETDATE()), status = N'IN_PROGRESS' WHERE tichet_id = @tichet_id;

EXEC sp_DeschideTichet @titlu = N'VPN site-to-site cazut intre HQ si depou',
    @descriere = N'Tunelul IPSec catre depoul din Bucuresti pica intermitent de 2 ore.',
    @client_id = 2, @categorie_id = 1, @prioritate = N'HIGH', @tip = N'INCIDENT',
    @tehnician_id = 1, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
UPDATE Tichete SET data_deschidere = DATEADD(HOUR, -20, GETDATE()), status = N'IN_PROGRESS' WHERE tichet_id = @tichet_id;

EXEC sp_DeschideTichet @titlu = N'Wi-Fi din mall picat in zona alimentatie',
    @descriere = N'Magazinele din food court raporteaza Wi-Fi instabil. POS-urile cad intermitent.',
    @client_id = 7, @categorie_id = 2, @prioritate = N'HIGH', @tip = N'INCIDENT',
    @tehnician_id = 7, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
UPDATE Tichete SET data_deschidere = DATEADD(HOUR, -36, GETDATE()), status = N'OPEN' WHERE tichet_id = @tichet_id;

EXEC sp_DeschideTichet @titlu = N'NAS Synology nu mai face backup-urile programate',
    @descriere = N'Job-ul nocturn de backup esueaza de 3 zile cu eroarea "destination full".',
    @client_id = 4, @categorie_id = 5, @prioritate = N'HIGH', @tip = N'INCIDENT',
    @tehnician_id = 3, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
UPDATE Tichete SET data_deschidere = DATEADD(HOUR, -72, GETDATE()), status = N'PENDING' WHERE tichet_id = @tichet_id;

-- Tichete MEDIUM (active normale)
EXEC sp_DeschideTichet @titlu = N'Solicitare licenta noua Office 365 pentru proaspat angajat',
    @descriere = N'Avem un nou junior dev care are nevoie de Office 365 E3 si Teams.',
    @client_id = 9, @categorie_id = 6, @prioritate = N'MEDIUM', @tip = N'REQUEST',
    @tehnician_id = 4, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
UPDATE Tichete SET data_deschidere = DATEADD(HOUR, -8, GETDATE()), status = N'OPEN' WHERE tichet_id = @tichet_id;

EXEC sp_DeschideTichet @titlu = N'Imprimanta HP din etaj 3 da hartie blocata',
    @descriere = N'Jam la fiecare 5-6 pagini de imprimare. Posibil rola feed uzata.',
    @client_id = 2, @categorie_id = 3, @prioritate = N'MEDIUM', @tip = N'INCIDENT',
    @tehnician_id = 2, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
UPDATE Tichete SET data_deschidere = DATEADD(HOUR, -16, GETDATE()), status = N'IN_PROGRESS' WHERE tichet_id = @tichet_id;

EXEC sp_DeschideTichet @titlu = N'Migrare statie de lucru pe Windows 11',
    @descriere = N'Userul s-a actualizat la 24H2 si pierde profilul Outlook la fiecare boot.',
    @client_id = 11, @categorie_id = 8, @prioritate = N'MEDIUM', @tip = N'PROBLEM',
    @tehnician_id = 8, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
UPDATE Tichete SET data_deschidere = DATEADD(HOUR, -28, GETDATE()), status = N'OPEN' WHERE tichet_id = @tichet_id;

EXEC sp_DeschideTichet @titlu = N'Conturi de acces pentru proiect nou - 12 useri',
    @descriere = N'Cream conturi AD + acces SharePoint pentru proiectul "Aurora 2026".',
    @client_id = 1, @categorie_id = 9, @prioritate = N'MEDIUM', @tip = N'REQUEST',
    @tehnician_id = 6, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
UPDATE Tichete SET data_deschidere = DATEADD(HOUR, -4, GETDATE()), status = N'OPEN' WHERE tichet_id = @tichet_id;

-- Tichete LOW (active si REZOLVATE)
EXEC sp_DeschideTichet @titlu = N'Cerere mouse wireless de schimb',
    @descriere = N'Userul cere un mouse Logitech MX Master 3 ca inlocuitor.',
    @client_id = 10, @categorie_id = 4, @prioritate = N'LOW', @tip = N'REQUEST',
    @tehnician_id = 2, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
UPDATE Tichete SET data_deschidere = DATEADD(HOUR, -2, GETDATE()), status = N'OPEN' WHERE tichet_id = @tichet_id;

-- Tichete REZOLVATE/INCHISE in ultimele 7 zile (pentru KPI + statistici)
EXEC sp_DeschideTichet @titlu = N'Reinstalare driver imprimanta',
    @descriere = N'Driver corupt dupa Windows Update.',
    @client_id = 1, @categorie_id = 3, @prioritate = N'MEDIUM', @tip = N'INCIDENT',
    @tehnician_id = 2, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
UPDATE Tichete
SET data_deschidere = DATEADD(HOUR, -54, GETDATE()),
    data_rezolvare = DATEADD(HOUR, -50, GETDATE()),
    data_inchidere = DATEADD(HOUR, -48, GETDATE()),
    status = N'CLOSED',
    ore_lucrate = 1.5,
    rating_client = 5,
    inchis_de = 4,
    note_inchidere = N'Driver reinstalat din mod safe. Test print reusit.'
WHERE tichet_id = @tichet_id;

EXEC sp_DeschideTichet @titlu = N'Configurare email pe telefon nou',
    @descriere = N'Outlook mobile pe iPhone 15 al directorului tehnic.',
    @client_id = 6, @categorie_id = 6, @prioritate = N'LOW', @tip = N'REQUEST',
    @tehnician_id = 4, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
UPDATE Tichete
SET data_deschidere = DATEADD(HOUR, -30, GETDATE()),
    data_rezolvare = DATEADD(HOUR, -29, GETDATE()),
    data_inchidere = DATEADD(HOUR, -28, GETDATE()),
    status = N'CLOSED',
    ore_lucrate = 0.5,
    rating_client = 4,
    inchis_de = 4
WHERE tichet_id = @tichet_id;

EXEC sp_DeschideTichet @titlu = N'Reactivare cont Office 365 dezactivat din greseala',
    @descriere = N'Userul a fost dezactivat automat din lipsa de loguri timp de 30 zile.',
    @client_id = 9, @categorie_id = 6, @prioritate = N'MEDIUM', @tip = N'REQUEST',
    @tehnician_id = 4, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
UPDATE Tichete
SET data_deschidere = DATEADD(HOUR, -100, GETDATE()),
    data_rezolvare = DATEADD(HOUR, -95, GETDATE()),
    data_inchidere = DATEADD(HOUR, -94, GETDATE()),
    status = N'CLOSED',
    ore_lucrate = 0.75,
    rating_client = 5,
    inchis_de = 4
WHERE tichet_id = @tichet_id;

EXEC sp_DeschideTichet @titlu = N'Suspect malware pe statia contabilitate',
    @descriere = N'Antivirusul a detectat un trojan in folderul Downloads.',
    @client_id = 3, @categorie_id = 11, @prioritate = N'HIGH', @tip = N'INCIDENT',
    @tehnician_id = 5, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
UPDATE Tichete
SET data_deschidere = DATEADD(DAY, -3, GETDATE()),
    data_rezolvare = DATEADD(DAY, -3, DATEADD(HOUR, 4, GETDATE())),
    data_inchidere = DATEADD(DAY, -2, GETDATE()),
    status = N'CLOSED',
    ore_lucrate = 4.0,
    rating_client = 5,
    inchis_de = 5,
    note_inchidere = N'Fisier in carantina, statie scanata complet, niciun alt artifact gasit.'
WHERE tichet_id = @tichet_id;

EXEC sp_DeschideTichet @titlu = N'Restart router Mikrotik dupa pana de curent',
    @descriere = N'Routerul a ramas in stare degradata dupa power cycling necontrolat.',
    @client_id = 10, @categorie_id = 1, @prioritate = N'HIGH', @tip = N'INCIDENT',
    @tehnician_id = 1, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
UPDATE Tichete
SET data_deschidere = DATEADD(DAY, -5, GETDATE()),
    data_rezolvare = DATEADD(DAY, -5, DATEADD(HOUR, 2, GETDATE())),
    data_inchidere = DATEADD(DAY, -4, GETDATE()),
    status = N'CLOSED',
    ore_lucrate = 2.0,
    rating_client = 4,
    inchis_de = 1
WHERE tichet_id = @tichet_id;

-- Cateva tichete REZOLVATE AZI ca dashboard-ul sa arate live activitate
EXEC sp_DeschideTichet @titlu = N'Resetare parola conturi VPN - 5 useri',
    @descriere = N'Conturi expirate automatic dupa 90 zile.',
    @client_id = 5, @categorie_id = 9, @prioritate = N'MEDIUM', @tip = N'REQUEST',
    @tehnician_id = 1, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
UPDATE Tichete
SET data_deschidere = DATEADD(HOUR, -5, GETDATE()),
    data_rezolvare = DATEADD(HOUR, -2, GETDATE()),
    data_inchidere = DATEADD(HOUR, -1, GETDATE()),
    status = N'CLOSED',
    ore_lucrate = 1.0,
    rating_client = 5,
    inchis_de = 1
WHERE tichet_id = @tichet_id;

EXEC sp_DeschideTichet @titlu = N'Update firmware switch Cisco esuat',
    @descriere = N'Update-ul a fost rollback automat dar lasa switch-ul in stare flaky.',
    @client_id = 2, @categorie_id = 2, @prioritate = N'MEDIUM', @tip = N'PROBLEM',
    @tehnician_id = 7, @creat_de = 2, @tichet_id_out = @tichet_id OUTPUT;
UPDATE Tichete
SET data_deschidere = DATEADD(HOUR, -8, GETDATE()),
    data_rezolvare = DATEADD(HOUR, -1, GETDATE()),
    data_inchidere = DATEADD(MINUTE, -30, GETDATE()),
    status = N'CLOSED',
    ore_lucrate = 6.0,
    rating_client = 4,
    inchis_de = 7
WHERE tichet_id = @tichet_id;
GO

-- Cateva activitati suplimentare in IstoricActivitate ca timeline-ul "Istoric" sa arate plin
INSERT INTO IstoricActivitate (tichet_id, tip_activitate, mesaj, status_vechi, status_nou, efectuat_de, ore_lucrate, data_activitate)
SELECT TOP 1 tichet_id, N'COMMENT', N'Am cerut info suplimentare clientului despre versiunea ERP-ului.', N'OPEN', N'IN_PROGRESS', 3, 0.5, DATEADD(HOUR, -10, GETDATE())
FROM Tichete WHERE numar_tichet LIKE N'TKT-%-00001';

INSERT INTO IstoricActivitate (tichet_id, tip_activitate, mesaj, status_vechi, status_nou, efectuat_de, ore_lucrate, data_activitate)
SELECT TOP 1 tichet_id, N'WORK_LOG', N'Restart aplicatie + verificare logs SQL Server.', N'IN_PROGRESS', N'IN_PROGRESS', 3, 1.5, DATEADD(HOUR, -8, GETDATE())
FROM Tichete WHERE numar_tichet LIKE N'TKT-%-00001';

INSERT INTO IstoricActivitate (tichet_id, tip_activitate, mesaj, status_vechi, status_nou, efectuat_de, ore_lucrate, data_activitate)
SELECT TOP 1 tichet_id, N'ASSIGN', N'Escaladat catre L3 — root-cause probabil la nivel de pool de conexiuni.', NULL, NULL, 5, NULL, DATEADD(HOUR, -3, GETDATE())
FROM Tichete WHERE numar_tichet LIKE N'TKT-%-00001';

INSERT INTO IstoricActivitate (tichet_id, tip_activitate, mesaj, status_vechi, status_nou, efectuat_de, ore_lucrate, data_activitate)
SELECT TOP 1 tichet_id, N'COMMENT', N'User-ul a confirmat ca problema este izolata pe filiala Bucuresti.', NULL, NULL, 1, NULL, DATEADD(HOUR, -15, GETDATE())
FROM Tichete WHERE numar_tichet LIKE N'TKT-%-00003';
GO

-- ----------------------------------------------------------------
-- Confirmare
-- ----------------------------------------------------------------
PRINT N'';
PRINT N'================================================================';
PRINT N'  SEED DATA — incarcat cu succes';
PRINT N'';
PRINT N'  Conturi disponibile (parola este aceeasi cu username-ul + 123):';
PRINT N'    admin           / admin123          (rol: Admin)';
PRINT N'    manager.ops     / manager123        (rol: Manager)';
PRINT N'    mihai.popescu   / tech123           (rol: Technician)';
PRINT N'    andreea.ionescu / tech123           (rol: Technician)';
PRINT N'    cristian.v      / tech123           (rol: Technician)';
PRINT N'    elena.dumitru   / tech123           (rol: Technician)';
PRINT N'    adrian.marin    / tech123           (rol: Technician — Security)';
PRINT N'    bianca.s        / tech123           (rol: Technician — ERP)';
PRINT N'    sorin.radu      / tech123           (rol: Technician — Network)';
PRINT N'    diana.pop       / tech123           (rol: Technician)';
PRINT N'    demo            / demo123           (rol: Technician — fara tehnician_id)';
PRINT N'';
PRINT N'  Volum:';
DECLARE @nc INT, @nt INT, @nk INT, @na INT;
SELECT @nc = COUNT(*) FROM Clienti;
SELECT @nt = COUNT(*) FROM Tehnicieni;
SELECT @nk = COUNT(*) FROM Tichete;
SELECT @na = COUNT(*) FROM Assets;
PRINT N'    Clienti:     ' + CAST(@nc AS NVARCHAR(10));
PRINT N'    Tehnicieni:  ' + CAST(@nt AS NVARCHAR(10));
PRINT N'    Tichete:     ' + CAST(@nk AS NVARCHAR(10));
PRINT N'    Asset-uri:   ' + CAST(@na AS NVARCHAR(10));
PRINT N'================================================================';
GO
