using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesignColumn
{
    public partial class MyModelessForm : Form
    {
        // 优化一下使用listTextContentsNeed的值来保存起来
        private List<string> _listTextContents = new List<string>();
        private Dictionary<string, double> _dictContextsNeed = new Dictionary<string, double>();
        private double _dRebarRate = 0;
        public MyModelessForm()
        {
            InitializeComponent();
            SetupLayout();
            // 假设你添加了一个按钮 btnConfirm
            // this.btnConfirm.Click += BtnConfirm_Click; 
        }

        public string TextBox1Value
        {
            get { return textBox1.Text; }
            set { textBox1.Text = value; }
        }

        public string TextBox2Value
        {
            get { return textBox2.Text; }
            set { textBox2.Text = value; }
        }


        /// <summary>
        /// 自动排列控件位置，避免硬编码坐标
        /// </summary>
        private void SetupLayout()
        {
            int startX = 20;      // 起始 X
            int startY = 20;      // 起始 Y
            int labelWidth = 140; // 标签宽度
            int textBoxWidth = 80; // 输入框宽度
            int verticalGap = 40;   // 垂直间距
            int horizontalGap = 20; // 水平间距

            // 定义每一行的控件组 (Label, TextBox, Optional Label, Optional TextBox)
            // null 表示该位置没有控件
            var rows = new (Control lbl, Control txt, Control lbl2, Control txt2)[]
            {
            (label1, textBox1, null, null),       // X向尺寸
            (label2, textBox2, null, null),       // Y向尺寸
            (label3, textBox3, label4, textBox4), // X向钢筋 (直径 + 根数)
            (label5, textBox5, label6, textBox6), // Y向钢筋 (直径 + 根数)
            (label7, textBox7, null, null),       // 角筋直径
            (label8, textBox8, null, null)        // 箍筋直径
            };

            int currentY = startY;

            foreach (var row in rows)
            {
                int currentX = startX;

                // 1. 放置第一个 Label
                if (row.lbl != null)
                {
                    row.lbl.Location = new Point(currentX, currentY + 10); // +10 是为了垂直居中对齐 TextBox
                    currentX += labelWidth + horizontalGap;
                }

                // 2. 放置第一个 TextBox
                if (row.txt != null)
                {
                    row.txt.Location = new Point(currentX, currentY);
                    currentX += textBoxWidth + horizontalGap * 2;
                }

                // 3. 放置第二个 Label (如果有)
                if (row.lbl2 != null)
                {
                    currentX += 80;
                    row.lbl2.Location = new Point(currentX, currentY + 10);
                    currentX += 50 + horizontalGap; // 短标签
                }

                // 4. 放置第二个 TextBox (如果有)
                if (row.txt2 != null)
                {
                    row.txt2.Location = new Point(currentX, currentY);
                }

                // 下一行
                currentY += verticalGap;
            }

            // 放置按钮 (固定在底部或特定位置)
            int buttonY = currentY + 80;
            btnConfirm.Location = new Point(startX, buttonY);
            button1.Location = new Point(startX + 150, buttonY);
            button2.Location = new Point(startX + 300, buttonY);

            // 自动调整窗体高度以适应内容
            this.ClientSize = new Size(800, buttonY + 200);
        }

        private void btnConfirm_Click_1(object sender, EventArgs e)
        {
            // 在这里获取值，因为这是用户主动触发的
            string val1 = this.textBox1.Text;
            string val2 = this.textBox2.Text;

            // 在这里执行后续逻辑，比如调用 AutoCAD API 绘图
            MessageBox.Show($"获取到的值: {val1}, {val2}");
            DrawColumn(_dictContextsNeed);
            // 如果需要，可以在此处关闭窗体
            // this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // 清空上次存储的数据
            _listTextContents.Clear();
            GetListTextContents(); 
            if (_listTextContents.Count <= 0)
            {
                return;
            }
            _dictContextsNeed = GetNeedListContexts(_listTextContents);
            _dRebarRate = CaculateTotalRebarWeight(_dictContextsNeed) / (_dictContextsNeed["dXLength"] / 1000  * _dictContextsNeed["dYLength"] / 1000);
        }

        private void button2_Click(object sender, EventArgs e)
        {

        }
        private void GetListTextContents()
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            // 1. 提示用户选择实体
            PromptSelectionResult psr = ed.GetSelection();
            if (psr.Status == PromptStatus.OK)
            {
                SelectionSet ss = psr.Value;

                // 2. 启动事务
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {

                    foreach (SelectedObject so in ss)
                    {
                        if (so != null)
                        {
                            // 3. 打开选中的实体
                            Entity ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;

                            if (ent != null)
                            {
                                // 示例：获取实体类型和图层
                                string info = $"类型: {ent.GetType().Name}, 图层: {ent.Layer}";
                                ed.WriteMessage("\n选中实体: " + info);

                                // 判断是否为单行文字 (DBText)
                                if (ent is DBText dbText)
                                {
                                    string strText = dbText.TextString;
                                    _listTextContents.Add(strText);
                                    ed.WriteMessage($"\n发现单行文字: {strText}");
                                }
                                // 判断是否为多行文字 (MText)
                                else if (ent is MText mText)
                                {
                                    string strText = mText.Contents;
                                    _listTextContents.Add(strText);
                                    ed.WriteMessage($"\n发现多行文字: {strText}");
                                }
                            }
                        }
                    }
                    tr.Commit();
                }
            }
        }
        private Dictionary<string, double> GetNeedListContexts(List<string> listContext)
        {
            Dictionary<string, double> dictContextsNeed = new Dictionary<string, double>();
            List<string> listDistinct = listContext.Distinct().ToList();

            foreach (string str in listDistinct)
            {
                if (str.Contains(":"))
                {
                    string separator = ":";
                    string[] parts = str.Split(new string[] { separator }, StringSplitOptions.None);
                    if (parts.Length > 1 && int.TryParse(parts[1], out int secondNumber))
                    {
                        dictContextsNeed.Add("Scale", secondNumber);
                    }
                }
                else if (str.Contains("X"))
                {
                    string separator = "X";
                    string[] parts = str.Split(new string[] { separator }, StringSplitOptions.None);
                    if (parts.Length > 1 && int.TryParse(parts[0], out int firstNumber))
                    {
                        dictContextsNeed.Add("dXLength", firstNumber);
                    }
                    if (parts.Length > 1 && int.TryParse(parts[1], out int secondNumber))
                    {
                        dictContextsNeed.Add("dYLength", secondNumber);
                    }
                }
                else if (Regex.IsMatch(str, @"[a-zA-Z]")) 
                {
                    dictContextsNeed.Add(str/*Name*/, 0);
                }

                if (str.Contains("\u0084"))
                {
                    string separator = "\u0084"; // 定义分隔符

                    // 1. 分割字符串
                    string[] parts = str.Split(new string[] { separator }, StringSplitOptions.None);

                    // 2. 转换为 int
                    if (parts.Length > 0 && int.TryParse(parts[0], out int firstNumber))
                    {
                        dictContextsNeed.Add("iXMainRebarNum", firstNumber);
                    }

                    if (parts.Length > 1 && int.TryParse(parts[1], out int secondNumber))
                    {
                        dictContextsNeed.Add("dXMainRebarDiameter", secondNumber);
                    }
                }

                if (str.Contains("%%132"))
                {
                    string separator = "%%132";
                    
                    if (str.StartsWith("4"))
                    {
                        dictContextsNeed.Add("eRebarType", 0);//根据separator做判断，钢筋类型
                        string[] parts = str.Split(new string[] { separator }, StringSplitOptions.None);
                        if (parts.Length > 0 && int.TryParse(parts[0], out int firstNumber))
                        {
                            dictContextsNeed.Add("iCornerRebarNum", firstNumber);
                        }
                        if (parts.Length > 0 && int.TryParse(parts[1], out int secondNumber))
                        {
                            dictContextsNeed.Add("dCornerRebarDiameter", secondNumber);
                        }
                    }

                    else if (str.StartsWith("%%132"))
                    {
                        string[] parts = str.Split(new string[] { separator }, StringSplitOptions.None);
                        if (parts.Length > 0)
                        {
                            var strTemp = parts[1];
                            string[] strParts = strTemp.Split(new string[] { "@" }, StringSplitOptions.None);
                            if (strParts.Length > 0 && int.TryParse(strParts[0], out int firstNumber))
                            {
                                dictContextsNeed.Add("dStirrupRebarDiameter", firstNumber);
                            }
                            if (strParts.Length > 0 && int.TryParse(strParts[1], out int secondNumber))
                            {
                                dictContextsNeed.Add("dStirrupRebarGAP", secondNumber);
                            }
                        }
                    }
                    else
                    {
                        string[] parts = str.Split(new string[] { separator }, StringSplitOptions.None);
                        if (parts.Length > 0 && int.TryParse(parts[0], out int firstNumber))
                        {
                            dictContextsNeed.Add("iYMainRebarNum", firstNumber);
                        }

                        if (parts.Length > 1 && int.TryParse(parts[1], out int secondNumber))
                        {
                            dictContextsNeed.Add("dYMainRebarDiameter", secondNumber);
                        }
                    }
                }
            }
            return dictContextsNeed;
        }

        private double CaculateTotalRebarWeight(Dictionary<string, double> dictContexts)
        {
            // 辅助函数：根据直径(mm)计算每米重量(kg/m)
            // var iRebarDensity = 7850;  
            // 公式: W = d*d*0.00617
            Func<double, double> GetUnitWeight = (diameterMm) =>
            {
                if (diameterMm <= 0) return 0;
                return diameterMm * diameterMm * 0.00617;
            };

            //主筋重量
            var dTotalMainRebarWeight = 
                dictContexts["iCornerRebarNum"] * GetUnitWeight(dictContexts["dCornerRebarDiameter"]) +
                dictContexts["iYMainRebarNum"] * GetUnitWeight(dictContexts["dYMainRebarDiameter"]) +
                dictContexts["iXMainRebarNum"] * GetUnitWeight(dictContexts["dXMainRebarDiameter"]);

            //箍筋长度
            var dTotalStirrupLength =
                /*dOuterStirrupLength*/
                2 * dictContexts["dXLength"] +
                2 * dictContexts["dYLength"] -
                35 * 4 +
                2 * 12 * dictContexts["dStirrupRebarDiameter"] +
                /*dXStirrupLength*/
                /*短边总长200mm 内圈肢数2*/
                200 +
                2 * (dictContexts["dYLength"] - 35 * 2) +
                ((2 + 1) / 2) * 2 * 12 * dictContexts["dStirrupRebarDiameter"] +
                /*dYStirrupLength*/
                /*短边总长200mm 内圈肢数2*/
                200 +
                2 * (dictContexts["dXLength"] - 35 * 2) +
                ((2 + 1) / 2) * 2 * 12 * dictContexts["dStirrupRebarDiameter"];
            //箍筋重量
            double dTotalStirrupWeight = GetUnitWeight(dictContexts["dStirrupRebarDiameter"]) * (dTotalStirrupLength / 1000) * 10;

            var dTotalRebarWeight = dTotalMainRebarWeight + dTotalStirrupWeight;//总钢筋重量
            return dTotalRebarWeight;
        }
        //混凝土重量 不一定使用
        private double CaculateTotalConcreteWeight(double dRebarVolume = 0, double dXlength = 0, double dYlength = 0, double dConcreteDensity = 0)
        {
            double dTotalConcreteWeight = 0;//混凝土重量
            var dVolume = dXlength / 1000 * dYlength / 1000 * 1 - dRebarVolume;
            dTotalConcreteWeight = dVolume * dConcreteDensity;//混凝土重量 per height = 1m
            return dTotalConcreteWeight;
        }

        private void DrawColumn(Dictionary<string, double> dict)
        {
            Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor editor = doc.Editor;
            using (DocumentLock acLckDoc = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // 获取模型空间
                        BlockTable blockTable = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord btr = tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                        // 柱参数（根据图纸：800x1200，保护层25mm）
                        double colWidth = 800;
                        double colHeight = 1200;
                        double cover = 25;
                        Point3d startPoint3d = new Point3d(1000, 1000, 0);  // 左下角点

                        // 1. 绘制柱轮廓
                        DrawColumnOutline(tr, btr, startPoint3d, colWidth, colHeight);

                        //// 2. 绘制主筋（40根φ28）
                        //List<Point3d> barPositions = CalculateMainBarPositions(
                        //    origin, colWidth, colHeight, cover,
                        //    totalBars: 40, barDiameter: mainBarDiam);
                        //DrawMainBars(btr, barPositions, mainBarDiam);

                        //// 3. 绘制外箍φ12
                        //double stirrupDiam = 12;
                        DrawOuterStirrupRebar(tr, btr, startPoint3d, colWidth - cover, colHeight - cover, cover);

                        //// 4. 绘制尺寸标注
                        //DrawDimensionLines(btr, startPoint3d, colWidth, colHeight);

                        //// 5. 绘制钢筋标注（引线+文字）
                        //DrawReinforcementLabel(btr, "4@28", new Point3d(100, 100, 0), new Point3d(200, 80, 0));
                        //DrawReinforcementLabel(btr, "Φ12@100", new Point3d(400, 1150, 0), new Point3d(550, 1200, 0));
                        //DrawReinforcementLabel(btr, "11@28", new Point3d(750, 600, 0), new Point3d(900, 600, 0));

                        tr.Commit();
                        editor.WriteMessage("\nKZ1柱截面绘制完成！");
                    }
                    catch (System.Exception ex)
                    {
                        editor.WriteMessage($"\n绘制出错：{ex.Message}");
                        tr.Abort();
                    }
                }
            }
            
        }
        private void DrawColumnOutline(Transaction tr, BlockTableRecord btr, Point3d startPoint3d, double colWidth, double colHeight , double cover)
        {
            Polyline pline = new Polyline();
            // ... 添加顶点逻辑 ...
            pline.AddVertexAt(0, new Point2d(startPoint3d.X + cover, startPoint3d.Y), 0, 0, 0);
            pline.AddVertexAt(1, new Point2d(startPoint3d.X + colHeight, startPoint3d.Y), 0, 0, 0);
            pline.AddVertexAt(2, new Point2d(startPoint3d.X + colHeight, startPoint3d.Y + colWidth), 0, 0, 0);
            pline.AddVertexAt(3, new Point2d(startPoint3d.X, startPoint3d.Y + colWidth), 0, 0, 0);
            pline.Closed = true;
            btr.AppendEntity(pline);
            tr.AddNewlyCreatedDBObject(pline, true); // 关键：加入当前事务
        }
        private void DrawOuterStirrupRebar(Transaction tr, BlockTableRecord btr, Point3d startPoint3d, double colWidth, double colHeight)
        {
            Polyline pline = new Polyline();
            // ... 添加顶点逻辑 ...
            pline.AddVertexAt(0, new Point2d(startPoint3d.X, startPoint3d.Y), 0, 0, 0);
            pline.AddVertexAt(1, new Point2d(startPoint3d.X + colHeight, startPoint3d.Y), 0, 0, 0);
            pline.AddVertexAt(2, new Point2d(startPoint3d.X + colHeight, startPoint3d.Y + colWidth), 0, 0, 0);
            pline.AddVertexAt(3, new Point2d(startPoint3d.X, startPoint3d.Y + colWidth), 0, 0, 0);
            // ... 钩子部分四个点
            Polyline plineHook = new Polyline();
            var newPoint3d = new Point3d(startPoint3d.X + colHeight, startPoint3d.Y + colWidth, 0);
            var dLength = 12*12/*箍筋直径*/;
            plineHook.AddVertexAt(0, new Point2d(newPoint3d.X - 0.4 * dLength - 0.6 * dLength * 0.7, newPoint3d.Y - 0.6 * dLength * 0.7), 0, 0, 0);
            plineHook.AddVertexAt(1, new Point2d(newPoint3d.X - 0.4 * dLength, newPoint3d.Y), 0, 0, 0);
            plineHook.AddVertexAt(2, new Point2d(newPoint3d.X - 0.6 * dLength * 0.7, newPoint3d.Y - 0.4 * dLength - 0.6 * dLength * 0.7), 0, 0, 0);
            plineHook.AddVertexAt(3, new Point2d(newPoint3d.X, newPoint3d.Y - 0.4 * dLength), 0, 0, 0);

            pline.Closed = true;
            plineHook.Closed = true;
            btr.AppendEntity(pline);
            btr.AppendEntity(plineHook);
            tr.AddNewlyCreatedDBObject(pline, true); // 关键：加入当前事务
        }
        private void DrawXStirrupRebar()
        {
            //随着X，Y尺寸的变化，会不会有变化
        }
        private void DrawYStirrupRebar()
        {
        }
        private void DrawXRebar()
        { 
        }
        private void DrawYRebar()
        {
        }
        private void DrawCornerRebar()
        {
        }



        private void DrawRebarInColumnSheet()
        {

        }
    }
}
