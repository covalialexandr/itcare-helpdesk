# ITCare Helpdesk — Quickstart

Aplicație desktop Avalonia (.NET 8) + SQL Server pentru gestiunea unui helpdesk IT.

## Cerințe

- .NET 8 SDK ([download](https://dotnet.microsoft.com/download/dotnet/8.0))
- SQL Server Express 2019+ cu instanță `SQLEXPRESS` (sau modifică connection string-ul)
- Windows 10/11 (UI-ul folosește AcrylicBlur — pe alte OS pică gracefully la fundal solid)

## Setup în 3 pași

### 1. Instalează schema în SQL Server

Deschide PowerShell în folderul `database/` și rulează în ordine:

```powershell
sqlcmd -S localhost\SQLEXPRESS -E -i 01_main.sql
sqlcmd -S localhost\SQLEXPRESS -E -i 02_extensions.sql
sqlcmd -S localhost\SQLEXPRESS -E -i 03_seed.sql
```

Sau, dacă vrei one-liner:

```powershell
cd database
@('01_main.sql','02_extensions.sql','03_seed.sql') | ForEach-Object { sqlcmd -S localhost\SQLEXPRESS -E -i $_ }
```

> **Notă**: `01_main.sql` creează DB-ul `ITCareHelpdesk` dacă nu există. Re-rularea scripturilor șterge tabelele și le recrează — bun pentru reset rapid.

### 2. Verifică connection string-ul

`src/ITCareHelpdesk.App/appsettings.json` este configurat pentru `localhost\SQLEXPRESS` cu Trusted Connection. Dacă instanța ta SQL are alt nume, modifică acolo.

### 3. Build & Run

```powershell
cd src/ITCareHelpdesk.App
dotnet run
```

Sau deschide `ITCareHelpdesk.sln` în Rider/Visual Studio și apasă **F5**.

## Conturi demo

| Username          | Parolă       | Rol         |
|-------------------|--------------|-------------|
| `admin`           | `admin123`   | Admin       |
| `manager.ops`     | `manager123` | Manager     |
| `mihai.popescu`   | `tech123`    | Technician  |
| `andreea.ionescu` | `tech123`    | Technician  |
| `adrian.marin`    | `tech123`    | Technician (Security) |
| `demo`            | `demo123`    | Technician  |

> Toți tehnicienii din seed au parola `tech123`.

## Sign-up nou (cu OTP)

Click **"Creează un cont nou"** pe ecranul de login. După formular, primești un cod de 6 cifre în toast (modul `OtpSimulationMode: true` din `appsettings.json` — în producție s-ar trimite prin SMS/email). Lipește codul (Ctrl+V pe prima căsuță distribuie automat) și gata.

## Structură

```
ITCareHelpdesk/
├── database/
│   ├── 01_main.sql          # schema + view-uri + proceduri de bază
│   ├── 02_extensions.sql    # OTP, lockout, audit, vw_DashboardKpi
│   └── 03_seed.sql          # date demo
├── src/ITCareHelpdesk.App/
│   ├── Controls/            # ParticleField, Converters
│   ├── Models/              # POCO records
│   ├── Services/            # DB, Auth, OTP, Toast, Navigation, Repositories
│   ├── ViewModels/          # MVVM (CommunityToolkit.Mvvm)
│   ├── Views/               # AXAML + code-behind
│   ├── Themes/              # Palette, DarkTheme, Controls
│   ├── App.axaml(+.cs)
│   ├── Program.cs
│   └── appsettings.json
└── ITCareHelpdesk.sln
```

## Troubleshooting

**Splash arată "Conexiunea la SQL Server a esuat":**
- Verifică că serviciul `MSSQL$SQLEXPRESS` rulează (Services.msc)
- Testează manual: `sqlcmd -S localhost\SQLEXPRESS -E -Q "SELECT @@VERSION"`
- Connection string-ul din `appsettings.json` să indice instanța corectă

**Build error pe Avalonia / SkiaSharp:**
- Rulează `dotnet restore` în folderul proiectului
- Asigură-te că SDK-ul .NET 8 este instalat: `dotnet --list-sdks`

**OTP-ul nu apare în toast:**
- Setarea `App:OtpSimulationMode` din `appsettings.json` trebuie să fie `true`
- Dacă e `false`, sistemul așteaptă un provider SMS/email real

**Login cu admin/admin123 returnează "parola gresita":**
- Verifică că hash-ul a fost generat corect prin SQL: `SELECT LOWER(CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', CAST('admin123' AS VARCHAR(255))), 2));` — trebuie să dea `ef92b778bafe771e89245b89ecbc08a44a4e166c06659911881f383d4473e94f`
- Dacă seed-ul a rulat parțial: re-rulează `03_seed.sql`

## Reset complet

Dacă vrei să resetezi totul (păstrând schema):

```powershell
sqlcmd -S localhost\SQLEXPRESS -E -i 03_seed.sql
```

Pentru reset total (re-create schema):

```powershell
sqlcmd -S localhost\SQLEXPRESS -E -Q "DROP DATABASE IF EXISTS ITCareHelpdesk;"
@('01_main.sql','02_extensions.sql','03_seed.sql') | ForEach-Object { sqlcmd -S localhost\SQLEXPRESS -E -i $_ }
```
