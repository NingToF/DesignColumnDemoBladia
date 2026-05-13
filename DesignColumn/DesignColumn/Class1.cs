using System;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

namespace DesignColumn
{
    public class Class1
    {
        // 添加静态字段来保持窗体引用，防止被垃圾回收
        private static MyModelessForm _modelessForm = null;

        // 定义 AutoCAD 命令
        [CommandMethod("bladia")]
        public void ShowModelessForm()
        {
            // 检查窗体是否已存在且未被销毁
            if (_modelessForm == null || _modelessForm.IsDisposed)
            {
                // 创建窗体实例
                _modelessForm = new MyModelessForm();

                // 可选：关闭窗体时清空引用，以便下次重新创建
                _modelessForm.FormClosed += (s, e) =>
                {
                    _modelessForm = null;
                };
            }

            // 关键：使用 Show() 方法显示非模态窗体
            // 注意：在 AutoCAD 中，直接 Show() 通常是可行的，但为了更好的集成，
            // 有时建议使用 Application.ShowModelessDialog 如果涉及特定的 UI 线程问题，
            // 但对于标准 WinForm，Show() 是最直接的非模态方式。

            // 为了确保窗体在 AutoCAD 失去焦点时不会隐藏，可以设置以下属性（可选）
            _modelessForm.ShowInTaskbar = true;

            // 显示窗体
            _modelessForm.Show();

            // 显示窗体
            // 方法 A: 使用标准的 Show() - 简单，但可能不会始终保持在 AutoCAD 顶部
            // _modelessForm.Show();

            // 方法 B (推荐): 使用 AutoCAD 专用的 ShowModelessDialog
            // 这能更好地处理与 AutoCAD 主窗口的交互和焦点问题
            // Application.ShowModelessDialog(_modelessForm);

            // 激活窗体，确保它获得焦点
            // _modelessForm.Activate();
        }
    }
}