# Miatech Sentinel 🛡️

**Miatech Sentinel** es un sistema empresarial DLP (Data Loss Prevention) y de monitoreo en tiempo real diseñado para detectar credenciales, tokens, llaves API y contraseñas guardadas en archivos dentro de estaciones de trabajo Windows.

---

## 🏗️ Arquitectura del Sistema

```text
┌─────────────────────────────────────────────────────────────┐
│                 DASHBOARD WEB (React + Vite)                │
│                                                             │
│   • Monitoreo de Equipos en Tiempo Real                     │
│   • Credenciales Detectadas & Alertas Críticas              │
│   • Historial de Eventos & Auditoría                        │
│   • Firebase Realtime Subscriptions (onSnapshot)            │
└─────────────────────────────▲───────────────────────────────┘
                              │
                              │ Realtime / Auth
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     FIREBASE CLOUD                          │
│                                                             │
│   • Cloud Firestore: Colecciones `agents`, `findings`, `events`
│   • Firebase Auth: Acceso seguro Admin / Auditor            │
└─────────────────────────────▲───────────────────────────────┘
                              │
                              │ HTTPS REST API
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              AGENTE DE ESCRITORIO (C# .NET)                 │
│                 Ubicación: `Robot-EventWin`                 │
│                                                             │
│   • FileSystemWatcher en tiempo real                        │
│   • Escáner de 16 Reglas Regex                              │
│   • Notificaciones en Bandeja del Sistema (System Tray)     │
│   • Envío automático de Heartbeats y Hallazgos              │
└─────────────────────────────────────────────────────────────┘
```

---

## 📦 Estructura del Proyecto

- `Robot-EventWin/`: Aplicación de escritorio C# .NET Windows Forms (Agente).
  - `Services/FirebaseApiClient.cs`: Conexión HTTP resiliente con Firebase Firestore.
  - `Services/CredentialScannerService.cs`: Motor de detección Regex.
  - `Forms/MainForm.cs`: Control en segundo plano, System Tray Icon y Token de salida.
- `dashboard/`: Panel de administración Web en **React** + **Vite**.
  - `src/services/firebaseService.js`: Subscripciones Firestore y datos mock/demo.
  - `src/pages/`: Overview, Agents, Findings, Events, Login.

---

## 🚀 Guía de Inicio Rápido

### 1. Iniciar el Agente C# Windows (`Robot-EventWin`)
1. Abre la solución `Robot-EventWin/Miatech Sentinel.csproj` en tu IDE o ejecuta:
   ```bash
   cd Robot-EventWin
   dotnet run
   ```
2. Asegúrate de tener el archivo `setup.dat` en la misma carpeta del ejecutable o configurado en `app.config`.

### 2. Iniciar el Dashboard Web (React + Vite)
```bash
cd dashboard
npm install
npm run dev
```
Accede al panel en `http://localhost:3000`.

---

## 🔐 Seguridad y Auditoría

- **Token de Administrador**: El agente C# requiere autorización por token para detener o cerrar la aplicación.
- **Detección de Kill & Intentos de Salida**: Registra eventos `unauthorized_attempt` ante intentos no autorizados.
- **Protección de Privacidad**: Solo se analizan modificaciones instantáneas de archivos autorizados.
