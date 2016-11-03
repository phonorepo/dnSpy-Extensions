using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

#region NativeMethods
internal static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetEvent(IntPtr hEvent);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, FileMapAccess dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, IntPtr dwNumberOfBytesToMap);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpFileMappingAttributes, FileMapProtection flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);


    [Flags]
    internal enum FileMapAccess
    {
        FileMapRead = 0x0004,
    }

    [Flags]
    internal enum FileMapProtection
    {
        PageReadWrite = 0x04,
    }

}
#endregion

namespace TraceSpy.Extension
{
    #region class Tracers
    public class Tracers
    {
        #region class TraceLine
        public class TraceLine : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            private int index;
            private int pid;
            private string processName;
            private string text;
            private long ticks;

            public override string ToString()
            {
                return String.Format("{0};{1};{2};{3};{4}", index, pid, processName, text, ticks);
            }


            public int Index
            {
                get { return index; }
                set
                {
                    index = value;
                    OnPropertyChanged("Index");
                }
            }

            public int Pid
            {
                get { return pid; }
                set
                {
                    pid = value;
                    OnPropertyChanged("Pid");
                }
            }

            public string ProcessName
            {
                get { return processName; }
                set
                {
                    processName = value;
                    OnPropertyChanged("ProcessName");
                }
            }
            public string Text
            {
                get { return text; }
                set
                {
                    text = value;
                    OnPropertyChanged("Text");
                }
            }

            public long Ticks
            {
                get { return ticks; }
                set
                {
                    ticks = value;
                    OnPropertyChanged("Ticks");
                }
            }


            protected void OnPropertyChanged(string name)
            {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null)
                {
                    handler(this, new PropertyChangedEventArgs(name));
                }
            }

        }
        #endregion


        #region class TraceSpy
        public class TraceSpy : IDisposable
        {
            /// <summary>
            /// Trace Spy Written by Simon Mourier.
            /// http://www.softfluent.com
            /// </summary>

            private bool disposed;

            private object _readerLock = new object();
            public CancellationTokenSource cancelTS = new CancellationTokenSource();
            public System.Threading.Tasks.Task ReaderTask;


            void BindingOperations_CollectionRegistering(object sender, CollectionRegisteringEventArgs e)
            {
                if (e.Collection == Queue)
                {
                    BindingOperations.EnableCollectionSynchronization(Queue, _readerLock);
                }
            }


            private bool enabled = true;
            public bool Enabled
            {
                get { return enabled; }
                set { enabled = value; }
            }

            private bool stop = false;
            public bool Stop
            {
                get { return stop; }
                set { stop = value; }
            }

            private int filterPID = 0;
            public int FilterPID
            {
                get { return filterPID; }
                set { filterPID = value; }
            }

            private IntPtr _bufferReadyEvent;
            private IntPtr _dataReadyEvent;
            private IntPtr _mapping;
            private IntPtr _file;

            private Stopwatch _watch;
            private DispatcherTimer _timer;

            private int _id;
            private const int WaitTimeout = 500;
            private const uint WAIT_OBJECT_0 = 0;

            private readonly Dictionary<int, TraceLine> _queue = new Dictionary<int, TraceLine>();
            private readonly Dictionary<int, string> _processes = new Dictionary<int, string>();

            private ListView listview;
            public ObservableCollection<TraceLine> Queue = new ObservableCollection<TraceLine>();



            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            protected virtual void Dispose(bool disposing)
            {
                if (!this.disposed)
                {
                    if (disposing)
                    {
                        listview = null;
                        _timer = null;
                        _watch = null;
                    }

                    this._bufferReadyEvent = IntPtr.Zero;
                    this._dataReadyEvent = IntPtr.Zero;
                    this._mapping = IntPtr.Zero;
                    this._file = IntPtr.Zero;
                    this.cancelTS.Dispose();
                }
                disposed = true;
            }


            ~TraceSpy()
            {
                Dispose(false);
            }


            public TraceSpy(ListView ListView)
            {
                BindingOperations.CollectionRegistering += BindingOperations_CollectionRegistering;

                if (ListView != null)
                {
                    _bufferReadyEvent = NativeMethods.CreateEvent(IntPtr.Zero, false, false, "DBWIN_BUFFER_READY");
                    if (_bufferReadyEvent == IntPtr.Zero)
                        Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());

                    _dataReadyEvent = NativeMethods.CreateEvent(IntPtr.Zero, false, false, "DBWIN_DATA_READY");
                    if (_dataReadyEvent == IntPtr.Zero)
                        Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());

                    _mapping = NativeMethods.CreateFileMapping(new IntPtr(-1), IntPtr.Zero, NativeMethods.FileMapProtection.PageReadWrite, 0, 4096, "DBWIN_BUFFER");
                    if (_mapping == IntPtr.Zero)
                        Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());

                    _file = NativeMethods.MapViewOfFile(_mapping, NativeMethods.FileMapAccess.FileMapRead, 0, 0, new IntPtr(1024));
                    if (_file == IntPtr.Zero)
                        Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());

                    listview = ListView;

                    listview.ItemsSource = Queue;

                    _timer = new DispatcherTimer();

                    _watch = new Stopwatch();
                }
            }

            public void ForceStop()
            {
                if (ReaderTask != null)
                {
                    cancelTS.Cancel();
                }

            }

            public void StartReaderTask()
            {
                ReaderTask = System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    enabled = true;
                    Read();

                }, cancelTS.Token);

            }
            public void Read()
            {
                try
                {
                    do
                    {
                        if (cancelTS.Token.IsCancellationRequested)
                        {
                            //Debug.WriteLine("Request to cancel Task received.");
                            break;
                        }

                        NativeMethods.SetEvent(_bufferReadyEvent);
                        uint wait = NativeMethods.WaitForSingleObject(_dataReadyEvent, WaitTimeout);
                        if (stop)
                            return;

                        if (!Enabled)
                            continue;

                        if (wait == WAIT_OBJECT_0) // we don't care about other return values
                        {
                            int pid = Marshal.ReadInt32(_file);
                            string text = Marshal.PtrToStringAnsi(new IntPtr(_file.ToInt64() + Marshal.SizeOf(typeof(int)))).TrimEnd(null);
                            if (string.IsNullOrEmpty(text))
                                continue;

                            TraceLine line = new TraceLine();
                            line.Index = _id++;
                            line.Pid = pid;
                            line.ProcessName = GetProcessName(line.Pid);
                            line.Text = text;

                            if (!_watch.IsRunning)
                            {
                                _watch.Start();
                                // small hack; we ensure the first has 0 ticks
                                line.Ticks = 0;
                            }
                            else
                            {
                                line.Ticks = _watch.ElapsedTicks;
                            }

                            if (FilterPID > 0 && pid == FilterPID)
                            {
                                Queue.Add(line);
                            }
                            else if (FilterPID == 0)
                            {
                                Queue.Add(line);
                            }
                        }
                    }
                    while (true);
                }
                catch
                {
                }
            }




            private string GetProcessName(int id, bool IdOnly = false)
            {
                if (IdOnly)
                    return id.ToString();

                string name;
                if (!_processes.TryGetValue(id, out name))
                {
                    try
                    {
                        Process process = Process.GetProcessById(id);
                        name = process.ProcessName;
                    }
                    catch
                    {
                    }

                    if (name == null)
                    {
                        name = id.ToString();
                    }
                    _processes[id] = name;
                }
                return name;
            }


        } // End TraceSpy
        #endregion

    }
    #endregion
}
