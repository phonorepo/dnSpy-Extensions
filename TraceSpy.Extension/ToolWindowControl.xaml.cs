using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static TraceSpy.Extension.Tracers;

namespace TraceSpy.Extension
{
    public partial class ToolWindowControl : UserControl
    {
        Tracers.TraceSpy TSpy;
        bool AutoScroll = true;
        bool AutoResize = true;
        //long AutoResizeElapsedTicks = 0;
        Regex rPID = new Regex("[^0-9]+");
        private int pid = 0;

        public ToolWindowControl()
        {
            InitializeComponent();
        }


        private void ProcessFilterPIDInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = rPID.IsMatch(e.Text);
        }

        private void TraceSpyFilterPID_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox txtBoxPID = sender as TextBox;
            Int32.TryParse(txtBoxPID.Text, out pid);
            if (TSpy != null) TSpy.FilterPID = pid;
        }

        public void ListViewCollectionChanged(Object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ScrollDown(TraceSpyListView);
            if (AutoResize) AutoResizeGridViewColumn(GridViewTraceSpy);
        }

        public void ScrollDown(ListView listview)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                if (AutoScroll && listview != null && listview.Items != null && listview.Items.Count > 0)
                {
                    int pos = listview.Items.Count - 1;
                    listview.ScrollIntoView(listview.Items[pos]);
                }
            }
            ));

        }

        private void AutoResizeGridViewColumn(GridView gridView)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                //if (gridView != null && TSpy != null && TSpy.Watch.ElapsedTicks > (AutoResizeElapsedTicks + 10000000))
                if (gridView != null && TSpy != null)
                {
                    if (TSpy.Queue.Count == 1 | TSpy.Queue.Count == 3 | TSpy.Queue.Count == 10 | TSpy.Queue.Count == 100)
                    {
                        foreach (GridViewColumn column in gridView.Columns)
                        {

                            if (double.IsNaN(column.Width))
                            {
                                column.Width = column.ActualWidth;
                            }

                            column.Width = double.NaN;
                        }
                        //AutoResizeElapsedTicks = TSpy.Watch.ElapsedTicks;
                    }
                }
            }
            ));

        }


        private void startTrace()
        {
            if (TraceSpyListView != null)
            {

                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    TSpy = new Tracers.TraceSpy(TraceSpyListView);
                    TSpy.FilterPID = pid;

                    TraceSpyListView.ItemsSource = TSpy.Queue;


                    ((System.Collections.Specialized.INotifyCollectionChanged)TraceSpyListView.ItemsSource).CollectionChanged -=
                    new System.Collections.Specialized.NotifyCollectionChangedEventHandler(ListViewCollectionChanged);

                    ((System.Collections.Specialized.INotifyCollectionChanged)TraceSpyListView.ItemsSource).CollectionChanged +=
                        new System.Collections.Specialized.NotifyCollectionChangedEventHandler(ListViewCollectionChanged);

                    TSpy.StartReaderTask();
                }
              ));
            }
        }

        private void TraceSpyButton1_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Primitives.ToggleButton tButton = sender as System.Windows.Controls.Primitives.ToggleButton;

            if (tButton.IsChecked ?? false) {
                tButton.Content = "Stop";
                startTrace();
            }
            else {
                tButton.Content = "Start";

                if (TSpy == null) {
                    TSpy = new Tracers.TraceSpy(TraceSpyListView);
                }

                TSpy.Enabled = false;
            }
        }
    }
}
