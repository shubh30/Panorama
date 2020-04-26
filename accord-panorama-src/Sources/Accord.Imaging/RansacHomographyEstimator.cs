﻿// Accord Imaging Library
// Accord.NET framework
// http://www.crsouza.com
//
// Copyright © César Souza, 2009-2010
// cesarsouza at gmail.com
//

namespace Accord.Imaging
{
    using System;
    using System.Drawing;
    using Accord.MachineLearning;
    using Accord.Math;
    using AForge;

    /// <summary>
    ///   RANSAC Robust Homography Matrix Estimator.
    /// </summary>
    /// 
    /// <remarks>
    /// <para>
    ///   Fitting a homography using RANSAC is pretty straightforward. Being a iterative method,
    ///   in a single iteration a random sample of four correspondences is selected from the 
    ///   given correspondence points and a homography H is then computed from those points.</para>
    /// <para>
    ///   The original points are then transformed using this homography and their distances to
    ///   where those transforms should be is then computed and matching points can classified
    ///   as inliers and non-matching points as outliers.</para>  
    /// <para>
    ///   After a given number of iterations, the iteration which produced the largest number
    ///   of inliers is then selected as the best estimation for H.</para>  
    ///   
    /// <para>
    ///   References:
    ///   <list type="bullet">
    ///     <item><description>
    ///       http://www.cs.ubc.ca/grads/resources/thesis/May09/Dubrofsky_Elan.pdf </description></item>
    ///     <item><description>
    ///       http://www.cc.gatech.edu/classes/AY2005/cs4495_fall/assignment4.pdf </description></item>
    ///   </list></para>
    /// </remarks>
    /// 
    public class RansacHomographyEstimator
    {
        private RANSAC<MatrixH> ransac;
        private int[] inliers;

        private PointF[] pointSet1;
        private PointF[] pointSet2;


        /// <summary>
        ///   Gets the RANSAC estimator used.
        /// </summary>
        public RANSAC<MatrixH> Ransac
        {
            get { return this.ransac; }
        }

        /// <summary>
        ///   Gets the final set of inliers detected by RANSAC.
        /// </summary>
        public int[] Inliers
        {
            get { return inliers; }
        }


        /// <summary>
        ///   Creates a new RANSAC homography estimator.
        /// </summary>
        /// <param name="threshold">Inlier threshold.</param>
        /// <param name="probability">Inlier probability.</param>
        public RansacHomographyEstimator(double threshold, double probability)
        {
            // Create a new RANSAC with the selected threshold
            ransac = new RANSAC<MatrixH>(4, threshold, probability);

            // Set RANSAC functions
            ransac.Fitting = homography;
            ransac.Degenerate = degenerate;
            ransac.Distances = distance;
        }

        /// <summary>
        ///   Matches two sets of points using RANSAC.
        /// </summary>
        /// <returns>The homography matrix matching x1 and x2.</returns>
        public MatrixH Estimate(Point[] points1, Point[] points2)
        {
            // Initial argument checkings
            if (points1.Length != points2.Length)
                throw new ArgumentException("The number of points should be equal.");

            if (points1.Length < 4)
                throw new ArgumentException("At least four points are required to fit an homography");

            PointF[] p1 = new PointF[points1.Length];
            PointF[] p2 = new PointF[points2.Length];
            for (int i = 0; i < points1.Length; i++)
            {
                p1[i] = new PointF(points1[i].X, points1[i].Y);
                p2[i] = new PointF(points2[i].X, points2[i].Y);
            }

            return Estimate(p1, p2);
        }

        /// <summary>
        ///   Matches two sets of points using RANSAC.
        /// </summary>
        /// <returns>The homography matrix matching x1 and x2.</returns>
        public MatrixH Estimate(IntPoint[] points1, IntPoint[] points2)
        {
            // Initial argument checkings
            if (points1.Length != points2.Length)
                throw new ArgumentException("The number of points should be equal.");

            if (points1.Length < 4)
                throw new ArgumentException("At least four points are required to fit an homography");

            PointF[] p1 = new PointF[points1.Length];
            PointF[] p2 = new PointF[points2.Length];
            for (int i = 0; i < points1.Length; i++)
            {
                p1[i] = new PointF(points1[i].X, points1[i].Y);
                p2[i] = new PointF(points2[i].X, points2[i].Y);
            }

            return Estimate(p1, p2);
        }

        /// <summary>
        ///   Matches two sets of points using RANSAC.
        /// </summary>
        /// <returns>The homography matrix matching x1 and x2.</returns>
        public MatrixH Estimate(PointF[] points1, PointF[] points2)
        {
            // Initial argument checkings
            if (points1.Length != points2.Length)
                throw new ArgumentException("The number of points should be equal.");

            if (points1.Length < 4)
                throw new ArgumentException("At least four points are required to fit an homography");


            // Normalize each set of points so that the origin is
            //  at centroid and mean distance from origin is sqrt(2).
            MatrixH T1, T2;
            this.pointSet1 = Tools.Normalize(points1, out T1);
            this.pointSet2 = Tools.Normalize(points2, out T2);


            // Compute RANSAC and find the inlier points
            MatrixH H = ransac.Compute(points1.Length, out inliers);

            if (inliers == null || inliers.Length < 4)
                //throw new Exception("RANSAC could not find enough points to fit an homography.");
                return null;


            // Compute the final homography considering all inliers
            H = homography(inliers);

            // Denormalise
            H = T2.Inverse() * (H * T1);

            return H;
        }

        /// <summary>
        ///   Estimates a homography with the given points.
        /// </summary>
        private MatrixH homography(int[] points)
        {
            // Retrieve the original points
            PointF[] x1 = this.pointSet1.Submatrix(points);
            PointF[] x2 = this.pointSet2.Submatrix(points);

            // Compute the homography
            return Tools.Homography(x1, x2);
        }

        /// <summary>
        ///   Compute inliers using the Symmetric Transfer Error,
        /// </summary>
        private int[] distance(MatrixH H, double t)
        {
            int n = pointSet1.Length;

            // Compute the projections (both directions)
            PointF[] p1 = H.TransformPoints(pointSet1);
            PointF[] p2 = H.Inverse().TransformPoints(pointSet2);

            // Compute the distances
            double[] d2 = new double[n];
            for (int i = 0; i < n; i++)
            {
                // Compute the distance as
                float ax = pointSet1[i].X - p2[i].X;
                float ay = pointSet1[i].Y - p2[i].Y;
                float bx = pointSet2[i].X - p1[i].X;
                float by = pointSet2[i].Y - p1[i].Y;
                d2[i] = (ax * ax) + (ay * ay) + (bx * bx) + (by * by);
            }

            // Find and return the inliers
            return Matrix.Find(d2, z => z < t);
        }

        /// <summary>
        ///   Checks if the selected points will result in a degenerate homography.
        /// </summary>
        private bool degenerate(int[] points)
        {
            PointF[] x1 = this.pointSet1.Submatrix(points);
            PointF[] x2 = this.pointSet2.Submatrix(points);

            // If any three of the four points in each set is colinear,
            //  the resulting homography matrix will be degenerate.

            return Tools.Colinear(x1[0], x1[1], x1[2]) ||
                   Tools.Colinear(x1[0], x1[1], x1[3]) ||
                   Tools.Colinear(x1[0], x1[2], x1[3]) ||
                   Tools.Colinear(x1[1], x1[2], x1[3]) ||

                   Tools.Colinear(x2[0], x2[1], x2[2]) ||
                   Tools.Colinear(x2[0], x2[1], x2[3]) ||
                   Tools.Colinear(x2[0], x2[2], x2[3]) ||
                   Tools.Colinear(x2[1], x2[2], x2[3]);
        }

    }
}
