using DBFAInstaller.enums;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DBFAInstaller
{
    public partial class Form1 : Form
    {

        private int index = 5;

        private GameType gameType = GameType.BFG;
        private CPUArch cpuArch = CPUArch.x64;
        public Form1()
        {
            InitializeComponent();
            this.setupInstaller();
        }

        private void setupInstaller()
        {
            updateButtons();
            switch(gameType)
            {
                case GameType.BFG:
                    radioButton1.Checked = true;
                    break;
                case GameType.NEW:
                    radioButton2.Checked = true;
                    break;
                case GameType.CLASSIC:
                    radioButton3.Checked = true;
                    break;
            }

            switch(cpuArch)
            {
                case CPUArch.x64:
                    radioButton5.Checked = true;
                    break;
                case CPUArch.x86:
                    radioButton4.Checked = true;
                    break;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                radioButton4.Visible = false;
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            changePanel();
        }

        private async void changePanel()
        {
            bool nextMove = true;
            if (index == 3)
            {
                nextMove = validateResult();
            }
            if (nextMove)
            {
                string oldName = "panel" + this.index;
                Panel oldPanel = (Panel)this.Controls.Find(oldName, true)[0];
                oldPanel.Visible = false;
                this.index--;
                updateButtons();
                if (index > 0)
                {
                    string newName = "panel" + this.index;
                    Panel newPanel = (Panel)this.Controls.Find(newName, true)[0];
                    newPanel.Visible = true;
                    updateButtons();
                    if (index == 2)
                    {
                        await installer.Installer.install(gameType, cpuArch, textBox1.Text, progressBar1);
                        changePanel();
                    }

                }
                else
                {
                    this.Close();
                }
            }
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            this.gameType = GameType.BFG;
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            this.gameType = GameType.NEW;
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            this.gameType = GameType.CLASSIC;
        }

        private void radioButton5_CheckedChanged(object sender, EventArgs e)
        {
            this.cpuArch = CPUArch.x64;
        }

        private void radioButton4_CheckedChanged(object sender, EventArgs e)
        {
            this.cpuArch = CPUArch.x86;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (index == 5)
            {
                this.Close();
            } else
            {
                string oldName = "panel" + this.index;
                Panel oldPanel = (Panel)this.Controls.Find(oldName, true)[0];
                oldPanel.Visible = false;
                index++;
                string newName = "panel" + this.index;
                Panel newPanel = (Panel)this.Controls.Find(newName, true)[0];
                newPanel.Visible = true;
                updateButtons();
            }
        }

        private void updateButtons()
        {
            switch(index)
            {
                case 5:
                    button1.Text = "next";
                    button2.Text = "cancel";
                    button3.Visible = false;
                    break;
                case 2:
                    button1.Visible = false;
                    button2.Visible = false;
                    button3.Visible = false;
                    break;
                case 1:
                    button1.Text = "close";
                    button1.Visible = true;
                    button2.Visible = false;
                    button3.Visible = false;
                    break;
                default:
                    button1.Text = "next";
                    button2.Text = "back";
                    button3.Text = "cancel";
                    button2.Visible = true;
                    button3.Visible = true;
                    break;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowser = new FolderBrowserDialog();
            folderBrowser.RootFolder = Environment.SpecialFolder.MyComputer;
            folderBrowser.ShowDialog();

            if (folderBrowser.SelectedPath != "")
            {
                textBox1.Text = folderBrowser.SelectedPath;
            }
        }

        private bool validateResult()
        {
            if (textBox1.Text == "")
            {
                MessageBox.Show("Please Select installation folder");
                return false;
            }
            DirectoryInfo d = new DirectoryInfo(textBox1.Text);
            FileInfo[] Files = d.GetFiles("*.exe");
            foreach (FileInfo file in Files)
            {
               if ((file.Name == "Doom3BFG.exe" || file.Name == "Doom3.exe") && gameType == GameType.CLASSIC)
                {
                    MessageBox.Show("Can't install Classic Edition in BFG/2019 installation folder");
                    return false;
                }

                if (file.Name == "Doom3BFG.exe" && gameType == GameType.NEW)
                {
                    MessageBox.Show("Can't install 2019 Edition in BFG installation folder");
                    return false;
                }

                if (file.Name == "Doom3.exe" && gameType == GameType.BFG)
                {
                    MessageBox.Show("Can't install BFG Edition in 2019 installation folder");
                    return false;
                }
            }
            return true;
        }

        private void radioButton1_MouseHover(object sender, EventArgs e)
        {
            label4.Text = "Install files for DOOM 3 BFG Edition";
        }

        private void radioButton2_MouseHover(object sender, EventArgs e)
        {
            label4.Text = "Install files for DOOM 3 (2019) Edition";
        }

        private void radioButton3_MouseHover(object sender, EventArgs e)
        {
            label4.Text = "Install files for DOOM BFA Classic Edition";
        }

        private void clearText(object sender, EventArgs e)
        {
            label4.Text = "";
        }

        private void radioButton5_MouseHover(object sender, EventArgs e)
        {
            label4.Text = "Install 64-bit executable files (Windows and Linux)";
        }

        private void radioButton4_MouseHover(object sender, EventArgs e)
        {
            label4.Text = "Install 32-bit executable files (Windows only)";
        }
    }
}
