using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using CredentialScanner.Models;
using CredentialScanner.Services;
using CredentialScanner.Utils;

namespace CredentialScanner
{
    public partial class MainForm : Form
    {
        private ConfigService _config;
        private CredentialScannerService _scannerService;

        // Controles de la bandeja
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;
        private ToolStripMenuItem menuShow;
        private ToolStripMenuItem menuStart;
        private ToolStripMenuItem menuStop;
        private ToolStripMenuItem menuViewLog;
        private ToolStripMenuItem menuExit;
        private ToolStripSeparator menuSeparator1;
        private ToolStripSeparator menuSeparator2;
        private ToolStripMenuItem menuStatus;

        // Controles de la ventana
        private Panel panelHeader;
        private Label lblTitle;
        private Label lblSubTitle;
        private Panel panelInfo;
        private Label lblStatusText;
        private Label lblStatusValue;
        private Label lblComputerText;
        private Label lblComputerValue;
        private Label lblUserText;
        private Label lblUserValue;
        private Label lblVersionText;
        private Label lblVersionValue;
        private Panel panelFooter;
        private Label lblFooter;

        private bool isMonitoring = false;
        private int totalFindingsCount = 0;
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Timer statusTimer;
        private System.Windows.Forms.Timer uiUpdateTimer;
        private string _computerName;
        private string _userName;
        private string _appVersion = "1.0.0";
        private List<string> _scanPaths = new();
        private List<FileSystemWatcher> _watchers = new();
        private Dictionary<string, DateTime> _processingQueue = new();
        private object _lockObject = new();

        public MainForm()
        {
            _computerName = Environment.MachineName;
            _userName = Environment.UserName;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            InitializeComponent();

            Icon appIcon = LoadCustomIcon();
            this.Icon = appIcon;
            if (notifyIcon != null)
                notifyIcon.Icon = appIcon;

            lblComputerValue.Text = _computerName;
            lblUserValue.Text = _userName;
            lblVersionValue.Text = _appVersion;

            LoadConfiguration();
            InicializarServicios();
            SetupSystemTray();
            SetupTimers();

            if (_scanPaths.Count > 0)
            {
                StartMonitoring();
            }

            SystemEvents.SessionEnding += SystemEvents_SessionEnding;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void LoadConfiguration()
        {
            string rootSetup = ConfigurationManager.AppSettings["rootSetup"];
            string nameSetup = ConfigurationManager.AppSettings["nameSetup"];

            if (string.IsNullOrEmpty(rootSetup) || string.IsNullOrEmpty(nameSetup))
            {
                string appPath = Path.GetDirectoryName(Application.ExecutablePath);
                rootSetup = appPath;
                nameSetup = "setup.dat";
            }

            string setupPath = Path.Combine(rootSetup, nameSetup);

            if (!File.Exists(setupPath))
            {
                MessageBox.Show($"No se encuentra el archivo de configuración:\n{setupPath}\n\n" +
                    "Asegúrate de que el archivo setup.dat esté en la misma carpeta que el ejecutable.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            _config = new ConfigService(setupPath);

            Logger.Initialize(_config.LogPath, _config.LogFileName);

            _scanPaths = _config.GetScanPaths();

            if (_scanPaths.Count == 0)
            {
                Logger.Warning("No se configuraron rutas de monitoreo. Revisa [MONITOR_SETTINGS] en setup.dat",
                    _computerName, _userName);
            }
        }

        private void InicializarServicios()
        {
            _scannerService = new CredentialScannerService(_config, _computerName, _userName);
        }

        private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            if (e.Reason == SessionEndReasons.SystemShutdown)
            {
                Logger.Info("Sistema en proceso de apagado", _computerName, _userName);
                System.Threading.Thread.Sleep(1000);
            }
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            statusTimer = new System.Windows.Forms.Timer(components);
            uiUpdateTimer = new System.Windows.Forms.Timer(components);

            panelHeader = new Panel();
            lblTitle = new Label();
            lblSubTitle = new Label();
            panelInfo = new Panel();
            lblStatusText = new Label();
            lblStatusValue = new Label();
            lblComputerText = new Label();
            lblComputerValue = new Label();
            lblUserText = new Label();
            lblUserValue = new Label();
            lblVersionText = new Label();
            lblVersionValue = new Label();
            panelFooter = new Panel();
            lblFooter = new Label();

            panelHeader.BackColor = Color.FromArgb(0, 51, 102);
            panelHeader.Dock = DockStyle.Top;
            panelHeader.Size = new Size(500, 55);

            lblTitle.Text = "MIATECH SENTINEL";
            lblTitle.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(15, 8);
            lblTitle.Size = new Size(470, 25);

            lblSubTitle.Text = "Monitor de Credenciales y Seguridad";
            lblSubTitle.Font = new Font("Segoe UI", 8.5F);
            lblSubTitle.ForeColor = Color.LightGray;
            lblSubTitle.Location = new Point(15, 32);
            lblSubTitle.Size = new Size(470, 16);

            panelHeader.Controls.Add(lblTitle);
            panelHeader.Controls.Add(lblSubTitle);

            panelInfo.BackColor = Color.White;
            panelInfo.Dock = DockStyle.Fill;
            panelInfo.Padding = new Padding(10, 8, 10, 8);

            lblStatusText.Text = "Estado:";
            lblStatusText.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblStatusText.ForeColor = Color.FromArgb(64, 64, 64);
            lblStatusText.Location = new Point(12, 10);
            lblStatusText.Size = new Size(75, 20);

            lblStatusValue.Text = "Detenido";
            lblStatusValue.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblStatusValue.ForeColor = Color.Gray;
            lblStatusValue.Location = new Point(100, 10);
            lblStatusValue.Size = new Size(300, 20);

            lblComputerText.Text = "Equipo:";
            lblComputerText.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblComputerText.ForeColor = Color.FromArgb(64, 64, 64);
            lblComputerText.Location = new Point(12, 35);
            lblComputerText.Size = new Size(75, 20);

            lblComputerValue.Text = "";
            lblComputerValue.Font = new Font("Segoe UI", 9.5F);
            lblComputerValue.ForeColor = Color.FromArgb(64, 64, 64);
            lblComputerValue.Location = new Point(100, 35);
            lblComputerValue.Size = new Size(300, 20);

            lblUserText.Text = "Usuario:";
            lblUserText.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            lblUserText.ForeColor = Color.FromArgb(64, 64, 64);
            lblUserText.Location = new Point(12, 60);
            lblUserText.Size = new Size(75, 20);

            lblUserValue.Text = "";
            lblUserValue.Font = new Font("Segoe UI", 9.5F);
            lblUserValue.ForeColor = Color.FromArgb(64, 64, 64);
            lblUserValue.Location = new Point(100, 60);
            lblUserValue.Size = new Size(300, 20);

            Panel line1 = new Panel();
            line1.BackColor = Color.FromArgb(200, 200, 200);
            line1.Location = new Point(12, 88);
            line1.Size = new Size(470, 1);

            lblVersionText.Text = "Versión:";
            lblVersionText.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblVersionText.ForeColor = Color.FromArgb(64, 64, 64);
            lblVersionText.Location = new Point(12, 98);
            lblVersionText.Size = new Size(75, 18);

            lblVersionValue.Text = "";
            lblVersionValue.Font = new Font("Segoe UI", 8.5F);
            lblVersionValue.ForeColor = Color.FromArgb(64, 64, 64);
            lblVersionValue.Location = new Point(100, 98);
            lblVersionValue.Size = new Size(300, 18);

            panelInfo.Controls.Add(lblStatusText);
            panelInfo.Controls.Add(lblStatusValue);
            panelInfo.Controls.Add(lblComputerText);
            panelInfo.Controls.Add(lblComputerValue);
            panelInfo.Controls.Add(lblUserText);
            panelInfo.Controls.Add(lblUserValue);
            panelInfo.Controls.Add(line1);
            panelInfo.Controls.Add(lblVersionText);
            panelInfo.Controls.Add(lblVersionValue);

            panelFooter.BackColor = Color.FromArgb(240, 240, 240);
            panelFooter.Dock = DockStyle.Bottom;
            panelFooter.Size = new Size(500, 28);

            lblFooter.Text = "© 2026 Miatech - Todos los derechos reservados";
            lblFooter.Font = new Font("Segoe UI", 7.5F);
            lblFooter.ForeColor = Color.Gray;
            lblFooter.TextAlign = ContentAlignment.MiddleRight; //MiddleRight
            lblFooter.Location = new Point(0, 5);
            lblFooter.Size = new Size(250, 16);

            panelFooter.Controls.Add(lblFooter);

            this.ClientSize = new Size(300, 200);
            this.MinimumSize = new Size(200, 150);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Controls.Add(panelInfo);
            this.Controls.Add(panelHeader);
            this.Controls.Add(panelFooter);
            this.Name = "MainForm";
            this.ShowInTaskbar = false;
            this.Text = "Miatech Sentinel";
            this.WindowState = FormWindowState.Minimized;
            this.FormClosing += MainForm_FormClosing;
            this.Load += MainForm_Load;

            uiUpdateTimer.Interval = 1000;
            uiUpdateTimer.Tick += UiUpdateTimer_Tick;
            uiUpdateTimer.Start();

            ResumeLayout(false);
        }

        private void SetupSystemTray()
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = LoadCustomIcon();
            notifyIcon.Text = "Miatech Sentinel";
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += NotifyIcon_DoubleClick;

            contextMenu = new ContextMenuStrip();

            menuStatus = new ToolStripMenuItem();
            menuStatus.Text = "Estado: Detenido";
            menuStatus.Enabled = false;
            menuStatus.ForeColor = Color.Gray;
            contextMenu.Items.Add(menuStatus);

            menuSeparator1 = new ToolStripSeparator();
            contextMenu.Items.Add(menuSeparator1);

            menuStart = new ToolStripMenuItem();
            menuStart.Text = "Iniciar Monitoreo";
            menuStart.Click += MenuStart_Click;
            contextMenu.Items.Add(menuStart);

            menuStop = new ToolStripMenuItem();
            menuStop.Text = "Detener Monitoreo";
            menuStop.Enabled = false;
            menuStop.Click += MenuStop_Click;
            contextMenu.Items.Add(menuStop);

            menuSeparator2 = new ToolStripSeparator();
            contextMenu.Items.Add(menuSeparator2);

            menuShow = new ToolStripMenuItem();
            menuShow.Text = "Mostrar Ventana";
            menuShow.Click += MenuShow_Click;
            contextMenu.Items.Add(menuShow);

            menuExit = new ToolStripMenuItem();
            menuExit.Text = "Salir";
            menuExit.Click += MenuExit_Click;
            contextMenu.Items.Add(menuExit);

            notifyIcon.ContextMenuStrip = contextMenu;

            if (_scanPaths.Count == 0)
            {
                notifyIcon.ShowBalloonTip(5000, "Miatech Sentinel",
                    "No hay rutas configuradas.\nRevisa setup.dat → [MONITOR_SETTINGS]",
                    ToolTipIcon.Warning);
            }
            else
            {
                notifyIcon.ShowBalloonTip(3000, "Miatech Sentinel",
                    "Aplicación iniciada correctamente.\nHaz clic derecho para ver opciones.",
                    ToolTipIcon.Info);
            }
        }

        private Icon LoadCustomIcon()
        {
            try
            {
                string iconPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Ico2.ico");
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private void SetupTimers()
        {
            statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 1000;
            statusTimer.Tick += StatusTimer_Tick;
            statusTimer.Start();
        }

        #region Eventos de UI

        private void MainForm_Load(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            UpdateUI();
        }

        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            ShowWindow();
        }

        private void ShowWindow()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.BringToFront();
            UpdateUI();
        }

        private void MenuStart_Click(object sender, EventArgs e)
        {
            if (_scanPaths.Count == 0)
            {
                Logger.Warning("Intento de inicio sin rutas configuradas", _computerName, _userName);
                notifyIcon.ShowBalloonTip(3000, "Error",
                    "No hay rutas configuradas para monitorear.\nRevisa el archivo setup.dat",
                    ToolTipIcon.Error);
                return;
            }

            StartMonitoring();
        }

        private void MenuStop_Click(object sender, EventArgs e)
        {
            if (!PedirTokenAutorizacion("Detener Monitoreo"))
            {
                Logger.Warning("Intento de detener monitoreo sin autorización", _computerName, _userName);
                notifyIcon.ShowBalloonTip(3000, "Miatech Sentinel",
                    "No puede detener el monitoreo sin autorización.",
                    ToolTipIcon.Warning);
                return;
            }

            Logger.Info("Detención de monitoreo autorizada con token", _computerName, _userName);
            StopMonitoring();
        }

        private void MenuExit_Click(object sender, EventArgs e)
        {
            if (!PedirTokenAutorizacion("Cerrar la Aplicación"))
            {
                Logger.Warning("Intento de salida sin autorización", _computerName, _userName);
                notifyIcon.ShowBalloonTip(3000, "Miatech Sentinel",
                    "No puede cerrar la aplicación sin autorización.",
                    ToolTipIcon.Warning);

                if (!isMonitoring && _scanPaths.Count > 0)
                {
                    StartMonitoring();
                }
                return;
            }

            Logger.Info("Salida autorizada con token", _computerName, _userName);
            CloseApp();
        }

        private void MenuShow_Click(object sender, EventArgs e)
        {
            ShowWindow();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                this.ShowInTaskbar = false;
                this.WindowState = FormWindowState.Minimized;
                return;
            }

            StopMonitoring();
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
        }

        #endregion

        #region Token de Autorización

        private bool PedirTokenAutorizacion(string accion)
        {
            using (var form = new Form())
            {
                form.Text = "Autorización Requerida";
                form.Size = new Size(420, 180);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.TopMost = true;

                var lblMensaje = new Label();
                lblMensaje.Text = $"Ingrese el token de administrador";
                lblMensaje.Location = new Point(20, 15);
                lblMensaje.Size = new Size(370, 40);
                lblMensaje.Font = new Font("Segoe UI", 9F);
                lblMensaje.ForeColor = Color.FromArgb(64, 64, 64);

                var txtToken = new TextBox();
                txtToken.Location = new Point(20, 60);
                txtToken.Size = new Size(370, 25);
                txtToken.Font = new Font("Segoe UI", 10F);
                txtToken.UseSystemPasswordChar = true;

                var btnAceptar = new Button();
                btnAceptar.Text = "Aceptar";
                btnAceptar.Location = new Point(200, 100);
                btnAceptar.Size = new Size(90, 30);
                btnAceptar.DialogResult = DialogResult.OK;
                btnAceptar.BackColor = Color.Transparent;
                btnAceptar.FlatStyle = FlatStyle.Flat;

                var btnCancelar = new Button();
                btnCancelar.Text = "Cancelar";
                btnCancelar.Location = new Point(300, 100);
                btnCancelar.Size = new Size(90, 30);
                btnCancelar.DialogResult = DialogResult.Cancel;
                btnCancelar.BackColor = Color.Transparent;
                btnCancelar.FlatStyle = FlatStyle.Flat;

                form.Controls.Add(lblMensaje);
                form.Controls.Add(txtToken);
                form.Controls.Add(btnAceptar);
                form.Controls.Add(btnCancelar);

                form.AcceptButton = btnAceptar;
                form.CancelButton = btnCancelar;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    string tokenIngresado = txtToken.Text.Trim();
                    string tokenCorrecto = _config.GetValue("SECURITY_SETTINGS", "EXIT_TOKEN", "");

                    if (string.IsNullOrEmpty(tokenCorrecto))
                    {
                        Logger.Warning("No hay token configurado en setup.dat", _computerName, _userName);
                        return false;
                    }

                    if (tokenIngresado == tokenCorrecto)
                    {
                        return true;
                    }

                    MessageBox.Show("Token incorrecto. Acción denegada.",
                        "Autorización Denegada",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Logger.Warning($"Token incorrecto para: {accion}", _computerName, _userName);
                    return false;
                }

                return false;
            }
        }

        #endregion

        #region Monitoreo

        private void StartMonitoring()
        {
            if (isMonitoring) return;

            try
            {
                isMonitoring = true;
                totalFindingsCount = 0;

                Logger.Info("Monitoreo iniciado", _computerName, _userName);

                foreach (var path in _scanPaths)
                {
                    try
                    {
                        var watcher = new FileSystemWatcher
                        {
                            Path = path,
                            IncludeSubdirectories = true,
                            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime |
                                          NotifyFilters.FileName | NotifyFilters.DirectoryName |
                                          NotifyFilters.Size,
                            Filter = "*.*",
                            InternalBufferSize = 65536 * 4
                        };

                        watcher.Created += OnFileCreated;
                        watcher.Changed += OnFileChanged;
                        watcher.Renamed += OnFileRenamed;
                        watcher.Error += OnWatcherError;

                        watcher.EnableRaisingEvents = true;
                        _watchers.Add(watcher);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error en watcher ({path}): {ex.Message}", _computerName, _userName);
                    }
                }

                if (menuStart != null) menuStart.Enabled = false;
                if (menuStop != null) menuStop.Enabled = true;
                if (menuStatus != null)
                {
                    menuStatus.Text = "Estado: Activo";
                    menuStatus.ForeColor = Color.Green;
                }
                if (notifyIcon != null) notifyIcon.Text = "Miatech Sentinel - Activo";

                UpdateUI();

                notifyIcon.ShowBalloonTip(2000, "Miatech Sentinel",
                    $"Monitoreo iniciado",
                    ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                isMonitoring = false;
                Logger.Error($"Error al iniciar: {ex.Message}", _computerName, _userName);
                MessageBox.Show($"Error al iniciar monitoreo: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopMonitoring()
        {
            if (!isMonitoring) return;

            Logger.Info("Monitoreo detenido", _computerName, _userName);

            foreach (var watcher in _watchers)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch { }
            }
            _watchers.Clear();
            isMonitoring = false;

            if (menuStart != null) menuStart.Enabled = true;
            if (menuStop != null) menuStop.Enabled = false;
            if (menuStatus != null)
            {
                menuStatus.Text = "Estado: Detenido";
                menuStatus.ForeColor = Color.Gray;
            }
            if (notifyIcon != null) notifyIcon.Text = "Miatech Sentinel - Detenido";

            UpdateUI();
        }

        private void CloseApp()
        {
            Logger.Info("Aplicación cerrada por autorización de administrador", _computerName, _userName);
            StopMonitoring();
            if (notifyIcon != null)
            {
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            Application.Exit();
        }

        #endregion

        #region Actualizar UI

        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(UpdateUI));
                return;
            }

            if (isMonitoring)
            {
                lblStatusValue.Text = "Activo";
                lblStatusValue.ForeColor = Color.Green;
                this.Text = "Miatech Sentinel";
            }
            else
            {
                lblStatusValue.Text = "Detenido";
                lblStatusValue.ForeColor = Color.Gray;
                this.Text = "Miatech Sentinel";
            }
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            if (isMonitoring && menuStatus != null)
            {
                menuStatus.Text = "Estado: Activo";
                menuStatus.ForeColor = Color.Green;
            }
        }

        #endregion

        #region Eventos de Archivos

        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Deleted) return;
            if (_scannerService.ShouldExcludeFile(e.FullPath)) return;
            await ProcessFileAsync(e.FullPath);
        }

        private async void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Deleted) return;
            if (_scannerService.ShouldExcludeFile(e.FullPath)) return;
            await ProcessFileAsync(e.FullPath);
        }

        private async void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (_scannerService.ShouldExcludeFile(e.FullPath)) return;
            await ProcessFileAsync(e.FullPath);
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            Logger.Error($"Watcher error: {e.GetException().Message}", _computerName, _userName);
        }

        private async Task ProcessFileAsync(string filePath)
        {
            lock (_lockObject)
            {
                if (_processingQueue.TryGetValue(filePath, out var lastProcessed) &&
                    (DateTime.Now - lastProcessed).TotalSeconds < 2)
                    return;
                _processingQueue[filePath] = DateTime.Now;
            }

            try
            {
                await _scannerService.WaitForFileReady(filePath);

                if (_scannerService.ShouldExcludeFile(filePath)) return;

                var findings = await _scannerService.AnalyzeFileAsync(filePath);

                if (findings.Any())
                {
                    totalFindingsCount += findings.Count;

                    string compName = _config.LogIncludeHostInfo ? _computerName : "";
                    string usrName = _config.LogIncludeHostInfo ? _userName : "";

                    foreach (var finding in findings)
                    {
                        string secretLimpio = LimpiarSecret(finding.Secret);

                        Logger.Info(
                            $"[{finding.Severity}] {filePath} | " +
                            $"Línea {finding.LineNumber}: {secretLimpio} | " +
                            $"Regla: {finding.RuleId} | " +
                            $"Riesgo: {finding.Score} pts;",
                            compName, usrName);
                    }

                    UpdateUI();

                    notifyIcon.ShowBalloonTip(5000, "¡CREDENCIALES ENCONTRADAS!",
                        $"Se encontraron {findings.Count} credenciales en:\n{Path.GetFileName(filePath)}",
                        ToolTipIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error procesando {Path.GetFileName(filePath)}: {ex.Message}", _computerName, _userName);
            }
            finally
            {
                lock (_lockObject)
                {
                    _processingQueue.Remove(filePath);
                }
            }
        }
        private string LimpiarSecret(string secret)
        {
            if (string.IsNullOrEmpty(secret)) return string.Empty;

            secret = secret.Replace("\\", "");
            secret = secret.Replace("\r", "").Replace("\n", "");
            secret = secret.Replace("\t", " ");

            while (secret.Contains("  "))
                secret = secret.Replace("  ", " ");

            secret = secret.Trim();

            if (secret.Length > 60)
                secret = secret.Substring(0, 60) + "...";

            return secret;
        }
        #endregion
    }
}