using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace ProcessManipulation
{
    public partial class Form1 : Form
    {
        //константа, идентифицирующая сообщение WM _ SETTEXT
        const uint WM_SETTEXT = 0x0C;
        //импортируем функцию SendMEssage из библиотеки
        //user32.dll
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hwnd,
                                                uint Msg,
                                                int wParam,
                                                [MarshalAs(UnmanagedType.LPStr)] string lParam);
        /*список, в котором будут храниться объекты,
        * описывающие дочерние процессы приложения*/
        List<Process> Processes = new List<Process>();
        /*счётчик запущенных процессов*/
        int Counter = 0;
        public Form1()
        {
            InitializeComponent();
            LoadAvailableAssemblies();
        }
        void LoadAvailableAssemblies()
        {
            //название файла сборки текущего приложения
            string except = new FileInfo(Application.ExecutablePath).Name;
            //получаем название файла без расширения
            except = except.Substring(0, except.IndexOf("."));
            //получаем все *.exe файлы из домашней
            //директории
            string[] files = Directory.GetFiles(Application.StartupPath, "*.exe");
            foreach (var file in files)
            {
                //получаем имя файла
                string fileName = new FileInfo(file).Name;
                /*если имя файла не содержит имени исполняемого
                *файла проекта, то оно добавляется в список*/
                if (fileName.IndexOf(except) == -1)
                    AvailableAssemblies.Items.Add(fileName);

            }
        }

        /*метод, запускающий процесс на исполнение и
        * сохраняющий объект, который его описывает*/
        void RunProcess(string AssamblyName)
        {
            //запускаем процесс на соновании исполняемого файла
            Process proc = Process.Start(AssamblyName);
            //добавляем процесс в список
            Processes.Add(proc);
            /*проверяем, стал ли созданный процесс дочерним,
             *по отношению к текущему и, если стал, выводим
             *MessageBox*/
            if (Process.GetCurrentProcess().Id == GetParentProcessId(proc.Id))
                MessageBox.Show(proc.ProcessName + " действительно дочерний процесс текущего процесса!");
            /*указываем, что процесс должен генерировать события*/

            proc.EnableRaisingEvents = true;
            //добавляем обработчик на событие завершения процесса
            proc.Exited += proc_Exited;
            /*устанавливаем новый текст главному окну
             *дочернего процесса*/
            SetChildWindowText(proc.MainWindowHandle, "Child process #" + (++Counter));
            /*проверяем, запускали ли мы экземпляр такого
            *приложения и, если нет, то добавляем в список
            *запущенных приложений*/
            if (!StartedAssemblies.Items.Contains(proc.ProcessName))
                StartedAssemblies.Items.Add(proc.ProcessName);
            /*убираем приложение из списка доступных
             *приложений*/
            AvailableAssemblies.Items.Remove(AvailableAssemblies.SelectedItem);
        }
        /*метод, получающий PID родительского процесса
*(использует WMI)*/
        int GetParentProcessId(int Id)
        {
            int parentId = 0;
            using (ManagementObject obj =
            new ManagementObject("win32_process.handle=" +
            Id.ToString()))
            {
                obj.Get();
                parentId = Convert.
                ToInt32(obj["ParentProcessId"]);
            }
            return parentId;
        }

        /*метод обёртывания для отправки сообщения
        *WM _ SETTEXT*/
        void SetChildWindowText(IntPtr Handle, string text)
        {
            SendMessage(Handle, WM_SETTEXT, 0, text);
        }

        /*обработчик события Exited класса Process*/
        void proc_Exited(object sender, EventArgs e)
        {
            Process proc = sender as Process;
            ////убираем процесс из списка запущенных //приложений
            //StartedAssemblies.Items.Remove(proc.ProcessName);
            //убираем процесс из списка дочерних процессов
            Processes.Remove(proc);
            //уменьшаем счётчик дочерних процессов на 1
            Counter--;
            int index = 0;
            /*меняем текст для главных окон всех дочерних *процессов*/
            foreach (var p in Processes)
                SetChildWindowText(p.MainWindowHandle, "Child process #" + ++index);
        }

        //объявление делегата, принимающего параметр типа
        //Process
        delegate void ProcessDelegate(Process proc);
        /*метод, который выполняет проход по всем дочерним
        *процессам с заданым именем и выполняющий для этих
        *процессов заданый делегатом метод*/
        void ExecuteOnProcessesByName(string ProcessName, ProcessDelegate func)
        {
            /*получаем список, запущенных в операционной
             *системе процессов*/
            Process[] processes = Process.GetProcessesByName(ProcessName);
            foreach (var process in processes)
                /*если PID родительского процесса равен PID
                 *текущего процесса*/
                if (Process.GetCurrentProcess().Id == GetParentProcessId(process.Id))
                    //запускаем метод
                    func(process);
        }

        void Kill(Process proc)
        {
            proc.Kill();
        }

        void CloseMainWindow(Process proc)
        {
            proc.CloseMainWindow();
        }

        void RefreshMethod(Process proc)
        {
            proc.Refresh();
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            RunProcess(AvailableAssemblies.SelectedItem.ToString());
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            string nameProcc = StartedAssemblies.SelectedItem.ToString();
            ExecuteOnProcessesByName(StartedAssemblies.SelectedItem.ToString(), Kill);
            StartedAssemblies.Items.Remove(nameProcc);
            AvailableAssemblies.Items.Add(nameProcc);

        }

        private void CloseWindowButton_Click(object sender, EventArgs e)
        {
            ExecuteOnProcessesByName(StartedAssemblies.SelectedItem.ToString(), CloseMainWindow);
            StartedAssemblies.Items.Remove(StartedAssemblies.SelectedItem);
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            ExecuteOnProcessesByName(StartedAssemblies.SelectedItem.ToString(), RefreshMethod);
        }

        private void RunNotepadButton_Click(object sender, EventArgs e)
        {
            RunProcess("notepad.exe");
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach (var proc in Processes)
                proc.Kill();
        }

        private void StartedAssemblies_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (StartedAssemblies.SelectedItems.Count == 0)
            {
                StopButton.Enabled = false;
                //CloseButton.Enabled = false;
                CloseWindowButton.Enabled = false;
            }
            else
            {
                StopButton.Enabled = true;
                //CloseButton.Enabled = true;
                CloseWindowButton.Enabled = true;
            }
        }

        private void AvailableAssemblies_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (AvailableAssemblies.SelectedItems.Count == 0)
                StartButton.Enabled = false;
            else
                StartButton.Enabled = true;
        }
    }
}
