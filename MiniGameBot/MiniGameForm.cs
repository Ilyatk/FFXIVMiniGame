using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MiniGameBot
{
    public partial class MiniGameForm : Form
    {
        public static MiniGameSettings Settings = MiniGameSettings.Instance;

        public MiniGameForm()
        {
            InitializeComponent();
        }

        private void button_Save_Click(object sender, EventArgs e)
        {
            Settings.SelectedGame = comboBox_MiniGameType.Text;
            Settings.Count = (int)numericUpDown_PlayCount.Value;
            Settings.Save();

            this.Close();
        }

        private void MiniGameForm_Load(object sender, EventArgs e)
        {
            comboBox_MiniGameType.Text = Settings.SelectedGame;
            numericUpDown_PlayCount.Value = Settings.Count;
        }
    }
}
