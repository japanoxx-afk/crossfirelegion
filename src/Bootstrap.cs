using System;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
class Bootstrap {
 [STAThread] static void Main() {
  try {
   string root=AppDomain.CurrentDomain.BaseDirectory;
   string version=File.ReadAllText(Path.Combine(root,"current.txt")).Trim();
   Version.Parse(version);
   string exe=Path.Combine(root,"versions",version,"Application.exe");
   Process.Start(new ProcessStartInfo(exe,"\""+root.TrimEnd('\\')+"\""){WorkingDirectory=Path.GetDirectoryName(exe)});
  } catch(Exception e){MessageBox.Show(e.Message,"런처 실행 실패");}
 }
}
