using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer; // <-- Aquí está la solución

namespace AutoClickerTray
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Iniciamos la aplicación sin un formulario, usando nuestro propio Contexto
            Application.Run(new TrayApplicationContext());
        }
    }

    public class TrayApplicationContext : ApplicationContext
    {
        // Importamos la función nativa de Windows para simular el clic del ratón
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);
        
        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;

        private NotifyIcon trayIcon;
        private Timer clickTimer;
        private Timer displayTimer;
        private ToolStripMenuItem pauseMenuItem;
        private CursorCountdownOverlay countdownOverlay;
        private readonly Stopwatch countdownStopwatch = new Stopwatch();
        private bool isPaused = false;

        public TrayApplicationContext()
        {
            // 1. Configurar el menú del clic derecho
            pauseMenuItem = new ToolStripMenuItem("Pausar", null, Pause_Click);
            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem("Cerrar", null, Exit_Click);

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add(pauseMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator()); // Línea separadora
            contextMenu.Items.Add(exitMenuItem);

            // 2. Configurar el ícono en la barra de tareas
            trayIcon = new NotifyIcon()
            {
                Icon = LoadTrayIcon(),
                ContextMenuStrip = contextMenu,
                Visible = true,
                Text = "AutoClicker (Activo)" 
            };
            
            // 3. Configurar el temporizador (1 minuto)
            clickTimer = new Timer();
            clickTimer.Interval = 60000; // 60,000 milisegundos = 1 minuto
            clickTimer.Tick += ClickTimer_Tick;
            clickTimer.Start();

            countdownOverlay = new CursorCountdownOverlay();
            countdownOverlay.Show();
            countdownStopwatch.Start();

            displayTimer = new Timer { Interval = 100 };
            displayTimer.Tick += DisplayTimer_Tick;
            displayTimer.Start();
            UpdateCountdownDisplay();
        }

        private static Icon LoadTrayIcon()
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "icono.ico");

            if (File.Exists(iconPath))
            {
                return new Icon(iconPath);
            }

            try
            {
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        // Evento que se ejecuta cada vez que pasa 1 minuto
        private void ClickTimer_Tick(object? sender, EventArgs e)
        {
            if (!isPaused)
            {
                // Simula el botón izquierdo del ratón (presionar y soltar)
                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                countdownStopwatch.Restart();
            }
        }

        private void DisplayTimer_Tick(object? sender, EventArgs e)
        {
            UpdateCountdownDisplay();
        }

        private void UpdateCountdownDisplay()
        {
            int remainingSeconds = Math.Max(0, 60 - (int)Math.Ceiling(countdownStopwatch.Elapsed.TotalSeconds));
            countdownOverlay.UpdateCountdown(remainingSeconds, Cursor.Position);
        }

        // Lógica para el botón Pausar/Reanudar
        private void Pause_Click(object? sender, EventArgs e)
        {
            isPaused = !isPaused;

            if (isPaused)
            {
                pauseMenuItem.Text = "Reanudar";
                trayIcon.Text = "AutoClicker (Pausado)";
                clickTimer.Stop(); 
                displayTimer.Stop();
                countdownStopwatch.Stop();
                countdownOverlay.Hide();
            }
            else
            {
                pauseMenuItem.Text = "Pausar";
                trayIcon.Text = "AutoClicker (Activo)";
                clickTimer.Start();
                countdownStopwatch.Restart();
                countdownOverlay.Show();
                displayTimer.Start();
                UpdateCountdownDisplay();
            }
        }

        // Lógica para cerrar la aplicación
        private void Exit_Click(object? sender, EventArgs e)
        {
            // Hay que ocultar el ícono manualmente antes de cerrar
            trayIcon.Visible = false;
            clickTimer.Dispose();
            displayTimer.Dispose();
            countdownOverlay.Dispose();
            trayIcon.Dispose();
            Application.Exit();
        }
    }

    internal sealed class CursorCountdownOverlay : Form
    {
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private string displayedText = string.Empty;

        public CursorCountdownOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Opacity = 0.95;
            BackColor = Color.FromArgb(35, 35, 35);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9, FontStyle.Bold);
            Size = new Size(48, 26);
            DoubleBuffered = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;
                return parameters;
            }
        }

        public void UpdateCountdown(int seconds, Point cursorPosition)
        {
            string text = $"{seconds}s";
            if (displayedText != text)
            {
                displayedText = text;
                Invalidate();
            }

            Rectangle workingArea = Screen.FromPoint(cursorPosition).WorkingArea;
            int x = Math.Min(cursorPosition.X + 18, workingArea.Right - Width);
            int y = Math.Min(cursorPosition.Y + 18, workingArea.Bottom - Height);
            Location = new Point(Math.Max(workingArea.Left, x), Math.Max(workingArea.Top, y));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(BackColor);
            TextRenderer.DrawText(
                e.Graphics,
                displayedText,
                Font,
                ClientRectangle,
                ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}