using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
//using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using Microsoft.Win32;
using System.Windows.Threading;

/**
    * Copyright (c) 2021 Guangyan Huang (guangyan.huang@gmail.com)
    *
    * All rights reserved.
    * 1. load video
    * 2. extract one frame and save to a bmp/jpg/png file
    * 3. canvas to draw a tracking box
    * You may use, distribute and modify this code 
    * under the terms of the open source license.
    */

namespace WPFAppMovie
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
  
    public partial class MainWindow : Window
    {
        private MediaPlayer player;
        private bool playFlag = false; 
        //byte[] oldGrayValues;
        byte[] grayValues;// = new byte[nbytes];
        byte[] backgroundValues;
        int[] backgroundBmp;
        int iFrame;
        DispatcherTimer timer = new DispatcherTimer();
        private bool mediaPlayerIsPlaying = false;
        private static int nFrame;//total frames
        private static double timeOneFrame = 34.48;//1000/25 frames per second
        bool backgroundFlag = false;
        bool subtractBack = true;//if video is camptured by staic camera


        public MainWindow()
        {
            InitializeComponent();
            this.Width = 800;
            this.Height = 600;
            player = new MediaPlayer();
            player.MediaOpened += new EventHandler(player_OpenMedia);
            player.MediaEnded += new EventHandler(player_EndMedia);

            player.ScrubbingEnabled = true;
            player.Open(new Uri("C:\\Users\\HP\\Downloads\\WPFAppMovie\\WPFAppMovie\\WPFAppMovie\\movies\\Species173.mp4", UriKind.Relative));
            player.Position = TimeSpan.FromMilliseconds(1);

            //player.SpeedRatio = 0.5;

            VideoDrawing aVideoDrawing = new VideoDrawing();
            aVideoDrawing.Rect = new Rect(0, 0, 800, 600);
            aVideoDrawing.Player = player;
            DrawingBrush DBrush = new DrawingBrush(aVideoDrawing);
            this.Background = DBrush;

            if (playFlag)
            {
                // Play the video once.
                player.Play();
                // mediaPlayerIsPlaying = true;

            }

            label1.Content = player.Source.ToString();

            timer.Interval = TimeSpan.FromMilliseconds(timeOneFrame);
            timer.Tick += new EventHandler(timer_Tick); 
            timer.Start();

            // Write the text to a new file named "WriteFile.txt".
            File.WriteAllText(player.Source.ToString() + ".txt", player.Source.ToString());

        }

        private void timer_Tick(object sender, EventArgs e)
        {
            if (player.Source != null && player.NaturalDuration.HasTimeSpan)
            {
                slider1.Minimum = 0;
                slider1.Maximum = player.NaturalDuration.TimeSpan.TotalMilliseconds;
                slider1.Value = player.Position.TotalMilliseconds; 


                // Stop the watchdog timer
                timer.Stop();
                // process one frame of the video
                //Movie Analysis: Automatically segment the movie using Histgram change
                System.Windows.Size dpi = new System.Windows.Size(96, 96);
                DrawingVisual drawingVisual = new DrawingVisual();
                DrawingContext drawingContext = drawingVisual.RenderOpen();
                drawingContext.DrawVideo(player, new Rect(0, 0, player.NaturalVideoWidth, player.NaturalVideoHeight));
                drawingContext.Close();

                RenderTargetBitmap wbm = new RenderTargetBitmap(player.NaturalVideoWidth, player.NaturalVideoHeight, dpi.Width, dpi.Height, PixelFormats.Pbgra32);
                wbm.Render(drawingVisual);
                //cal background
                if (backgroundFlag && subtractBack)                
                {
                    if (backgroundBmp != null)
                        calBackgroundBitmap(wbm);
                }
                else
                {
                    ProcessOneFrameBitmap(wbm);

                }

                SeekToNextVideoPosition();

            }
        }

        private void SeekToNextVideoPosition()
        {
            // If more frames remain to capture...
            
            if (player.Source != null && (player.Position < player.NaturalDuration))
            {
                // Seek to next position and start watchdog timer
                iFrame++;
                player.Position = TimeSpan.FromMilliseconds(timeOneFrame * iFrame);
                timer.Start();
            }
            else
            {
                // Done; close media file and stop processing
                if(backgroundFlag)
                saveBackgroundBitmap();
                player.Close();
                
            }
        }

        private void player_OpenMedia(object sender, EventArgs e)

        {
            label1.Content = player.Source.ToString();
            if (subtractBack)
            {
                if (backgroundFlag)
                {
                    backgroundBmp = new int[player.NaturalVideoWidth * player.NaturalVideoHeight * 4 + 54];
                    nFrame = (int)(player.NaturalDuration.TimeSpan.TotalMilliseconds / timeOneFrame);
                }
                else
                {
                    string filename = player.Source.ToString() + "_background.bmp";
                    backgroundValues = ConvertBitmapSourceToByteArray(filename);
                }
            
            }

        }

        private void player_EndMedia(object sender, EventArgs e)
        {

        }

        private void saveBackgroundBitmap()
        {
            //output Background to bmp file
            for (int y = 0; y < player.NaturalVideoHeight; y++)
            {
                for (int x = 0; x < player.NaturalVideoWidth; x++)
                {
                    int k = (y * player.NaturalVideoWidth + x) * 4 + 54;

                    grayValues[k] = (byte)(backgroundBmp[k] / nFrame);//Blue
                    grayValues[k + 1] = (byte)(backgroundBmp[k + 1] / nFrame);//Green
                    grayValues[k + 2] = (byte)(backgroundBmp[k + 2] / nFrame);//Red
                }
            }

            //save to files
            string filename = player.Source.ToString() + "_background.bmp";
            BitmapImage bmp1 = GetBitmapImage(grayValues);
            SaveBitmapImageToFile(bmp1, filename);


        }

        public static byte[] ConvertBitmapSourceToByteArray(string filepath)
        {
            var image = new BitmapImage(new Uri(filepath));
            byte[] data;
            BitmapEncoder encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (MemoryStream ms = new MemoryStream())
            {
                encoder.Save(ms);
                data = ms.ToArray();
            }
            return data;
        }

        /*
        public BitmapImage ConvertWriteableBitmapToBitmapImage(RenderTargetBitmap wbm)
        {
            BitmapImage bi = new BitmapImage();
            using (MemoryStream stream = new MemoryStream())
            {
                BmpBitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(wbm));
                encoder.Save(stream);
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bi.StreamSource = new MemoryStream(stream.ToArray()); //stream;
                bi.EndInit();
                bi.Freeze();
            }
            return bi;
        }*/

        public static byte[] GetBytesFromBitmap(RenderTargetBitmap wbm, int iFrame)
        {
            //BitmapImage bi = new BitmapImage();
            BmpBitmapEncoder encoder = new BmpBitmapEncoder();
            
            using (MemoryStream stream = new MemoryStream())
            {
            
                encoder.Frames.Add(BitmapFrame.Create(wbm));
                encoder.Save(stream);
                
                return stream.ToArray();
                //stream.Close();

            }
            
        }

        public static BitmapImage GetBitmapImage(byte[] imageArray)
        {
            using (var stream = new MemoryStream(imageArray))
            {
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }

        public static void SaveBitmapImageToFile(BitmapImage image, string filePath)
        {
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                encoder.Save(fileStream);
            }
        }
       

        private void Button_Click(object sender, RoutedEventArgs e)
        { 
            //to do ...
        }

        public void calBackgroundBitmap(RenderTargetBitmap wbm)
        {
            //BitmapImage bi = new BitmapImage();
            BmpBitmapEncoder encoder = new BmpBitmapEncoder();

            using (MemoryStream stream = new MemoryStream())
            {

                encoder.Frames.Add(BitmapFrame.Create(wbm));
                encoder.Save(stream);

                grayValues = new byte[stream.ToArray().Length];
                
                stream.ToArray().CopyTo(grayValues, 0);// GetBytesFromBitmap(wbm, -1);
                stream.Close();
            }
            //byte temp = 0;
           
            for (int y = 0; y < player.NaturalVideoHeight; y++)
            {
                for (int x = 0; x < player.NaturalVideoWidth; x++)
                {
                    int k = (y * player.NaturalVideoWidth + x) * 4 + 54;
                   

                    backgroundBmp[k] = backgroundBmp[k]+(int)grayValues[k];//Blue;
                    backgroundBmp[k+1] = backgroundBmp[k + 1]+(int)grayValues[k + 1];//Green
                    backgroundBmp[k+2] = backgroundBmp[k + 2]+(int)grayValues[k + 2];//Red
                }
            }
        }

        public void subtractBackground(byte [] backgroundVal, byte [] grayVal)
        {
            for (int y = 0; y < player.NaturalVideoHeight; y++)
            {
                for (int x = 0; x < player.NaturalVideoWidth; x++)
                {
                    int k = (y * player.NaturalVideoWidth + x) * 4 + 54;


                    grayVal[k] = (byte) Math.Max(0,grayVal[k]-backgroundVal[k]);//Blue;
                    grayVal[k + 1] = (byte) Math.Max(0, grayVal[k+1] - backgroundVal[k+1]);//Green
                    grayVal[k + 2] = (byte) Math.Max(0, grayVal[k+2] - backgroundVal[k+2]);//Red
                }
            }

        }

       
        public void ProcessOneFrameBitmap (RenderTargetBitmap wbm)
        {
            
            BmpBitmapEncoder encoder = new BmpBitmapEncoder();

            using (MemoryStream stream = new MemoryStream())
            {

                encoder.Frames.Add(BitmapFrame.Create(wbm));
                encoder.Save(stream);

                grayValues = new byte[stream.ToArray().Length];

                stream.ToArray().CopyTo(grayValues, 0);
                stream.Close();
            }
            // plot a rectangle

            byte[] originalGrayValues = new byte[grayValues.Length];

            if (subtractBack)
            {
                grayValues.CopyTo(originalGrayValues, 0);
                if (!backgroundFlag && backgroundValues!=null)
                {
                    subtractBackground(backgroundValues, grayValues);
                }
            }

           for (int y = 0; y < player.NaturalVideoHeight; y++)
            {
                for (int x = 0; x < player.NaturalVideoWidth; x++)
                {
                    int k = (y * player.NaturalVideoWidth + x) * 4 + 54;
                    byte r, g, b;
                    //insert your new algorithm here to process an image frame.
                    //eg, draw a tracking box here
                    b = grayValues[k];//Blue;
                    g = grayValues[k + 1];//Green
                    r = grayValues[k + 2];//Red
                    
                }
            }

            //save to files
            if (true)
            {
                string filename = player.Source.ToString() + "_" + iFrame.ToString() + ".bmp";

                BitmapImage bmp1 = GetBitmapImage(grayValues);
                SaveBitmapImageToFile(bmp1, filename);
            }
            
            if (false)
            {
                if (subtractBack && !backgroundFlag)
                {
                    string oriFilename = player.Source.ToString() + "_oringinal" + iFrame.ToString() + ".bmp";

                    BitmapImage oriBmp = GetBitmapImage(originalGrayValues);
                    SaveBitmapImageToFile(oriBmp, oriFilename);
                }
            }
                               
            File.AppendAllText(player.Source.ToString() + ".txt", Environment.NewLine);
            File.AppendAllText(player.Source.ToString() + ".txt", iFrame.ToString());

        }

        /*
        private void DrawLine(Canvas canvas1, int x1, int y1, int x2, int y2, int thickness, System.Windows.Media.Brush brush)
        {
            Line line1 = new Line();
            line1.Stroke = brush;
            line1.X1 = x1;
            line1.X2 = x2;
            line1.Y1 = y1;
            line1.Y2 = y2;
            line1.HorizontalAlignment = HorizontalAlignment.Left;
            line1.VerticalAlignment = VerticalAlignment.Center;
            line1.StrokeThickness = thickness;
            canvas1.Children.Add(line1);
        }

        private void DrawDot(Canvas canvas1, int x1, int y1, int thickness, System.Windows.Media.Brush brush)
        {
            Line line1 = new Line();
            line1.Stroke = brush;
            line1.X1 = x1;
            line1.X2 = x1+ thickness;
            line1.Y1 = y1;
            line1.Y2 = y1;
            line1.HorizontalAlignment = HorizontalAlignment.Left;
            line1.VerticalAlignment = VerticalAlignment.Center;
            line1.StrokeThickness = thickness;
            canvas1.Children.Add(line1);
        }

        private void Text(Canvas canvas1, double x, double y, string text, System.Windows.Media.Color color)
        {

            TextBlock textBlock = new TextBlock();
            textBlock.Text = text;
            textBlock.Foreground = new SolidColorBrush(color);
            //textBlock.FontStyle = new Font("New Timer", 8);
            Canvas.SetLeft(textBlock, x);
            Canvas.SetTop(textBlock, y);
            canvas1.Children.Add(textBlock);

        }
        */

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            player.Play();
            timer.Start();
            mediaPlayerIsPlaying = true;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            player.Pause();
            timer.Stop();
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            player.Stop();
            timer.Stop();
            mediaPlayerIsPlaying = false;
        }

        //select and open a video file
        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.AddExtension = true;
            ofd.DefaultExt = "*.*";
            ofd.Filter = "Media(*.*)|*.*";
            ofd.ShowDialog();
            InitializeComponent();
            player.MediaOpened += new EventHandler(player_OpenMedia);
            player.Open(new Uri(ofd.FileName, UriKind.Relative));
            player.ScrubbingEnabled = true;
            player.Position = TimeSpan.FromMilliseconds(1);//TimeSpan.FromSeconds(1);
            
            VideoDrawing aVideoDrawing = new VideoDrawing();
            aVideoDrawing.Rect = new Rect(0, 0, 800, 600);
            aVideoDrawing.Player = player;
            DrawingBrush DBrush = new DrawingBrush(aVideoDrawing);
            this.Background = DBrush;

            timer.Interval = TimeSpan.FromMilliseconds(timeOneFrame); // TimeSpan.FromSeconds(1);
            timer.Tick += new EventHandler(timer_Tick);
        //    timer.Start();
            //player.Source = new Uri(ofd.FileName);

        }

       private void slider1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
           
        //    player.Position = TimeSpan.FromMilliseconds(slider1.Value);
             
        }
    }
}
