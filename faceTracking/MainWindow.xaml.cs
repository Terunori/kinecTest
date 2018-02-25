using System;
using System.Collections.Generic;
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

// add 0225
using Microsoft.Kinect;
using Coding4Fun.Kinect.Wpf;

namespace faceTracking
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        // make val to put kinectsensor instances
        private KinectSensor kinect;

        public MainWindow()
        {
            // 色々よしなにしてくれるおまじない.
            InitializeComponent();

            // kinect is connect?
            if ( KinectSensor.KinectSensors.Count == 0)
            {
                // 例外処理
                MessageBox.Show("No KinectSensors ditected");
                Close();
            }
            // 1個目のKinectのKinectsensor instanceを取得, kinectに格納
            kinect = KinectSensor.KinectSensors[0];

            // RGBカメラ有効化
            kinect.ColorStream.Enable();

            // 骨格検出を有効化
            // kinect.SkeletonStream.Enable();

            // RGBカメラの情報取得時に実行
            kinect.ColorFrameReady += new EventHandler<ColorImageFrameReadyEventArgs>(kinect_ColorFrameReady);
            // すべての情報の更新について実行されるイベント
            // kinect.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(kinect_ColorFrameReady);

            // 動作開始
            kinect.Start();
        }

        // カメラ更新で実行されるイベント(eの中に更新されたデータを格納)
        void kinect_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            // eからRGB画像データを取り出しビットマップに変換、そしてImage部品(MainWindow.xamlに記入)に表示
            rgbCamera.Source = e.OpenColorImageFrame().ToBitmapSource();
        }
    }
}
