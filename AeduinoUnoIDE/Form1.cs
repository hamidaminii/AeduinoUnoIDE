using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

namespace AeduinoUnoIDE
{
    public partial class Form1 : Form
    {
        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private const string ArduinoCliPath = @"G:\Arduino_CLI\arduino-cli.exe";



        private readonly Timer portTimer = new Timer();

        public Form1()
        {
            InitializeComponent();

            //btnCompile.Click += btnCompile_Click;
            //btnRun.Enabled = false;

            cmbPort.DropDownStyle = ComboBoxStyle.DropDownList;

            portTimer.Interval = 2000;
            portTimer.Tick += (sender, e) => RefreshPorts();

            RefreshPorts();
            portTimer.Start();

            FormClosed += (sender, e) => portTimer.Dispose();

        }

        private void RefreshPorts()
        {
            if (cmbPort.DroppedDown)
                return;

            string[] ports = SerialPort.GetPortNames();
            Array.Sort(ports, StringComparer.OrdinalIgnoreCase);

            bool unchanged = cmbPort.Items.Count == ports.Length;
            for (int i = 0; unchanged && i < ports.Length; i++)
                unchanged = string.Equals(
                    cmbPort.Items[i].ToString(),
                    ports[i],
                    StringComparison.OrdinalIgnoreCase);

            if (unchanged)
                return;

            string selectedPort = cmbPort.SelectedItem as string;

            cmbPort.BeginUpdate();
            try
            {
                cmbPort.Items.Clear();
                cmbPort.Items.AddRange(ports);

                int previousIndex = Array.FindIndex(
                    ports,
                    port => string.Equals(
                        port,
                        selectedPort,
                        StringComparison.OrdinalIgnoreCase));

                if (previousIndex >= 0)
                    cmbPort.SelectedIndex = previousIndex;
                else if (selectedPort == null && ports.Length == 1)
                    cmbPort.SelectedIndex = 0;
                else
                    cmbPort.SelectedIndex = -1;
            }
            finally
            {
                cmbPort.EndUpdate();
            }

        }

        //private void btnCompile_Click(object sender, EventArgs e)
        //{

        //}

        //private void btnRun_Click(object sender, EventArgs e)
        //{

        //}

        //***********************************************************

        private const string BoardFqbn = "arduino:avr:uno"; // Arduino Uno R3

        private static readonly string SketchDirectory =
            Path.Combine(Path.GetTempPath(), "ArduinoServoEditor", "ServoSketch");

        private static readonly string BuildDirectory =
            Path.Combine(Path.GetTempPath(), "ArduinoServoEditor", "build");

        private sealed class CliResult
        {
            public int ExitCode;
            public string Output;
            public string Error;
        }

        private async void btnCompile_Click(object sender, EventArgs e)
        {

            

            if (string.IsNullOrWhiteSpace(rtbCode.Text))
            {
                MessageBox.Show(this, "ابتدا کد آردوینو را وارد کن.");
                return;
            }

            btnCompile.Enabled = false;
            btnRun.Enabled = false;

            try
            {
                CliResult result = await CompileSketchAsync();

                if (result.ExitCode == 0)
                {
                    MessageBox.Show(
                        this,
                        "کامپایل با موفقیت انجام شد.",
                        "Compile",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    ShowCliError("کامپایل ناموفق بود.", result);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "خطا در اجرای Arduino CLI",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                btnCompile.Enabled = true;
                btnRun.Enabled = true;
            }
        }

        private async Task<CliResult> CompileSketchAsync()
        {
            Directory.CreateDirectory(SketchDirectory);
            Directory.CreateDirectory(BuildDirectory);

            string sketchFile = Path.Combine(SketchDirectory, "ServoSketch.ino");

            File.WriteAllText(
                sketchFile,
                rtbCode.Text,
                new UTF8Encoding(false));

            string arguments =
                "compile --fqbn " + BoardFqbn +
                " --build-path " + Quote(BuildDirectory) +
                " " + Quote(SketchDirectory);

            return await RunCliAsync(arguments);
        }

        private async Task<CliResult> RunCliAsync(string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ArduinoCliPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                if (!process.Start())
                    throw new InvalidOperationException("Arduino CLI اجرا نشد.");

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                await Task.Run(() => process.WaitForExit());

                return new CliResult
                {
                    ExitCode = process.ExitCode,
                    Output = await outputTask,
                    Error = await errorTask
                };
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }

        private void ShowCliError(string title, CliResult result)
        {
            string details = (result.Error + Environment.NewLine + result.Output).Trim();

            if (string.IsNullOrWhiteSpace(details))
                details = "Arduino CLI با کد خطای " + result.ExitCode + " متوقف شد.";

            // MessageBox برای متن‌های خیلی بلند مناسب نیست.
            if (details.Length > 4000)
                details = details.Substring(details.Length - 4000);

            MessageBox.Show(
                this,
                title + Environment.NewLine + Environment.NewLine + details,
                "Arduino CLI",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }



        //***********************************************************


        private async void btnRun_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(rtbCode.Text))
            {
                MessageBox.Show(this, "ابتدا کد آردوینو را وارد کن.");
                return;
            }

            string port = cmbPort.SelectedItem as string;

            if (string.IsNullOrWhiteSpace(port))
            {
                MessageBox.Show(this, "ابتدا پورت برد را انتخاب کن.");
                return;
            }

            btnRun.Enabled = false;
            btnCompile.Enabled = false;
            cmbPort.Enabled = false;
            rtbCode.Enabled = false;
            portTimer.Stop();

            try
            {
                CliResult compileResult = await CompileSketchAsync();

                if (compileResult.ExitCode != 0)
                {
                    ShowCliError("کامپایل ناموفق بود؛ آپلود انجام نشد.", compileResult);
                    return;
                }

                string arguments =
                    "upload --fqbn " + BoardFqbn +
                    " --port " + Quote(port) +
                    " --build-path " + Quote(BuildDirectory) +
                    " " + Quote(SketchDirectory);

                CliResult uploadResult = await RunCliAsync(arguments);

                if (uploadResult.ExitCode != 0)
                {
                    ShowCliError("آپلود ناموفق بود.", uploadResult);
                    return;
                }

                MessageBox.Show(
                    this,
                    "آپلود موفق بود؛ برنامه روی برد اجرا می‌شود.",
                    "Run",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "خطا در اجرای Arduino CLI",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                portTimer.Start();
                rtbCode.Enabled = true;
                cmbPort.Enabled = true;
                btnCompile.Enabled = true;
                btnRun.Enabled = true;
            }
        }













    }
}
