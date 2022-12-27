using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Xml.Linq;
using static System.Net.WebRequestMethods;

namespace WindowsFormsApl
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            chart1.Series.Clear();
            chart1.ChartAreas.Clear();
            string chart_area1 = "Area1";
            chart1.ChartAreas.Add(new ChartArea(chart_area1));
            this.Text = "MZML移動平均";
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] fileName = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            textBox1.Text = fileName[0];

            FileInfo fileInfo = new FileInfo(textBox1.Text);
            if (fileInfo.Extension == ".mzML")
            {
                TICCreator(fileName[0]);
            }
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }

        }

        delegate void delegate_UpdateProgressBar(int value);
        private void UpdateProgressBar1(int value)
        {
            this.progressBar1.Value = value;
        }

        private class MSINT
        {
            internal double MoverZ;
            internal double Intensity;
        }

        private class MSINTWTime
        {
            internal List<MSINT> msint;
            internal double RetentionTime;
        }

        internal class TimeIntensity
        {
            internal double Timescsv;
            internal double Intensity;
        }

        internal class Mzsplit
        {
            internal double RetentionTime;
            internal double MoverZ;
            internal double IonIntensity;
        }

        private TimeIntensity ReadLineReturnTI(string sr)
        {
            string[] strings = sr.Split(',');
            return new TimeIntensity()
            {
                Timescsv = Convert.ToDouble(strings[0]),
                Intensity = Convert.ToDouble(strings[1])
            };
        }

        private Mzsplit CreateSampleFromString(string Line)
        {
            try
            {
                string[] Items = Line.Split(',');
                return new Mzsplit()
                {
                    RetentionTime = double.Parse(Items[0]),
                    MoverZ = double.Parse(Items[1]),
                    IonIntensity = double.Parse(Items[2])
                };
            }
            catch (InvalidOperationException ex) { throw ex; }
            catch (FormatException ex) { throw ex; }
        }


        /// <summary>
        /// 移動平均をするためのファイル検索　今回は使用しない
        /// </summary>
        /// <param name="FilePath">ファイルパス</param>
        /// <param name="windowsize">窓幅</param>
        /// <param name="threshold">判定の閾値</param>
        private void MovingAverageFolderSearcher(string FilePath, int windowsize, double threshold)
        {
            var FilePathString = Path.GetDirectoryName(FilePath) + '\\' + Path.GetFileNameWithoutExtension(FilePath) + '\\';
            List<String> files = Directory.GetFiles(FilePathString).ToList<String>();

           foreach (String file in files)
            {
                    if (new FileInfo(file).Extension == ".csv")
                    {
                        FileInfo info = new FileInfo(file);
                        var path = info.FullName;
                        MovingAveragerWithCheck(path, windowsize, threshold);
                        WMovingAveragerWithCheck(path, windowsize, threshold);
                    }
                    else
                    { }
            }
        }

        /// <summary>
        /// 移動平均をするためのファイル検索　非同期、プログレスバーを操作するために変更
        /// </summary>
        /// <param name="FilePath">ファイルパス</param>
        /// <param name="windowsize">窓幅</param>
        /// <param name="threshold">判定の閾値</param>
        private async Task<int> TMovingAverageFolderSearcher(string FilePath, int windowsize, double threshold)
        {
            var test2 = Path.GetDirectoryName(FilePath) + '\\' + Path.GetFileNameWithoutExtension(FilePath) + '\\';
            List<String> files = Directory.GetFiles(test2).ToList<String>();

            progressBar1.Minimum = 0;
            progressBar1.Maximum = files.Count() ;
            progressBar1.Value = 0;
            int i = 1;

            await Task.Run(() => {
                foreach (String file in files)
                {
                    if (new FileInfo(file).Extension == ".csv")
                    {
                        FileInfo info = new FileInfo(file);
                        var path = info.FullName;
                        MovingAveragerWithCheck(path, windowsize, threshold);
                        WMovingAveragerWithCheck(path, windowsize, threshold);
                  //    STTestMoveAv2(path, windowsize, threshold); //標準化したい場合は使う
                    }
                    else
                    { }
                    delegate_UpdateProgressBar Callback = new delegate_UpdateProgressBar(UpdateProgressBar1);
                    this.progressBar1.Invoke(Callback, i);
                    i++;
                }
            });
            return 0; 
        }

        /// <summary>
        /// 移動平均をする
        /// </summary>
        /// <param name="FilePath">StringBuilder</param>
        /// <param name="windowsize">窓幅</param>
        /// <param name="threshold">判定の閾値</param>
        private void MovingAveragerWithCheck(string FilePath, int windowsize, double threshold)
        {
            DirectoryInfo Dinfo = new DirectoryInfo(FilePath);

            var DefaultDirectoryName = Dinfo.Parent.Parent.FullName + "\\" + Dinfo.Parent.Name + "_MV" + "\\";
            var plusDirectoryName = Dinfo.Parent.Parent.FullName + "\\" + Dinfo.Parent.Name + "_MV" + "\\" + "over_threshold" + "\\";
            var minusDirectoryName = Dinfo.Parent.Parent.FullName + "\\" + Dinfo.Parent.Name + "_MV" + "\\" + "under_threshold" + "\\";
            Directory.CreateDirectory(DefaultDirectoryName);
            Directory.CreateDirectory(plusDirectoryName);
            Directory.CreateDirectory(minusDirectoryName);

            int MovingAverageWindow = windowsize;

            using (StreamReader sr = new StreamReader(FilePath))
            {
                List<TimeIntensity> test = new List<TimeIntensity>();
                while (!sr.EndOfStream)
                {
                    test.Add((ReadLineReturnTI(sr.ReadLine())));
                }
                IEnumerable<double> Average = test.Select(x => x.Intensity);
                var AveragedSource = Average.Average();
                Average = Average.Select(x => x / AveragedSource);

                var Tester1 = Enumerable.Range(0, Average.Count() - MovingAverageWindow).Select(i => Average.Skip(i).Take(MovingAverageWindow).Average()).ToList();

                string filesavename = "";

                if (Tester1.Max() > threshold) { filesavename = (plusDirectoryName + Dinfo.Name); }
                else if (Tester1.Min() < threshold) { filesavename = (minusDirectoryName + Dinfo.Name); }
                else { filesavename = (DefaultDirectoryName + Dinfo.Name); }
                StreamWriter sw = new StreamWriter(filesavename);

                int n = 0;
                foreach (var Value in Tester1)
                {
                    var RetentionTime = test[n].Timescsv.ToString();
                    sw.WriteLine(RetentionTime + ',' + (Value - 0.0).ToString()); //Double 0回避
                    n++;
                }
                sw.Close();
            }
        }

        /// <summary>
        /// 加重移動平均をする　
        /// </summary>
        /// <param name="FilePath">StringBuilder</param>
        /// <param name="windowsize">窓幅</param>
        /// <param name="threshold">判定の閾値</param>
        private void WMovingAveragerWithCheck(string FilePath, int windowsize, double threshold)
        {
            DirectoryInfo info = new DirectoryInfo(FilePath);

            var DefaultDirectoryName = info.Parent.Parent.FullName + "\\" + info.Parent.Name + "_WMV" + "\\";
            var plusDirectoryName = info.Parent.Parent.FullName + "\\" + info.Parent.Name + "_WMV" + "\\" + "over_threshold" + "\\";
            var minusDirectoryName = info.Parent.Parent.FullName + "\\" + info.Parent.Name + "_WMV" + "\\" + "under_threshold" + "\\";
            Directory.CreateDirectory(DefaultDirectoryName);
            Directory.CreateDirectory(plusDirectoryName);
            Directory.CreateDirectory(minusDirectoryName);

            int MovingAverageWindow = windowsize;

            using (StreamReader sr = new StreamReader(FilePath))
            {
                List<TimeIntensity> test = new List<TimeIntensity>();
                while (!sr.EndOfStream) { test.Add((ReadLineReturnTI(sr.ReadLine()))); }
                IEnumerable<double> Average = test.Select(x => x.Intensity);
                var AveragedSource = Average.Average();
                Average = Average.Select(x => x / AveragedSource);

                var Tester2 = Enumerable.Range(0, Average.Count() - MovingAverageWindow).Select(i => Average.Skip(i).Take(MovingAverageWindow).ToList()).ToList();

                List<double> Tester3 = new List<double>();
                foreach (var x in Tester2) { Tester3.Add(WeightedMovingAv(x, MovingAverageWindow)); }

                string SaveFileName = "";

                int Taker = Average.Count() / 100;

                var ThresholdChecker = Tester3.OrderByDescending(x => x).Take(Taker).Last();

                if (ThresholdChecker > threshold +1 ) { SaveFileName = (plusDirectoryName + info.Name); }
                else if (ThresholdChecker < threshold ) { SaveFileName = (minusDirectoryName + info.Name); }
                else { SaveFileName = (DefaultDirectoryName + info.Name); }
                StreamWriter sw = new StreamWriter(SaveFileName);

                int n = 0;
                foreach (var Value in Tester3)
                {
                    var RetentionTime = test[n].Timescsv.ToString();
                    sw.WriteLine(RetentionTime + ',' + (Value - 0.0).ToString()); //Double 0回避
                    n++;
                }
                sw.Close();
            }
        }

        /// <summary>
        /// 標準化をする試験モジュール
        /// </summary>
        /// <param name="FilePath">StringBuilder</param>
        /// <param name="windowsize">窓幅</param>
        /// <param name="threshold">判定の閾値</param>
        private void STTestMoveAv2(string FilePath, int MovingAverageWindow, double threshold)
        {
            DirectoryInfo Dinfo = new DirectoryInfo(FilePath);
            var DefaultDirectoryName = Dinfo.Parent.Parent.FullName + "\\" + Dinfo.Parent.Name + "_STMV" + "\\";
            var plusDirectoryName = Dinfo.Parent.Parent.FullName + "\\" + Dinfo.Parent.Name + "_STMV" + "\\" + " overSTDev" + "\\";
            var minusDirectoryName = Dinfo.Parent.Parent.FullName + "\\" + Dinfo.Parent.Name + "_STMV" + "\\" + " underSTDev" + "\\";
            Directory.CreateDirectory(DefaultDirectoryName);
            Directory.CreateDirectory(plusDirectoryName);
            Directory.CreateDirectory(minusDirectoryName);




            using (StreamReader sr = new StreamReader(FilePath))
            {
                List<TimeIntensity> test = new List<TimeIntensity>();
                while (!sr.EndOfStream)
                {
                    test.Add((ReadLineReturnTI(sr.ReadLine())));
                }
                IEnumerable<double> averager = test.Select(x => x.Intensity);
                var IntensityAverage = averager.Average();

                double std = 0.0;

                    IEnumerable<double> STaverager = test.Select(x => (x.Intensity - IntensityAverage));

                    double sumstd = 0.0;
                    foreach (var x in STaverager)
                    {
                        sumstd += Math.Pow(x, 2);
                    }
                    
                    std = sumstd / STaverager.Count();
                    IEnumerable<double> STDEDLIST = test.Select(x => ((x.Intensity - IntensityAverage) / std));

                    var tes = Enumerable.Range(0, STDEDLIST.Count() - MovingAverageWindow).Select(i => STDEDLIST.Skip(i).Take(MovingAverageWindow).Average()).ToList();
                    var tes2 = Enumerable.Range(0, STDEDLIST.Count() - MovingAverageWindow).Select(i => STDEDLIST.Skip(i).Take(MovingAverageWindow).ToList()).ToList();

                    List<double> tes3 = new List<double>();
                    foreach (var x in tes2) { tes3.Add(WeightedMovingAv(x, MovingAverageWindow)); }



                string filesavename = "";
                var desAv = tes3.Average();
                int taker = averager.Count() / 100;

                var dist = tes3.OrderByDescending(x => x).Take(taker).Last();

                if (dist > desAv) { filesavename = (plusDirectoryName + Dinfo.Name); }
                else if (dist < desAv) { filesavename = (minusDirectoryName + Dinfo.Name); }
                else { filesavename = (DefaultDirectoryName + Dinfo.Name); }
                    StreamWriter sw = new StreamWriter(filesavename);

                    int n = 0;
                    foreach (var te in tes)
                    {
                        var chchk = test[n].Timescsv.ToString();
                        sw.WriteLine(chchk + ',' + (te - 0.0).ToString()); //Double 0回避
                        n++;
                    }
                    sw.Close();
               
            }
        }

        /// <summary>
        /// 加重移動平均の共通部分
        /// </summary>
        /// <param name="source">StringBuilder</param>
        /// <param name="ParsedDirectoryName">作る予定のディレクトリパス</param>
        private Double WeightedMovingAv(List<double> source, int Window)
        {
            double s = 0;
            for (int i = 0; i < source.Count(); i++) { s += source[i] * (i + 1); }
            s = s / (Window * (1 + Window) / 2);
            return s;
        }

        /// <summary>
        /// MZMLのTIC,BPCを生成する
        /// </summary>
        /// <param name="FileName">必ずMZMLを入力すること</param>
        private void TICCreator(string FileName)
        {
            using (StreamReader sr = new StreamReader(FileName, Encoding.GetEncoding("Shift_JIS")))
            {
                String FileString = sr.ReadToEnd();
                XElement xmlTree = XElement.Parse(FileString);
                var TICstr = xmlTree.Descendants().Where(x => x.Name.LocalName == "chromatogram");
                var BinaryString64Base = TICstr.Descendants().DescendantsAndSelf().Where(x => x.Name.LocalName == "binary").Distinct();
                var BitStringFromMZML = TICstr.Descendants().Attributes().Where(yn => yn.Value.Contains("-bit"));

                List<string> String64BASE = new List<string>();
                foreach (var x in BinaryString64Base)
                {
                    String64BASE.Add(x.Value.ToString());
                }

                List<string> BitString = new List<string>();
                foreach (var x in BitStringFromMZML)
                {
                    BitString.Add(x.Value.ToString());
                }

                /// <summary>
                /// 下記を private void Chartstarter(List<double> TICTime, List<double> TICInt, List<double> BPCTime, List<double> BPCInt)
                /// することで関数にできる
                /// </summary>
                /// <param name="TICTime"></param>
                /// <param name="TICInt"></param>
                /// <param name="BPCTime"></param>
                /// <param name="BPCInt"></param>
                {
                    List<double> TICTIme = MZMLcsvsaveUnityPrime(BitString[0], String64BASE[0]);
                    List<double> TICInt = MZMLcsvsaveUnityPrime(BitString[1], String64BASE[1]);
                    List<double> BPCTime = MZMLcsvsaveUnityPrime(BitString[2], String64BASE[2]);
                    List<double> BPCInt = MZMLcsvsaveUnityPrime(BitString[3], String64BASE[3]);

                    chart1.Series.Clear();
                    chart1.ChartAreas.Clear();
                    string chart_area1 = "Area1";
                    chart1.ChartAreas.Add(new ChartArea(chart_area1));


                    Series TICSeries = new Series();
                    TICSeries.ChartType = SeriesChartType.Line;
                    TICSeries.LegendText = "TIC";

                    Series BPCSeries = new Series();
                    BPCSeries.ChartType = SeriesChartType.Line;
                    BPCSeries.LegendText = "BPC";

                    for (int x = 0; x < TICInt.Count(); x++)
                    {
                        double TICTimeAsX = (TICTIme[x]);
                        double TICIntAsY = (TICInt[x]);
                        double BPCTimeAsX = (BPCTime[x]);
                        double BPCIntAsY = (BPCInt[x]);
                        TICSeries.Points.AddXY(TICTimeAsX, TICIntAsY);
                        BPCSeries.Points.AddXY(BPCTimeAsX, BPCIntAsY);
                    }
                    chart1.Series.Add(TICSeries);
                    chart1.Series.Add(BPCSeries);

                    textBox2.Text = "0.0";
                    textBox3.Text = BPCTime.Last().ToString();
                }
            }
        }

        /// <summary>
        /// MZMLのBASE64を展開する関数
        /// </summary>
        /// <param name="bits">bit数をstringで入力 32-bitなどでも入力可能</param>
        /// <param name="RawDataString">変換する文字列を入力、LabSolutionsで生じるの改行も除去可能</param>
        /// <returns> List<double> </returns>
        private List<double> MZMLcsvsaveUnityPrime(string bits, string RawDataString)
        {
            try
            {
                int bit = Int32.Parse((Regex.Replace(bits, @"[^0-9]", "")));

                string DataString = RawDataString;
                List<double> BaseParsed = new List<double>();

                if (DataString == null)
                {
                    DataString = "====";
                }

                DataString = DataString.Replace("\r", "");
                DataString = DataString.Replace("\n", "");

                String paddingBase64Txt = DataString;
                if (paddingBase64Txt.Length % 4 != 0)
                {
                    paddingBase64Txt = DataString.PadRight(DataString.Length + (4 - DataString.Length % 4), '=');
                }
                DataString = paddingBase64Txt;
                var arrlength = Convert.FromBase64String(DataString).Length;
                double[] returndouble = new double[arrlength];
                byte[] bytearrey = Convert.FromBase64String(DataString);

                if (bit == 32)
                {
                    int j = 0;
                    var endbit = (bytearrey.Length) - 4;
                    for (long i = 0; j < endbit; i++)
                    {
                        byte[] tempb = BitConverter.GetBytes(BitConverter.ToSingle(bytearrey, j));
                        //Array.Reverse(tempb);　//mzxmlはエンディアンが違う
                        var bitd1 = BitConverter.ToSingle(tempb, 0);   // 結局ここで分岐が入る
                        j += 4;
                        returndouble[i] = bitd1;
                        BaseParsed.Add(bitd1);
                    }
                }

                if (bit == 64)
                {
                    int j = 0;
                    var endbit = (bytearrey.Length) - 8;
                    for (long i = 0; j < endbit; i++)
                    {
                        byte[] tempb = BitConverter.GetBytes(BitConverter.ToDouble(bytearrey, j));
                        //Array.Reverse(tempb);//mzxmlはエンディアンが違う
                        var bitd1 = BitConverter.ToDouble(tempb, 0); // 結局ここで分岐が入る
                        j += 8;
                        returndouble[i] = bitd1;
                        BaseParsed.Add(bitd1);
                    }
                }

                return BaseParsed;
            }
            catch (InvalidOperationException ex) { throw ex; }
            catch (DriveNotFoundException ex) { throw ex; }
            catch (FormatException ex) { throw ex; }
        }

        /// <summary>
        /// MZMLのBASE64を展開する関数に小数点指定桁で四捨五入する関数、オーバーロードで機能、別にDistinctする必要がある
        /// </summary>
        /// <param name="bits">string bit数をstringで入力 32-bitなどでも入力可能</param>
        /// <param name="RawDataString">string 変換する文字列を入力、LabSolutionsで生じるの改行も除去可能</param>
        /// <param name="RoundParam">四捨五入する小数点桁数を入力</param>
        /// <returns> List<double> </returns>
        private List<double> MZMLcsvsaveUnityPrime(string bits, string RawDataString, int RonundParam)
        {
            try
            {
                int bit = Int32.Parse((Regex.Replace(bits, @"[^0-9]", "")));

                string DataString = RawDataString;
                List<double> BaseParsed = new List<double>();

                if (DataString == null)
                {
                    DataString = "====";
                }

                DataString = DataString.Replace("\r", "");
                DataString = DataString.Replace("\n", "");

                String paddingBase64Txt = DataString;
                if (paddingBase64Txt.Length % 4 != 0)
                {
                    paddingBase64Txt = DataString.PadRight(DataString.Length + (4 - DataString.Length % 4), '=');
                }
                DataString = paddingBase64Txt;
                var arrlength = Convert.FromBase64String(DataString).Length;
                double[] returndouble = new double[arrlength];
                byte[] bytearrey = Convert.FromBase64String(DataString);


                if (bit == 32)
                {
                    int j = 0;
                    var endbit = (bytearrey.Length) - 4;
                    for (long i = 0; j < endbit; i++)
                    {
                        byte[] tempb = BitConverter.GetBytes(BitConverter.ToSingle(bytearrey, j));
                        //Array.Reverse(tempb);　//mzxmlはエンディアンが違う
                        var bitd1 = BitConverter.ToSingle(tempb, 0);   // 結局ここで分岐が入る
                        j += 4;
                        returndouble[i] = bitd1;
                        double bitd2 = Math.Round(bitd1, RonundParam, MidpointRounding.ToEven);
                        BaseParsed.Add(bitd2);
                    }
                }

                if (bit == 64)
                {
                    int j = 0;
                    var endbit = (bytearrey.Length) - 8;
                    for (long i = 0; j < endbit; i++)
                    {
                        byte[] tempb = BitConverter.GetBytes(BitConverter.ToDouble(bytearrey, j));
                        //Array.Reverse(tempb);//mzxmlはエンディアンが違う
                        var bitd1 = BitConverter.ToDouble(tempb, 0); // 結局ここで分岐が入る
                        j += 8;
                        returndouble[i] = bitd1;

                        double bitd2 = Math.Round(bitd1, RonundParam, MidpointRounding.ToEven);
                        BaseParsed.Add(bitd2);
                    }
                }

                return BaseParsed;
            }
            catch (InvalidOperationException ex) { throw ex; }
            catch (DriveNotFoundException ex) { throw ex; }
            catch (FormatException ex) { throw ex; }
        }

        /// <summary>
        ///  MZMLをStringBuilderで返すための基本の関数。
        ///  SBreturner
        ///  StringBuilderFiler2
        ///  と連携する
        ///  SBreturnerをPuddingSBreturnerにすることもできる
        /// ファイル全部を処理するためメモリに注意、MZMLcsvsaveUnityPrime()を使う
        /// </summary>
        /// <param name="FilePath)">string ファイルのフルパス</param>
        /// <returns> 移動平均処理するためのパスを返す </returns>
        private  async Task<string> PRoundedMSParse(string FilePath, string StartMS, string EndMS,
            string StartTime, string EndTime, string PolalityString, string MSxString, string RoundString)
        {

            double DStartTime = Double.Parse(StartTime); 
            double DEndTime = double.Parse(EndTime); 
            double DStartMS = double.Parse(StartMS);
            double DEndMS = double.Parse(EndMS);

            string RoundParam = RoundString.ToString();

            string FilePathWithoutExtenstion = Path.GetDirectoryName(FilePath);
            string ParsedDirectoryName = FilePathWithoutExtenstion + '\\' + Path.GetFileNameWithoutExtension(FilePath)
                + '\\' + PolalityString + " " + MSxString + "_" + "PRounded" + "_" + RoundParam + "_";

            StringBuilder sb = new StringBuilder();

            sb = PuddingSBreturner(FilePath, PolalityString, MSxString, RoundParam, StartMS, EndMS, StartTime, EndTime);
            StringBuilderFiler(sb, ParsedDirectoryName);

            GC.Collect();
            return ParsedDirectoryName;
        }

        /// <summary>
        /// MZMLをStringBuilderで返す、時間分処理するため巨大なStringbuilderになる、ファイル全部を処理するためメモリに注意、MZMLcsvsaveUnityPrime()を使う
        /// </summary>
        /// <param name="FileName">string ファイルのフルパス</param>
        /// <param name=" PolalityString">string positive scan もしくは negative scanが入る</param>
        /// <param name=" MSxString">string MS1 spectrum もしくはMSn spectrum</param>
        /// <param name="RoundParam">string 小数点以下切り捨てる桁数</param>
        /// <returns> StringBuilder </returns>
        private StringBuilder PuddingSBreturner(string FileName, string PolalityString, string MSxString, string RoundParam,
            string StartMS, string EndMS, string StartTime, string EndTime)
        {
            double DStartTime = Double.Parse(StartTime); 
            double DEndTime = double.Parse(EndTime) ; 
            double DStartMS = double.Parse(StartMS);
            double DEndMS = double.Parse(EndMS);

            int RoundString = Int32.Parse(RoundParam);

            StringBuilder sb = new StringBuilder();

            var MZParsedLists = new List<List<double>>();
            var InParsedLists = new List<List<double>>();
            List<string> TimeParsedList = new List<string>();

            using (StreamReader sr = new StreamReader(FileName, Encoding.GetEncoding("UTF-8")))
            {
                String str = sr.ReadToEnd();
                XElement xmlTree = XElement.Parse(str);


                // mz 配列を取得
                var mz64base = xmlTree.Descendants().Elements()
                    .Where(x => x.Name.LocalName == "binary")
                    .Where(y => y.Parent.Name.LocalName == "binaryDataArray")
                    .Where((a, index) => index % 2 == 0)
                    .Where(b => b.Parent.Parent.Parent.ToString().Contains(PolalityString))
                    .Where(c => c.Parent.Parent.Parent.ToString().Contains(MSxString));

                var mznbits = xmlTree.Descendants().Attributes()
                   .Where(x => x.Value.ToString().Contains("-bit"))
                   .Where((a, index) => index % 2 == 0)
                   .Where(n => n.Parent.Parent.Name.LocalName == "binaryDataArray")
                   .Where(m => m.Parent.Parent.FirstNode.NextNode.NextNode.ToString().Contains("m/z array"))
                   .Where(b => b.Parent.Parent.Parent.Parent.FirstNode.ToString().Contains(PolalityString))
                   .Where(c => c.Parent.Parent.Parent.Parent.ToString().Contains(MSxString));

                List<string> MZ64BaseString = new List<string>();
                foreach (XElement binary in mz64base) { MZ64BaseString.Add(binary.Value.TrimStart('\n')); }

                List<string> MZbitListString = new List<string>();
                foreach (var binary in mznbits) { MZbitListString.Add(binary.Value.TrimStart('\n')); }

                //リストサイズが異なる場合何かしら問題がある
                if (MZ64BaseString.Count != MZbitListString.Count) { throw new Exception(); }

                var MZZIP = MZ64BaseString.Zip(MZbitListString, (mz, bit) => new { Mz = mz, Bits = bit });
                foreach (var i in MZZIP) { MZParsedLists.Add(MZMLcsvsaveUnityPrime(i.Bits, i.Mz)); }

                //強度配列を取得

                var in64base = xmlTree.Descendants().Elements()
                       .Where(x => x.Name.LocalName == "binary")
                       .Where(y => y.Parent.Name.LocalName == "binaryDataArray")
                       .Where((a, index) => index % 2 == 1)
                       .Where(b => b.Parent.Parent.Parent.ToString().Contains(PolalityString))
                       .Where(c => c.Parent.Parent.Parent.ToString().Contains(MSxString));

                var inbits = xmlTree.Descendants().Attributes()
                        .Where(x => x.Value.ToString().Contains("-bit"))
                        .Where((a, index) => index % 2 == 1)
                        .Where(n => n.Parent.Parent.Name.LocalName == "binaryDataArray")
                        .Where(m => m.Parent.Parent.FirstNode.NextNode.NextNode.ToString().Contains("intensity array"))
                        .Where(b => b.Parent.Parent.Parent.Parent.FirstNode.ToString().Contains(PolalityString))
                        .Where(c => c.Parent.Parent.Parent.Parent.ToString().Contains(MSxString));

                List<string> Inten64BaseString = new List<string>();
                foreach (XElement binary in in64base) { Inten64BaseString.Add(binary.Value.TrimStart('\n')); }

                List<string> IntenBitListString = new List<string>();
                foreach (var binary in inbits) { IntenBitListString.Add(binary.Value.TrimStart('\n')); }

                //リストサイズが異なる場合何かしら問題がある
                if (Inten64BaseString.Count != IntenBitListString.Count) { throw new Exception(); }

                var IntensityZip = Inten64BaseString.Zip(IntenBitListString, (inten, bit) => new { Inten = inten, Bits = bit });
                foreach (var i in IntensityZip) { InParsedLists.Add(MZMLcsvsaveUnityPrime(i.Bits, i.Inten)); }

                if (InParsedLists.Count != MZParsedLists.Count) { throw new Exception(); }

                //時間
                var IETimeList = xmlTree.Descendants().Attributes()
                    .Where(x => x.Name == "value")
                    .Where(y => y.Parent.Parent.Name.LocalName == "scan")
                    .Where(z => z.PreviousAttribute.Value == "second")
                    .Where(a => a.Value != "")
                    .Where(b => b.Parent.Parent.Parent.Parent.FirstNode.ToString().Contains(PolalityString))
                    .Where(c => c.Parent.Parent.Parent.Parent.FirstNode.NextNode.ToString().Contains(MSxString));

                foreach (var binary2 in IETimeList) { TimeParsedList.Add(binary2.Value); }

                //リストサイズが異なる場合何かしら問題がある
                if (TimeParsedList.Count != IntenBitListString.Count) { throw new Exception(); }

                List<double> ChekMZListAllArray = new List<double>();


                //ゼロパディングの下準備
                for (int n = 0; n < TimeParsedList.Count(); n++)
                {
                    var MZListForProcess = MZMLcsvsaveUnityPrime(MZbitListString[n], MZ64BaseString[n], RoundString);
                    foreach (var x in MZListForProcess) { ChekMZListAllArray.Add(x); };
                }
                var ArrayedAllMZList = ChekMZListAllArray.Distinct().ToArray();


                for (int i = 0; i < TimeParsedList.Count(); i++)
                {
                    if (DStartTime <= Double.Parse(TimeParsedList[i]) && DEndTime >= Double.Parse(TimeParsedList[i]))
                    {
                        var ParsedMZList = MZMLcsvsaveUnityPrime(MZbitListString[i], MZ64BaseString[i], RoundString);
                        var ParsedIntensity = MZMLcsvsaveUnityPrime(IntenBitListString[i], Inten64BaseString[i], RoundString);

                        var exest = ArrayedAllMZList.Except(ParsedMZList).ToList();
                        foreach (var x in exest) ParsedMZList.Add(x);

                        if (ParsedMZList.Count() > ParsedIntensity.Count())
                        {
                            int counter = ParsedMZList.Count() - ParsedIntensity.Count();
                            for (int g = 0; g < (counter); g++) { ParsedIntensity.Add((Double)0); }
                        }

                        var BeforeDistinct = ParsedMZList.Zip(ParsedIntensity, (mz, inten) => new { MZ = mz, Inten = inten });

                        var SoredItems = from order in BeforeDistinct.AsEnumerable()
                                    group order by order.MZ
                                    into m
                                    select new MSINT
                                    {
                                        MoverZ = m.Key,
                                        Intensity = m.Sum(order => order.Inten),
                                    };

                        //StringBuilderで処理、100MBくらいまでならこれで問題ない
                        foreach (var item in SoredItems)
                        {
                            if (DStartMS <= item.MoverZ && DEndMS >= item.MoverZ)
                            {
                                sb.Append(TimeParsedList[i].ToString());
                                sb.Append(",");
                                sb.Append(item.MoverZ.ToString());
                                sb.Append(",");
                                sb.Append(item.Intensity.ToString());
                                sb.Append("\r");
                            }
                        }
                    }
                }
            }

            GC.Collect();
            return sb;
        }

        /// <summary>
        /// StringBuilderを時間ごとに保存する
        /// </summary>
        /// <param name="sb">StringBuilder</param>
        /// <param name="ParsedDirectoryName">作る予定のディレクトリパス</param>
        private void StringBuilderFiler(StringBuilder sb, string ParsedDirectoryName)
        {
            {
                IEnumerable<string> Lines = sb.ToString().TrimEnd().Split('\r');
                IEnumerable<Mzsplit> Sources = Lines.Select(Line1 => CreateSampleFromString(Line1));
                var PosSorterdLinq = Sources.OrderBy(lin2 => lin2.MoverZ).ThenBy(lin3 => lin3.RetentionTime).GroupBy(h => h.MoverZ);

                try
                {
                    Directory.CreateDirectory(ParsedDirectoryName);
                }
                catch (InvalidOperationException ex) { throw ex; }
                catch (DriveNotFoundException ex) { throw ex; }
                catch (FormatException ex) { throw ex; }

                foreach (var group in PosSorterdLinq)
                {
                    StringBuilder StrBul = new StringBuilder();
                    var tempkey = group.Key;
                    foreach (var item in group)
                    {
                        StrBul.Append(item.RetentionTime);
                        StrBul.Append(",");
                        StrBul.Append(item.IonIntensity);
                        StrBul.Append("\r");
                    }

                    string FileSavepath = ParsedDirectoryName + '\\' + group.Key + ".csv";//名前は変更
                    StreamWriter sw = new StreamWriter(FileSavepath);
                    try
                    {
                        sw.Write(StrBul.ToString());
                    }
                    catch (InvalidOperationException ex) { throw ex; }
                    catch (DriveNotFoundException ex) { throw ex; }
                    catch (FormatException ex) { throw ex; }
                    finally
                    {
                        StrBul.Clear();
                        sw.Close();
                        GC.Collect();
                    }
                    StrBul.Clear();
                    GC.Collect();
                }
            }
        }



        private async void button1_Click_1(object sender, EventArgs e)
        {
            string FileName = textBox1.Text;
            string StartMS = textBox4.Text;
            string EndMS = textBox5.Text;
            string StartTime = textBox2.Text;
            string EndTime = textBox3.Text;
            string PolalityString = "";
            string MSxString = "";
            string PositiveString = "positive scan";
            string NegativeString = "negative scan";
            string ms1str = "MS1 spectrum";
            string msnstr = "MSn spectrum";


            if (Positive.Checked) { PolalityString = PositiveString; }
            else if (Negative.Checked) { PolalityString = NegativeString; }
            else { Exception exception = new Exception(); }

            if (MS1.Checked) { MSxString = ms1str; }
            else if (MSn.Checked) { MSxString = msnstr; }
            else { Exception exception = new Exception(); }

            int RoundStringINT = new int();
            try { RoundStringINT = int.Parse((string)comboBox1.SelectedItem); }
            catch { RoundStringINT = 1; }
            string RoundString = RoundStringINT.ToString();

            int windowsize = 1;
            double threshold = 2.0;

            try { windowsize = int.Parse(textBox6.Text); }
            catch { windowsize = 1; }
            try { threshold = Double.Parse(textBox7.Text); }
            catch { threshold = 2.0; }

            MessageBox.Show("クリックして処理開始");

            try
            {
                FileInfo fileInfo = new FileInfo(textBox1.Text);
                if (textBox1.Text != null && (fileInfo.Extension == ".mzML"))
                {
                    string FilePath = textBox1.Text;

                    string DirectroyName = await PRoundedMSParse(FilePath, StartMS, EndMS, StartTime, EndTime, PolalityString, MSxString, RoundString);
                    await TMovingAverageFolderSearcher(DirectroyName, windowsize, threshold);
                }
                MessageBox.Show("処理終了");

                Form2 form2 = new Form2(FileName);
                form2.Show();
            }
            catch
            {
                MessageBox.Show("ファイルか設定が不正です");
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string FileName = textBox1.Text;
            Form2 form2 = new Form2(FileName);
            form2.Show();
        }

    }
}
