
using System;
using System.Drawing;
using System.Windows.Forms;
using Accord.Imaging;
using Accord.Imaging.Filters;
using Accord.Math;
using AForge;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Collections;

namespace Panorama
{
    public partial class MainForm : Form
    {
        private Bitmap img1 = new Bitmap("C:\\Users\\SHUBHAM\\Desktop\\MatrixWork\\Panorama\\accord-panorama-src\\Samples\\Imaging\\Panorama\\Resources\\DJI_0137.JPG");
        private Bitmap img2 = new Bitmap("C:\\Users\\SHUBHAM\\Desktop\\MatrixWork\\Panorama\\accord-panorama-src\\Samples\\Imaging\\Panorama\\Resources\\DJI_0138.JPG");
        BitmapData bData2;
        ArrayList measurements = new ArrayList();
        PointF lastPos = new PointF(0, 0);
        Boolean manualMatching = false;
        private IntPoint[] harrisPoints1;
        private IntPoint[] harrisPoints2;

        private IntPoint[] correlationPoints1;
        private IntPoint[] correlationPoints2;

        private MatrixH homography;


        public MainForm()
        {
            InitializeComponent();
            Cursor cur = Cursors.Default;
            this.Cursor = cur;

            // Concatenate and show entire image at start
            Concatenate concatenate = new Concatenate(img1);
            pictureBox.Image = concatenate.Apply(img2);
        }
        private void showToolStripMenuItem_Click(object sender, EventArgs e)
        {
          
        }

        private void btnHarris_Click(object sender, EventArgs e)
        {

            // Step 1: Detect feature points using Harris Corners Detector
            HarrisCornersDetector harris = new HarrisCornersDetector(0.04f, 1000f);
            harrisPoints1 = harris.ProcessImage(img1).ToArray();
            harrisPoints2 = harris.ProcessImage(img2).ToArray();

            // Show the marked points in the original images
            Bitmap img1mark = new PointsMarker(harrisPoints1).Apply(img1);
            Bitmap img2mark = new PointsMarker(harrisPoints2).Apply(img2);

            // Concatenate the two images together in a single image (just to show on screen)
            Concatenate concatenate = new Concatenate(img1mark);
            pictureBox.Image = concatenate.Apply(img2mark);
        }
        private void drawCursor(PictureBox pBox, int x, int y, BitmapData bData1)
        {
            Point img2Matched = new Point(x + (pictureBox.ClientSize.Width/2), y);
            Graphics g1 = pBox.CreateGraphics();
            Debug.WriteLine("Matching position "+ img2Matched.X.ToString()+","+ img2Matched.Y.ToString ());
            g1.DrawLine(Pens.Red, new Point(img2Matched.X - 5, img2Matched.Y - 5), new Point(img2Matched.X + 5, img2Matched.Y + 5));
            g1.DrawLine(Pens.Red, new Point(img2Matched.X + 5, img2Matched.Y - 5), new Point(img2Matched.X - 5, img2Matched.Y + 5));
        }
        private void btnCorrelation_Click(object sender, EventArgs e)
        {
            // Step 2: Match feature points using a correlation measure
            CorrelationMatching matcher = new CorrelationMatching(9);
            IntPoint[][] matches = matcher.Match(img1, img2, harrisPoints1, harrisPoints2);

            // Get the two sets of points
            correlationPoints1 = matches[0];
            correlationPoints2 = matches[1];
            
            // Concatenate the two images in a single image (just to show on screen)
            Concatenate concat = new Concatenate(img1);
            Bitmap img3 = concat.Apply(img2);

            // Show the marked correlations in the concatenated image
            PairsMarker pairs = new PairsMarker(
                correlationPoints1, // Add image1's width to the X points to show the markings correctly
                correlationPoints2.Apply(p => new IntPoint(p.X + img1.Width, p.Y)));

            pictureBox.Image = pairs.Apply(img3);  
        }

        private void btnRansac_Click(object sender, EventArgs e)
        {
            // Step 3: Create the homography matrix using a robust estimator
            RansacHomographyEstimator ransac = new RansacHomographyEstimator(0.001, 0.99);
            homography = ransac.Estimate(correlationPoints1, correlationPoints2);

            // Plot RANSAC results against correlation results
            IntPoint[] inliers1 = correlationPoints1.Submatrix(ransac.Inliers);
            IntPoint[] inliers2 = correlationPoints2.Submatrix(ransac.Inliers);

            // Concatenate the two images in a single image (just to show on screen)
            Concatenate concat = new Concatenate(img1);
            Bitmap img3 = concat.Apply(img2);

            // Show the marked correlations in the concatenated image
            PairsMarker pairs = new PairsMarker(
                inliers1, // Add image1's width to the X points to show the markings correctly
                inliers2.Apply(p => new IntPoint(p.X + img1.Width, p.Y)));

            pictureBox.Image = pairs.Apply(img3);
        }

        private void btnBlend_Click(object sender, EventArgs e)
        {
            // Step 4: Project and blend the second image using the homography
            Blend blend = new Blend(homography, img1);
            pictureBox.Image = blend.Apply(img2);
        }

        private void btnDoItAll_Click(object sender, EventArgs e)
        {
            // Do it all
            btnHarris_Click(sender, e);
            btnCorrelation_Click(sender, e);
            btnRansac_Click(sender, e);
            btnBlend_Click(sender, e);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {

        }

        private void openLeftImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // open file dialog   
            OpenFileDialog open = new OpenFileDialog();
            if (open.ShowDialog() == DialogResult.OK)
            {
                // display image in picture box  
                img1 = new Bitmap(open.FileName);
            }
        }

        private void addRightImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog();
            DialogResult dr1 = open.ShowDialog();
            if (dr1 == DialogResult.OK)
            {
                img2 = new Bitmap(open.FileName);
            }
            bData2 = img2.LockBits(new Rectangle(0, 0, img2.Width, img2.Height), ImageLockMode.ReadOnly, img2.PixelFormat);
            Debug.WriteLine("Inside Read image function");
        }
        private void saveOutputToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Images | *.png;*.bmp;*jpg";
            ImageFormat format = ImageFormat.Png;
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                string ext = Path.GetExtension(sfd.FileName);
                switch (ext)
                {
                    case ".jpg":
                        format = ImageFormat.Jpeg;
                        break;
                    case ".bmp":
                        format = ImageFormat.Bmp;
                        break;
                }
                pictureBox.Image.Save(sfd.FileName, format);
            }
        }     

        private void pictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            //this.Cursor = new Cursor(Cursor.Current.Handle);

            //int xCoordinate = Cursor.Position.X;
            //int yCoordinate = Cursor.Position.Y;

            //Debug.WriteLine(xCoordinate.ToString(), yCoordinate.ToString());
        }

        public unsafe Point ThresholdUA(BitmapData bDataR, BitmapData bDataL)
        {
            StreamWriter sw1 = new StreamWriter("C:\\Users\\SHUBHAM\\Desktop\\MatrixWork\\Panorama\\accord-panorama-src\\log.txt");
            byte bitsPerPixel = Convert.ToByte(bDataL.Stride / img1.Width);
            //This time we convert the IntPtr to a ptr
            byte* bDataL_scan0 = (byte*)bDataL.Scan0.ToPointer();
            byte* bDataR_scan0 = (byte*)bDataR.Scan0.ToPointer();
            int size = 5;
            Double minSum = 99999.0F;
            Point matchingPos = new Point(0, 0);
            for (int i = 0; i < bData2.Height-size; i++)
            {
                for (int j = 0; j < bData2.Width-size; j++)
                { 
                    double sum = 0;
                    for (int p = 0; p < size; p++)
                        for (int q = 0; q < size; q++)
                        {
                            byte* bDataRp = bDataR_scan0 + (i + p) * bDataR.Stride + (j + q) * bitsPerPixel;
                            byte* bDataLp = bDataL_scan0 + p * bDataL.Stride + q * bitsPerPixel;
                            byte R1 = bDataLp[2];
                            byte G1 = bDataLp[1];
                            byte B1 = bDataLp[0];
                            byte R2 = bDataRp[2];
                            byte G2 = bDataRp[1];
                            byte B2 = bDataRp[0];
                            Double diffR = Math.Abs(R1 - R2); 
                            Double diffG = Math.Abs(G1 - G2);
                            Double diffB = Math.Abs(B1 - B2);
                            sum += diffR * diffR + diffG * diffG + diffB * diffB;
                        }
                    if (sum < minSum)
                    {
                        minSum = sum; matchingPos = new Point(i, j);
                        sw1.WriteLine("i=" + i.ToString() + "    j=" + j.ToString() + "     Sum:" + sum.ToString() + "      matchingPos" + matchingPos.ToString());
                    }
                }
            }
            Debug.WriteLine("Inside ThresholdUA "+matchingPos.X.ToString()+", "+matchingPos.Y.ToString());
            sw1.Close();
            return matchingPos;
        }

        private void pictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            this.Cursor = new Cursor(Cursor.Current.Handle);

            int xCoordinate = Cursor.Position.X;
            int yCoordinate = Cursor.Position.Y;

            Debug.WriteLine(xCoordinate.ToString(), yCoordinate.ToString());
            if (manualMatching == true)
            {
                if (lastPos.X == 0 && lastPos.Y == 0)
                    lastPos = new PointF(Cursor.Position.X, Cursor.Position.Y);
                else
                {

                    PointF[] measurement = new PointF[2];
                    measurement[0]=lastPos;
                    measurement[1]= new PointF(Cursor.Position.X, Cursor.Position.Y);
                    measurements.Add(measurement); lastPos = new PointF(0, 0);
                }
            }
            else
            { 
                BitmapData bDataL;
                bDataL = img1.LockBits(new Rectangle(xCoordinate, yCoordinate, xCoordinate+5, yCoordinate+5), ImageLockMode.ReadOnly, img2.PixelFormat);
                Point image2Matched =  ThresholdUA(bData2, bDataL);
                drawCursor(pictureBox, image2Matched.X, image2Matched.Y, bDataL);
                img1.UnlockBits(bDataL);
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            img2.UnlockBits(bData2);
        }

        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void finishToolStripMenuItem_Click(object sender, EventArgs e)
        {
            manualMatching = false;
            lastPos = new PointF(0, 0);
        }

        private void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            manualMatching = true;
            lastPos = new PointF(0, 0);
        }
    }
}
