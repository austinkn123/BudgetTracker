# Workflow Diagrams

This document shows the two main workflows we discussed for BudgetTracker:

- Entity Framework Core database changes
- Mobile delivery using Capacitor, with PWA as a companion option

## 1. Entity Framework Core Workflow

```mermaid
flowchart TD
    A[Change model or Fluent API configuration] --> B[Create migration]
    B --> C[Review generated migration]
    C --> D[Apply database update]
    D --> E[Run app and verify behavior]
    C --> F[Generate SQL script if needed]

    A1["Models and configurations<br/>BudgetTracker.Domain/Models<br/>BudgetTracker.Domain/Data/Configurations"]
    B1["dotnet ef migrations add Name<br/>project: Domain<br/>startup: Server"]
    D1["dotnet ef database update<br/>records migration in __EFMigrationsHistory"]

    A --- A1
    B --- B1
    D --- D1
```

```mermaid
sequenceDiagram
    participant Dev as Developer
    participant EF as EF Core
    participant Snap as Model Snapshot
    participant Db as SQL Server

    Dev->>EF: Change entity or Fluent API config
    Dev->>EF: dotnet ef migrations add NewChange
    EF->>Snap: Compare current model to snapshot
    EF->>Dev: Generate migration files
    Dev->>EF: dotnet ef database update
    EF->>Db: Run migration SQL
    Db->>EF: Success
```

### What gets updated

- A new migration file pair in [BudgetTracker.Domain/Migrations](../BudgetTracker.Domain/Migrations)
- [BudgetTrackerDbContextModelSnapshot.cs](../BudgetTracker.Domain/Migrations/BudgetTrackerDbContextModelSnapshot.cs)
- The database schema through `__EFMigrationsHistory`

## 2. Mobile Delivery Workflow

```mermaid
flowchart TD
    A[React + Vite web app] --> B[Responsive UI tweaks]
    B --> C{Delivery target}
    C -->|Web install| D[PWA]
    C -->|App stores| E[Capacitor]
    C -->|Both| F[PWA + Capacitor]

    D --> D1[Manifest + service worker]
    E --> E1[Native shell for iOS and Android]
    F --> F1[One frontend, two delivery modes]

    E1 --> E2[Plugins for push, camera, biometrics]
    D1 --> D2[Install from browser]
```

```mermaid
sequenceDiagram
    participant Dev as Developer
    participant Web as React/Vite Build
    participant Cap as Capacitor
    participant iOS as iOS App
    participant And as Android App

    Dev->>Web: npm run build
    Web->>Cap: Web assets in dist/
    Dev->>Cap: npx cap sync
    Cap->>iOS: Update native project
    Cap->>And: Update native project
    Dev->>iOS: Open in Xcode
    Dev->>And: Open in Android Studio
```

### Why Capacitor fits BudgetTracker

- Reuses the existing React frontend
- Keeps a single codebase for web and mobile
- Lets you add native features later through plugins
- Avoids a rewrite in React Native

### Recommended next steps

1. Make the dashboard responsive on small screens
2. Add Capacitor for app-store builds
3. Add PWA support if you want install-from-browser behavior too
