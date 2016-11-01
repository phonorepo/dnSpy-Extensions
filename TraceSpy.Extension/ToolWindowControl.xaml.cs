using System;
using System.Windows;
using System.Windows.Controls;
using static TraceSpy.Extension.Tracers;

namespace TraceSpy.Extension {
	public partial class ToolWindowControl : UserControl
    {
        Tracers.TraceSpy TSpy;
        bool AutoScroll = true;

        public ToolWindowControl() {
			InitializeComponent();


        }

        public void ListViewCollectionChanged(Object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            ScrollDown(TraceSpyListView);
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

        private void startTrace()
        {
            if (TraceSpyListView != null)
            {

                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    TSpy = new Tracers.TraceSpy(TraceSpyListView);

                    
                    TraceSpyListView.ItemsSource = TSpy.Queue;

                    
                    ((System.Collections.Specialized.INotifyCollectionChanged)TraceSpyListView.ItemsSource).CollectionChanged -=
                    new System.Collections.Specialized.NotifyCollectionChangedEventHandler(ListViewCollectionChanged);

                    ((System.Collections.Specialized.INotifyCollectionChanged)TraceSpyListView.ItemsSource).CollectionChanged +=
                        new System.Collections.Specialized.NotifyCollectionChangedEventHandler(ListViewCollectionChanged);

                    TSpy.StartReaderTask();
                }
              ));
            }
            else
            {
                MessageBox.Show("TraceSpyListView == null");
            }
        }

        private void TraceSpyButton1_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Primitives.ToggleButton tButton = sender as System.Windows.Controls.Primitives.ToggleButton;

            if (tButton.IsChecked ?? false)
            {
                tButton.BorderThickness = new Thickness(4, 4, 4, 4);
                tButton.Padding = new Thickness(4, 4, 4, 4);
                tButton.Content = "Stop";

                startTrace();
            }
            else
            {
                tButton.BorderThickness = new Thickness(1, 1, 1, 1);
                tButton.Padding = new Thickness(0, 0, 0, 0);
                tButton.Content = "Start";

                if (TSpy == null)
                {
                    TSpy = new Tracers.TraceSpy(TraceSpyListView);
                }

                TSpy.Enabled = false;
            }
        }
    }
}
