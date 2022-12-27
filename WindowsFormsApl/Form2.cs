using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace WindowsFormsApl
{
    public partial class Form2 : Form
    {
        public Form2(string FilePath)
        {
            string DirPaths = Path.GetDirectoryName(FilePath) + '\\' + Path.GetFileNameWithoutExtension(FilePath) + "\\";

            InitializeComponent();
            TreeNode node = new TreeNode(DirPaths);
            chart1.Series.Clear();
            chart1.ChartAreas.Clear();
            this.Text = Path.GetFileNameWithoutExtension(FilePath);

            foreach (String drive in Environment.GetLogicalDrives())
            {
                node.Nodes.Add(new TreeNode());
                treeView1.Nodes.Add(node);
            }
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            e.Node.Nodes.Clear();
            try
            {
                foreach (string dir in Directory.GetDirectories(e.Node.FullPath)) { e.Node.Nodes.Add(Path.GetFileName(dir)); }
                setListItem(e.Node.FullPath);
            }
            catch
            {

            }
        }

        private void setListItem(String FilePath)
        {
            listView1.View = View.Details;
            listView1.Clear();
            listView1.Columns.Add("ファイル名");
            listView1.Columns.Add("ファイルサイズ");
            listView1.Columns.Add("フルパス");

            DirectoryInfo DirList = new DirectoryInfo(FilePath);
            foreach (DirectoryInfo DInfo in DirList.GetDirectories())
            {
                ListViewItem item = new ListViewItem(DInfo.Name);
                item.SubItems.Add("");
                listView1.Items.Add(item);
            }

            List<String> files = Directory.GetFiles(FilePath).ToList<String>();
            foreach (String file in files)
            {
                FileInfo info = new FileInfo(file);
                ListViewItem item = new ListViewItem(info.Name);
                item.SubItems.Add((info.Length / 1000).ToString() + "kb");
                item.SubItems.Add(info.FullName);
                listView1.Items.Add(item);
            }
            listView1.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private void listView1_ItemActivate(object sender, EventArgs e)
        {
            try
            {
                foreach (ListViewItem item in listView1.SelectedItems)
                {
                    var FilePath = item.SubItems[2].Text;
                    ChartMaker();
                }
            }
            catch { }

            }

        private void ChartMaker()
        {
            try
            {
                foreach (ListViewItem item in listView1.SelectedItems)
                {

                    var FilePath = item.SubItems[2].Text;
                    chart1.Series.Clear();
                    chart1.ChartAreas.Clear();
                    string chart_area1 = "Area1";
                    chart1.ChartAreas.Add(new ChartArea(chart_area1));
                    Series series = new Series();

                    series.ChartType = SeriesChartType.Line;
                    try
                    {
                        series.LegendText = (new FileInfo(FilePath).Name);
                        FileInfo tests = new FileInfo(FilePath);

                        if (new FileInfo(FilePath).Extension == ".csv")
                        {
                            using (StreamReader sr = new StreamReader(FilePath))
                            {
                                List<string> itemlist = new List<string>();
                                while (!sr.EndOfStream)
                                {
                                    var line = sr.ReadLine();
                                    double xpoint = Double.Parse(line.Split(',')[0]);
                                    double ypoint = Double.Parse(line.Split(',')[1]);
                                    series.Points.AddXY(xpoint, ypoint);
                                }
                            }
                            chart1.Series.Add(series);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }


        private void Form2_DragDrop(object sender, DragEventArgs e)
        {
            string[] Filepath = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            string DirPaths = Path.GetDirectoryName(Filepath[0]) + '\\' + Path.GetFileNameWithoutExtension(Filepath[0]) + "\\";
            TreeNode node = new TreeNode(DirPaths);

            node.Nodes.Add(new TreeNode());
            treeView1.Nodes.Add(node);

        }

        private void Form2_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

    }
}
