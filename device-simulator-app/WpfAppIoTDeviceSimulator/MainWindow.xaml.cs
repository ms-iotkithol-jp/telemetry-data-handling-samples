using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WpfAppIoTDeviceSimulator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            rand = new Random(DateTime.Now.Millisecond);
        }

        void ShowLog(string log)
        {
            var sb = new StringBuilder();
            var writer = new StringWriter(sb);
            writer.WriteLine($"{DateTime.Now.ToString("yyyyMMdd-HHmmss")}: {log}");
            writer.Write(tbLog.Text);
            tbLog.Text = sb.ToString();
        }

        DeviceClient deviceClient;
        Random rand;
        string deviceId;

        private async void buttonConnect_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbIoTHubCS.Text))
            {
                MessageBox.Show("Please set IoT Hub Device Connection String!");
                return;
            }
            deviceClient = DeviceClient.CreateFromConnectionString(tbIoTHubCS.Text);
            await deviceClient.OpenAsync();
            var csBuilder = IotHubConnectionStringBuilder.Create(tbIoTHubCS.Text);
            deviceId = csBuilder.DeviceId;
            ShowLog($"Connected to IoT Hub as {deviceId}");
            buttonSendingControl.IsEnabled = true;
        }

        double currentValue = 0.0f;

        private double CalclateCurrentValue()
        {
            double targetValue = double.Parse(tbTargetValue.Text);
            double coef = double.Parse(tbCoef.Text);
            double wnr = double.Parse(tbWNR.Text);
            double delta = (targetValue - currentValue) * coef;
            double noise = (rand.NextDouble() - 0.5) * wnr;
            currentValue += (delta + wnr);
            tbCurrentValue.Text = $"{currentValue}";
            return currentValue;
        }

        DispatcherTimer timer = null;
        private void buttonSendingControl_Click(object sender, RoutedEventArgs e)
        {
            if (timer == null)
            {
                timer = new DispatcherTimer();
                timer.Tick += Timer_Tick;
            }
            if (timer.IsEnabled)
            {
                timer.Stop();
                ShowLog("Stop sending");
                buttonSendingControl.Content = "Send Start";
            }
            else
            {
                currentValue = double.Parse(tbInitValue.Text);
                timer.Interval = TimeSpan.FromMilliseconds(int.Parse(tbSendInterval.Text));
                timer.Start();
                ShowLog("Start sending");
                buttonSendingControl.Content = "Send Stop";
            }
        }

        private async void Timer_Tick(object sender, EventArgs e)
        {
            var msg = $"\"deviceid\":\"{deviceId}\",\"{tbPropName.Text}\":{CalclateCurrentValue()},\"timestamp\":\"{DateTime.UtcNow.ToString("yyyy/MM/ddTHH:mm:ss.fffZ")}\"";
            msg = "{" + msg + "}";
            var msgBytes = System.Text.Encoding.UTF8.GetBytes(msg);
            var iothubMsg = new Message(msgBytes);
            await deviceClient.SendEventAsync(iothubMsg);
            ShowLog($"Message Send - {msg},{msgBytes.Length} bytes");
        }
    }
}
