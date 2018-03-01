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
// add 0227
using Bespoke.Common;
using Bespoke.Common.Osc;
using Microsoft.Kinect.Toolkit.FaceTracking;

namespace faceTracking
{
    static class KinectExtentions
    {
        public static byte[] ToPixelData(this ColorImageFrame colorFrame)
        {
            byte[] pixels = new byte[colorFrame.PixelDataLength];
            colorFrame.CopyPixelDataTo(pixels);
            return pixels;
        }


        public static short[] ToPixelData(this DepthImageFrame depthFrame)
        {
            short[] depth = new short[depthFrame.PixelDataLength];
            depthFrame.CopyPixelDataTo(depth);
            return depth;
        }


        public static Skeleton[] ToSkeletonData(this SkeletonFrame sFrame)
        {
            Skeleton[] skeletons = new Skeleton[sFrame.SkeletonArrayLength];
            sFrame.CopySkeletonDataTo(skeletons);
            return skeletons;
        }

        // trackedなスケルトンのSkeletonDataを返す
        public static IEnumerable<Skeleton> GetTrackedSkeletons(this SkeletonFrame skeletonFrame)
        {
            return from s in skeletonFrame.ToSkeletonData()
                   where s.TrackingState == SkeletonTrackingState.Tracked
                   select s;
        }
    }

    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        // make val to put kinectsensor, faceTracker instances
        // アクセス修飾子のdefault(書かなかった場合)はprivateと同じ
        KinectSensor kinect;
        FaceTracker faceTracker;

        public MainWindow()
        {
            // 色々よしなにしてくれるおまじない.
            InitializeComponent();

            try
            {
                // kinect is connected?
                if (KinectSensor.KinectSensors.Count == 0)
                {
                    // 例外処理
                    MessageBox.Show("No KinectSensors ditected");
                    Close();
                }
                // 1個目のKinectのKinectsensor instanceを取得, kinectに格納
                // 複数のときはforeach文が便利. その場合はthisを使う(?)
                kinect = KinectSensor.KinectSensors[0];

                // Color,Depth,Skeltonを有効化
                kinect.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                kinect.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                kinect.SkeletonStream.Enable();
                // kinect.ColorStream.Enable(ColorImageFormat.InfraredResolution640x480Fps30);

                // すべての情報の更新について実行されるイベント
                kinect.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(kinect_AllFramesReady);

                // 動作開始
                kinect.Start();

                // 顔追跡用インスタンス生成
                faceTracker = new FaceTracker(kinect);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Close();
            }
        }

        /// <summary>
        /// Frames更新で実行されるイベント(eの中に更新されたデータを格納)
        /// kinectのメインループ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void kinect_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                // Image部品にRGB表示
                if (colorFrame != null)
                {
                    imageRgbCamera.Source = colorFrame.ToBitmapSource();
                }

                // Skeletonが取れているとき表示
                if (skeletonFrame !=  null)
                {
                    foreach ( var skeleton in skeletonFrame.GetTrackedSkeletons() )
                    {
                        ShowSkeleton(skeleton);
                    }
                }

                // RGB, Depth, Skeltonフレームが取得できたら, 追跡されているSkeltonに対して顔認識
                if ( (colorFrame != null) && (depthFrame != null) && (skeletonFrame != null) )
                {
                    foreach ( var skeleton in skeletonFrame.GetTrackedSkeletons() )
                    {
                        FaceDataAcquisition(colorFrame, depthFrame, skeleton);
                    }
                }
            }
        }

        /// <summary>
        /// 骨格表示
        /// </summary>
        /// <param name="skeletonFrame"></param>
        private void ShowSkeleton(Skeleton skeleton)
        {
            // clear canvas
            canvasSkeleton.Children.Clear();
            
            // draw skeleton
            foreach ( Joint joint in skeleton.Joints)
            {
                //tracked skeleton
                if ( joint.TrackingState != JointTrackingState.NotTracked )
                {
                    // 骨格座標をカラー座標に変換
                    ColorImagePoint point = kinect.CoordinateMapper.MapSkeletonPointToColorPoint(joint.Position, kinect.ColorStream.Format);

                    // draw circles
                    canvasSkeleton.Children.Add(new Ellipse() {
                        Margin = new Thickness( point.X, point.Y, 0, 0 ),
                        Fill = new SolidColorBrush( Colors.Black ),
                        Width = 20,
                        Height = 20,
                    });
                }
            }

        }


        /// <summary>
        /// 顔を認識し位置と回転のパラメータを取得
        /// </summary>
        /// <param name="colorFrame"></param>
        /// <param name="depthFrame"></param>
        /// <param name="skeletonFrame"></param>
        private void FaceDataAcquisition(ColorImageFrame colorFrame, DepthImageFrame depthFrame, Skeleton skeleton)
        {
            var faceFrame = faceTracker.Track(colorFrame.Format, colorFrame.ToPixelData(),
                depthFrame.Format, depthFrame.ToPixelData(), skeleton);
        }

    }
}
