using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Specialized;
using System.Security.Cryptography.X509Certificates;
using ass.Properties;

namespace AkatsukiServerSwitcher
{
    public partial class MainForm : Form
    {
        public bool Akatsuki = false;
        public string AkatsukiIP = "51.15.228.68";  // kawata
        public string mirrorIP = "51.15.222.176";  // zxq
        public bool testConnection = false;

        public int currentVersion = 140;     // Increment this and update changelog before compiling a new update
        public int latestChangelog = 0;

        public string settingsPath = System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Kawata Server Switcher";
        public string hostsPath = Environment.GetEnvironmentVariable("windir") + "\\system32\\drivers\\etc\\hosts";

        public MainForm()
        {
            InitializeComponent();

            // Make sure hosts file exists
            if (!File.Exists(hostsPath))
                File.AppendAllText(hostsPath, "# Hosts file");

            // Create tooltips
            ToolTip OnOffTooltip = new ToolTip();
            OnOffTooltip.SetToolTip(this.switchButton, "Switch between osu! and Kawata Servers");
            ToolTip LocalRipwotTooltip = new ToolTip();
            LocalRipwotTooltip.SetToolTip(this.updateIPButton, "Get the right server IP address directly from the server.");
            ToolTip InstallCertificateTooltip = new ToolTip();
            InstallCertificateTooltip.SetToolTip(this.installCertificateButton, "Install/Remove HTTPS certificate.\nYou must have install the certificate in order to connect\nto Kawata with stable/beta/cutting edge.\nYou don't need the certificate with fallback.");

            // Create settings directory (if it doesn't exists)
            Directory.CreateDirectory(settingsPath);

            // Check if Kawata.txt exists and if not create a default one
            if (!File.Exists(settingsPath + "\\Kawata.txt"))
            {
                File.AppendAllText(settingsPath + "\\Kawata.txt", AkatsukiIP + Environment.NewLine);
                File.AppendAllText(settingsPath + "\\Kawata.txt", mirrorIP + Environment.NewLine);
                File.AppendAllText(settingsPath + "\\Kawata.txt", "true");
                File.AppendAllText(settingsPath + "\\Kawata.txt", Convert.ToString(currentVersion-1)+Environment.NewLine);
            }

            // Read Kawata.txt
            string[] AkatsukiTxt = File.ReadAllLines(settingsPath + "\\Kawata.txt");

            // If there are 4 lines, it's not corrupter or memes
            if (AkatsukiTxt.Length == 4)
            {
                // Read IP
                AkatsukiIP = AkatsukiTxt[0];
                mirrorIP = AkatsukiTxt[1];

                // Check if testConnection is bool, if yes read it, otherwise use default settings
                bool isBool;
                Boolean.TryParse(AkatsukiTxt[2], out isBool);
                if (isBool)
                    testConnection = Convert.ToBoolean(AkatsukiTxt[2]);

                // Read latest changelog
                latestChangelog = Convert.ToInt32(AkatsukiTxt[3]);
            }
            else
            {
                // Something went wrong, use default settings
            }

            // Check if we have to show a changelog
            if (latestChangelog < currentVersion)
            {
                // Show new changelog
                ChangelogForm cf = new ChangelogForm();
                cf.ShowDialog();

                // Update latest changelog
                latestChangelog = currentVersion;
            }

            // Update settings
            updateSettings();

            // Get current hosts configuration
            findServer();

            // Get certificate status and update button text
            updateCertificateButton();


            // Check if we are using old server IP
            checkOldServerIP();
        }


        private void MainForm_Shown(object sender, EventArgs e)
        {
            updateStatusLabel();
        }

        public void saveSettings()
        {
            // Save settings to Kawata.txt
            File.WriteAllText(settingsPath + "\\Kawata.txt", AkatsukiIP + Environment.NewLine);
            File.AppendAllText(settingsPath + "\\Kawata.txt", mirrorIP + Environment.NewLine);
            File.AppendAllText(settingsPath + "\\Kawata.txt", Convert.ToString(testConnection) + Environment.NewLine);
            File.AppendAllText(settingsPath + "\\Kawata.txt", Convert.ToString(latestChangelog));
        }

        public bool findServer()
        {
            // Read hosts
            string[] hostsContent = File.ReadAllLines(hostsPath);

            // Loop through all strings
            for (var i = 0; i < hostsContent.Length; i++)
            {
                // Check if current line is not empty (otherwise it throws an exception)
                if (hostsContent[i] != "")
                {
                    // Check if current line is not commented and redirects to osu.ppy.sh
                    if ((Regex.Matches(hostsContent[i], "#").Count == 0) && (Regex.Matches(hostsContent[i], "osu.ppy.sh").Count > 0))
                    {
                        // Our hosts points to Akatsuki
                        Akatsuki = true;
                        return Akatsuki;
                    }
                }
            }

            // Hosts doesn't contain any reference to osu.ppy.sh, we are not pointing to Akatsuki
            Akatsuki = false;
            return Akatsuki;
        }

        public bool updateServer()
        {
            // Check if IP is not empty and valid (I should rewrite this but idc for now)
            if (AkatsukiIP != "" && mirrorIP != "")
            {
                IPAddress type;
                if ( (IPAddress.TryParse(AkatsukiIP, out type) && type.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) && IPAddress.TryParse(mirrorIP, out type))
                {
                    // Check read only
                    if(IsFileReadOnly(hostsPath))
                        SetFileReadAccess(hostsPath, false);

                    // Read hosts
                    string[] hostsContent = File.ReadAllLines(hostsPath);

                    // Check for any osu.ppy.sh line, remove them
                    for (var i = 0; i < hostsContent.Length; i++)
                    {
                        if (Regex.Matches(hostsContent[i], "(?:osu|a|c|c1|bm6|i).ppy.sh").Count > 0)
                        {
                            // Line that points (or used to point) to osu.ppy.sh, remove it
                            hostsContent[i] = "";
                        }
                    }

                    // Empty hosts
                    try
                    {
                        File.WriteAllText(hostsPath, "");

                        // Rewrite hosts
                        for (var i = 0; i < hostsContent.Length; i++)
                        {
                            if (hostsContent[i] != "")
                            {
                                // Current line is not empty, write it
                                File.AppendAllText(hostsPath, hostsContent[i] + Environment.NewLine);
                            }
                        }

                        // Point to Akatsuki if required
                        if (Akatsuki)
                        {
                            File.AppendAllText(hostsPath, AkatsukiIP + "   osu.ppy.sh" + Environment.NewLine);
                            File.AppendAllText(hostsPath, AkatsukiIP + "   c.ppy.sh" + Environment.NewLine);
                            File.AppendAllText(hostsPath, AkatsukiIP + "   c1.ppy.sh" + Environment.NewLine);
                            File.AppendAllText(hostsPath, AkatsukiIP + "   a.ppy.sh" + Environment.NewLine);
                            File.AppendAllText(hostsPath, AkatsukiIP + "   i.ppy.sh" + Environment.NewLine);
                            File.AppendAllText(hostsPath, mirrorIP + "   bm6.ppy.sh" + Environment.NewLine);
                        }

                        return true;
                    }
                    catch
                    {
                        MessageBox.Show("Error while writing hosts file."+Environment.NewLine+"Please make sure hosts is not set to read only mode.");
                        return false;
                    }
                }
                else
                {
                    statusLabel.Text = "Invalid Kawata/Mirror IP address";
                    return false;
                }
            }
            else
            {
                statusLabel.Text = "Invalid Kawata/Mirror IP address";
                return false;
            }
        }

        public void updateStatusLabel()
        {
            // Update statusLabel based on Akatsuki variable
            statusLabel.Text = Akatsuki ? "You are playing on Kawata." + Environment.NewLine+IPTextBox.Text+" - "+MirrorIPTextBox.Text : "You are playing on osu! official server.";
            // Ayy k maron sn pigor xd
            updateJennaWarning();
        }

        public void updateJennaWarning()
        {
            if (Application.OpenForms.Count >= 1)
                if (AkatsukiIP == "127.0.0.1")
                    Application.OpenForms[0].Height = 330;
                else
                    Application.OpenForms[0].Height = 202;
        }

        public void updateSettings()
        {
            // Update textBoxes in settings group
            IPTextBox.Text = AkatsukiIP;
            MirrorIPTextBox.Text = mirrorIP;
            //testCheckBox.Checked = testConnection;
        }

        private void IPTextBox_TextChanged(object sender, EventArgs e)
        {
            // Settings: Update IP address
            AkatsukiIP = IPTextBox.Text;
        }

        private void MirrorIPTextBox_TextChanged(object sender, EventArgs e)
        {
            // Settings: Update Mirror IP address
            mirrorIP = MirrorIPTextBox.Text;
        }

        private void testCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            // Settings:  Update test connection
            //testConnection = testCheckBox.Checked;
        }

        private void switchButton_Click(object sender, EventArgs e)
        {
            // Get current hosts status, because it might have changed
            findServer();

            // Switch between Akatsuki/osu!, write hosts and update label
            Akatsuki = !Akatsuki;
            if (updateServer())
                updateStatusLabel();

            // Connection check if we are on Akatsuki
            //if (Akatsuki && testConnection)
            //    checkAkatsukiConnection();
        }

        public void checkAkatsukiConnection()
        {
            // Checks if osu.ppy.sh actually points to Akatsuki
            try
            {
                //WebClient wc = new WebClient();
                //string s = wc.DownloadString("http://osu.ppy.sh/");

                string s;
                using (WebClient client = new WebClient())
                {
                    byte[] response =
                    client.UploadValues("http://osu.ppy.sh/", new NameValueCollection()
                    {
                        { "switcher", "true" },
                    });
                    s = Encoding.UTF8.GetString(response);
                }

                if (s == "ok")
                    updateStatusLabel();    // This changes statuslabel.text to "You are playing on Kawata"
                else
                    statusLabel.Text = "Error while connecting Kawata.";
            }
            catch
            {
                // 4xx / 5xx error
                statusLabel.Text = "Error while connecting Kawata.";
            }
        }

        private void genuineButton1_Click(object sender, EventArgs e)
        {
            // Save settings and close
            saveSettings();
            Application.Exit();
        }

        private void groupBox1_Paint(object sender, PaintEventArgs e)
        {
            GroupBox box = sender as GroupBox;
            DrawGroupBox(box, e.Graphics, Color.White, Color.FromArgb(100, 100, 100));
        }

        private void DrawGroupBox(GroupBox box, Graphics g, Color textColor, Color borderColor)
        {
            if (box != null)
            {
                Brush textBrush = new SolidBrush(textColor);
                Brush borderBrush = new SolidBrush(borderColor);
                Pen borderPen = new Pen(borderBrush);
                SizeF strSize = g.MeasureString(box.Text, box.Font);
                Rectangle rect = new Rectangle(box.ClientRectangle.X,
                                               box.ClientRectangle.Y + (int)(strSize.Height / 2),
                                               box.ClientRectangle.Width - 1,
                                               box.ClientRectangle.Height - (int)(strSize.Height / 2) - 1);

                // Clear text and border
                g.Clear(this.BackColor);

                // Draw text
                g.DrawString(box.Text, box.Font, textBrush, box.Padding.Left, 0);

                // Drawing Border
                //Left
                g.DrawLine(borderPen, rect.Location, new Point(rect.X, rect.Y + rect.Height));
                //Right
                g.DrawLine(borderPen, new Point(rect.X + rect.Width, rect.Y), new Point(rect.X + rect.Width, rect.Y + rect.Height));
                //Bottom
                g.DrawLine(borderPen, new Point(rect.X, rect.Y + rect.Height), new Point(rect.X + rect.Width, rect.Y + rect.Height));
                //Top1
                g.DrawLine(borderPen, new Point(rect.X, rect.Y), new Point(rect.X + box.Padding.Left, rect.Y));
                //Top2
                g.DrawLine(borderPen, new Point(rect.X + box.Padding.Left + (int)(strSize.Width), rect.Y), new Point(rect.X + rect.Width, rect.Y));
            }
        }

      

        void updateServerIP()
        {
            try
            {
                // Get server ip from ip.kawata.pw
                WebClient client = new WebClient();
                string[] remoteIPs = client.DownloadString("http://old.kawata.pw/ips.txt").TrimEnd('\r', '\n').Split('\n');

                // Akatsuki IP
                if (AkatsukiIP != remoteIPs[0])
                {
                    IPTextBox.Text = remoteIPs[0];
                    if (updateServer())
                        updateStatusLabel();
                }

                // Mirror IP
                if (mirrorIP != remoteIPs[1])
                {
                    MirrorIPTextBox.Text = remoteIPs[1];
                    if (updateServer())
                        updateStatusLabel();
                }
            }
            catch
            {
                // Error
            }
        }


        void goToWebsite()
        {
            System.Diagnostics.Process.Start("https://kawata.pw");
        }

        void checkOldServerIP()
        {
            try
            {
                // Get old ip from ip.Akatsuki.moe
                WebClient client = new WebClient();
                var oldIPs = client.DownloadString("http://old.kawata.pw/oldips.txt").Split('\n');
                int l = oldIPs.Length;
                for (int i=0; i< l; i++)
                {
                    if (IPTextBox.Text == oldIPs[i] || MirrorIPTextBox.Text == oldIPs[i])
                    {
                        MessageBox.Show("Your IPs might be out of date. Just click the Update IP button to fix this! :)");
                        break;
                    }
                }
            }
            catch
            {
                // Error
            }
        }

        private void updateIPButton_Click(object sender, EventArgs e)
        {
            updateServerIP();
        }


        // Returns wether a file is read-only.
        public static bool IsFileReadOnly(string FileName)
        {
            // Create a new FileInfo object.
            FileInfo fInfo = new FileInfo(FileName);

            // Return the IsReadOnly property value.
            return fInfo.IsReadOnly;
        }

        // Sets the read-only value of a file.
        public static void SetFileReadAccess(string FileName, bool SetReadOnly)
        {
            // Create a new FileInfo object.
            FileInfo fInfo = new FileInfo(FileName);

            // Set the IsReadOnly property.
            fInfo.IsReadOnly = SetReadOnly;
        }

        private void updateCertificateButton(bool __installed = true, bool check = true)
        {
            bool installed = __installed;
            if (check)

            {
                X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                X509Certificate2Collection certs = store.Certificates.Find(X509FindType.FindBySubjectName, "Hangout CA", true);
                installed = certs.Count > 0 ? true : false;
            }
            if (installed)
            {
                installCertificateButton.Text = "Remove certificate";
                installCertificateButton.Font = new Font(installCertificateButton.Font.Name, installCertificateButton.Font.Size, FontStyle.Regular);
            }
            else
            {
                installCertificateButton.Text = "Install certificate";
                installCertificateButton.Font = new Font(installCertificateButton.Font.Name, installCertificateButton.Font.Size, FontStyle.Bold);
            }
        }

        private void installCertificateButton_Click(object sender, EventArgs e)
        {
            // Check and install certificate
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            X509Certificate2Collection certs = store.Certificates.Find(X509FindType.FindBySubjectName, "Hangout CA", true);

            if (certs.Count > 0)
            {
                // Certificate already installed, remove it
                DialogResult yn = MessageBox.Show("Are you sure you want to remove Kawata's HTTPS certificate?\n re's no need to remove it, you'll be able to browse both Kawata and osu!\nwithout any problem even if the certificate is installed and the switcher is off.", "Kawata certificate installer", MessageBoxButtons.YesNo);
                if (yn == DialogResult.No)
                {
                    store.Close();
                    return;
                }
                try
                {
                    foreach (X509Certificate2 cert in certs)
                        store.Remove(certs[0]);

                    updateCertificateButton(false, false);
                    MessageBox.Show("Certificate removed!", "Kawata certificate installer");
                }
                catch
                {
                    MessageBox.Show("You can't remove me hahahah.", "Kawata certificate installer");
                }
            }
            else
            {
                // Install certificate
                try
                {
                    // Save the certificate in settingsPath temporary
                    string certFilePath = settingsPath + "\\temp.crt";
                    File.WriteAllBytes(certFilePath, Resources.certificate);

                    // Get all certficates
                    X509Certificate2Collection collection = new X509Certificate2Collection();
                    collection.Import(certFilePath);

                    // Install all certificates
                    foreach (X509Certificate2 cert in collection)
                        store.Add(cert);

                    updateCertificateButton(true, false);
                    MessageBox.Show("Certificate installed! Try connecting to Kawata with beta/stable/cutting edge", "Kawata certificate installer");

                    // Delete temp certificate file
                    File.Delete(certFilePath);
                }
                catch
                {
                    MessageBox.Show("Error while installing certificate.", "Kawata certificate installer");
                }
            }

            store.Close();
        }

        private void localButton_Click(object sender, EventArgs e)
        {
            goToWebsite();
        }

        private void statusLabel_Click(object sender, EventArgs e)
        {

        }

        private void genuineTheme1_Click(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}
