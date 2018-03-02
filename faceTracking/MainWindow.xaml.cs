using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
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

        // 3次元ベクトル
        public struct Vector3DF
        {
            public float x, y, z;
        }

        // 回転角や位置を格納する
        private Vector3DF rotHeadXYZ = new Vector3DF();
        private Vector3DF posHeadXYZ = new Vector3DF();
        private Vector3DF posHandRightXYZ = new Vector3DF();
        private Vector3DF posHandLeftXYZ = new Vector3DF();

        // 送信元・送信先のネットワークエンドポイント
        private IPEndPoint from = null;
        private IPEndPoint toExp = null;
        // private IPEndpoint toListener = null;

        // 送信元・送信先のポート
        private int portFrom = 7777;
        private int portTo = 1234;

        /// <summary>
        /// Face rotation display angle increment in degrees
        /// </summary>
        // const double FaceRotationIncrementInDegrees = 0.1;

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
                kinect.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(Kinect_AllFramesReady);

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

        /*
        /// <summary>
        /// Converts rotation quaternion to Euler angles
        /// And then maps them to a specified range of values to control the refresh rate
        /// v1(v1.5-v1.8)ではQuaternion使えない？？
        /// </summary>
        /// <param name="rotQuaternion">face rotation quaternion</param>
        /// <param name="pitch">rotation about the X-axis</param>
        /// <param name="yaw">rotation about the Y-axis</param>
        /// <param name="roll">rotation about the Z-axis</param>
        private static void ExtractFaceRotationInDegrees(double qx, double qy, double qz, double qw, out double pitch, out double yaw, out double roll)
        {
            double x = qx;
            double y = qy;
            double z = qz;
            double w = qw;

            double pitchD, yawD, rollD;

            // convert face rotation quaternion to Euler angles in degrees

            pitchD = Math.Atan2(2 * ((y * z) + (w * x)), (w * w) - (x * x) - (y * y) + (z * z)) / Math.PI * 180.0;
            yawD = Math.Asin(2 * ((w * y) - (x * z))) / Math.PI * 180.0;
            rollD = Math.Atan2(2 * ((x * y) + (w * z)), (w * w) + (x * x) - (y * y) - (z * z)) / Math.PI * 180.0;

            // clamp the values to a multiple of the specified increment to control the refresh rate
            double increment = FaceRotationIncrementInDegrees;
            pitch = (double)(Math.Floor((pitchD + ((increment / 2.0) * (pitchD > 0 ? 1.0 : -1.0))) / increment) * increment);
            yaw = (double)(Math.Floor((yawD + ((increment / 2.0) * (yawD > 0 ? 1.0 : -1.0))) / increment) * increment);
            roll = (double)(Math.Floor((rollD + ((increment / 2.0) * (rollD > 0 ? 1.0 : -1.0))) / increment) * increment);
        }
        */

        /// <summary>
        /// Frames更新で実行されるイベント(eの中に更新されたデータを格納)
        /// kinectのメインループ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Kinect_AllFramesReady(object sender, AllFramesReadyEventArgs e)
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
        /// 初期化処理
        /// WPFを開いた直後に実行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // ソケットの生成
            from = new IPEndPoint(IPAddress.Loopback, portFrom);
            toExp = new IPEndPoint(IPAddress.Loopback, portTo);
            // from = new IPEndPoint(IPAddress.Parse("192.168.11.24"), portFrom);

            // 送信先のアドレスとポート番号の表示
            sendTextBlock.Text = "Target: " + toExp.Address.ToString() + ':' + toExp.Port.ToString();
            // SendLisnerTextBlock.Text = "送信先(受聴者): " + toLisner.Address.ToString() + ':' + toLisner.Port.ToString();
            
            // IPPortUpdateボタンが押されたら更新
            IPPortUpdate.Click += new RoutedEventHandler(this.IPPortUpdate_Click);
        }

        /// <summary>
        /// IPPortUpdateボタンが押された
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void IPPortUpdate_Click(object sender, RoutedEventArgs e)
        {
            IPAddress IPnew = IPAddress.Parse(iPUpdate.Text);
            int Portnew = Int32.Parse(portUpdate.Text);

            toExp = new IPEndPoint(IPnew, Portnew);
            sendTextBlock.Text = "Target: " + toExp.Address.ToString() + ':' + toExp.Port.ToString();
        }


        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if ( faceTracker != null)
            {
                faceTracker.Dispose();
                faceTracker = null;
            }

            if (kinect != null)
            {
                kinect.Stop();
                kinect = null;
            }

        }

        /// <summary>
        /// 終了処理
        /// いるのかなこれ？？
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closed(object sender, EventArgs e)
        {
            //kinectChooser.Stop();
        }

        /// <summary>
        /// 頭、両手の位置情報取得
        /// 関節表示
        /// </summary>
        /// <param name="skeletonFrame"></param>
        private void ShowSkeleton(Skeleton skeleton)
        {
            // clear canvas
            canvasSkeleton.Children.Clear();

            // draw skeleton
            foreach ( Joint joint in skeleton.Joints)
            {
                JointType jointType = joint.JointType;
                //tracked skeleton
                if ( joint.TrackingState != JointTrackingState.NotTracked )
                {
                    switch (jointType)
                    {
                        case JointType.Head:
                            posHeadXYZ.x = joint.Position.X;
                            posHeadXYZ.y = joint.Position.Y;
                            posHeadXYZ.z = joint.Position.Z;
                            DrawJoint(joint, Colors.Yellow);
                            break;
                        case JointType.HandRight:
                            posHandRightXYZ.x = joint.Position.X;
                            posHandRightXYZ.y = joint.Position.Y;
                            posHandRightXYZ.z = joint.Position.Z;
                            DrawJoint(joint, Colors.Yellow);
                            break;
                        case JointType.HandLeft:
                            posHandLeftXYZ.x = joint.Position.X;
                            posHandLeftXYZ.y = joint.Position.Y;
                            posHandLeftXYZ.z = joint.Position.Z;
                            DrawJoint(joint, Colors.Yellow);
                            break;
                        default:
                            DrawJoint(joint, Colors.Gray);
                            break;
                    }
                }
            }

        }

        /// <summary>
        /// canvasSkeleton上に関節を与えられた色の円で表示
        /// </summary>
        /// <param name="joint"></param>
        /// <param name="gray"></param>
        private void DrawJoint(Joint joint, Color color)
        {
            // 骨格座標をカラー座標に変換
            ColorImagePoint point = kinect.CoordinateMapper.MapSkeletonPointToColorPoint(joint.Position, kinect.ColorStream.Format);
            // draw circles
            canvasSkeleton.Children.Add(new Ellipse()
            {
                Margin = new Thickness(point.X, point.Y, 0, 0),
                Fill = new SolidColorBrush(color),
                Width = 20,
                Height = 20,
            });
        }

        /// <summary>
        /// 顔を認識し回転のパラメータを取得
        /// </summary>
        /// <param name="colorFrame"></param>
        /// <param name="depthFrame"></param>
        /// <param name="skeletonFrame"></param>
        private void FaceDataAcquisition(ColorImageFrame colorFrame, DepthImageFrame depthFrame, Skeleton skeleton)
        {
            FaceTrackFrame faceFrame = faceTracker.Track(colorFrame.Format, colorFrame.ToPixelData(),
                depthFrame.Format, depthFrame.ToPixelData(), skeleton);
            if (faceFrame.TrackSuccessful)
            {
                rotHeadXYZ.x = faceFrame.Rotation.X;
                rotHeadXYZ.y = faceFrame.Rotation.Y;
                rotHeadXYZ.z = faceFrame.Rotation.Z;

                // トラッキング状態を表示
                statusTextBlock.Text = "Status: Tracked";

                // 送信
                SendOSCMessage(rotHeadXYZ, posHeadXYZ, posHandRightXYZ, posHandLeftXYZ);
            } else
            {
                // トラッキング状態を表示
                statusTextBlock.Text = "Status: NOT Tracked";
            }
        }

        /// <summary>
        /// 回転角や位置情報をOSCにより送信
        /// </summary>
        /// <param name="rotHeadXYZ"></param>
        /// <param name="posHeadXYZ"></param>
        /// <param name="posHandRightXYZ"></param>
        /// <param name="posHandLeftXYZ"></param>
        private void SendOSCMessage(Vector3DF rotHeadXYZ, Vector3DF posHeadXYZ, Vector3DF posHandRightXYZ, Vector3DF posHandLeftXYZ)
        {
            // OSCメッセージ生成
            OscMessage rotHeadOsc = new OscMessage(from, "/head_rot", null);
            OscMessage posHeadOsc = new OscMessage(from, "/head_pos", null);
            OscMessage posHandRightOsc = new OscMessage(from, "/hand_r", null);
            OscMessage posHandLeftOsc = new OscMessage(from, "/hand_l", null);

            // 情報の追加
            rotHeadOsc.Append<float>(rotHeadXYZ.x);
            rotHeadOsc.Append<float>(rotHeadXYZ.y);
            rotHeadOsc.Append<float>(rotHeadXYZ.z);
            posHeadOsc.Append<float>(posHeadXYZ.x);
            posHeadOsc.Append<float>(posHeadXYZ.y);
            posHeadOsc.Append<float>(posHeadXYZ.z);
            posHandRightOsc.Append<float>(posHandRightXYZ.x);
            posHandRightOsc.Append<float>(posHandRightXYZ.y);
            posHandRightOsc.Append<float>(posHandRightXYZ.z);
            posHandLeftOsc.Append<float>(posHandLeftXYZ.x);
            posHandLeftOsc.Append<float>(posHandLeftXYZ.y);
            posHandLeftOsc.Append<float>(posHandLeftXYZ.z);

            // 送信
            rotHeadOsc.Send(toExp);
            posHeadOsc.Send(toExp);
            posHandRightOsc.Send(toExp);
            posHandLeftOsc.Send(toExp);
        }
        
    }
}
