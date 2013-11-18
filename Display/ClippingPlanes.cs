//
// ClippingPlanes.cs
// RhinoMobile.Display
//
// Created by dan (dan@mcneel.com) on 9/17/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
//
using System;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace RhinoMobile.Display
{
	public struct ClippingInfo
	{
		// CalcBoundingBox sets bbox.
		// Often it is too big, but generally contains
		// everything core Rhino knows needs to be drawn.
		// ReSharper disable FieldCanBeMadeReadOnly.Local
		// Analysis disable InconsistentNaming
		public BoundingBox bbox;


		// CalcClippingPlanes sets 
		// min_near_dist and min_near_over_far based on 
		// video hardware capabilities.   If you want high 
		// quality display, make sure your code sets near 
		// and far so that
		//    bbox_near >= min_near_dist
		//    bbox_near >= min_near_over_far*bbox_far
		public double min_near_dist;
		public double min_near_over_far;

		// Rhino sets this to be the distance from the camera
		// to the target.  It can generally be ignored.
		// In situations where bbox_near and bbox_far are too
		// far apart, target_dist can be used as a hint about
		// what area of the frustum is the most important to
		// show.
		public double target_dist;

		// You can override the virtual function
		// CalcClippingPlanes() 
		// and adjust the values of
		//   m_Clipping.bbox_near
		//   m_Clipping.bbox_far
		// If you set them incorrectly, Rhino will ignore 
		// your request.
		//    bbox_near >  0
		//    bbox_far  >  bbox_near
		public double bbox_near;
		public double bbox_far;

		// These fields are set but not used.
		// Changing them does not change the
		// the view projection.
		public double left,right;
		public double top,bottom;
		// ReSharper restore FieldCanBeMadeReadOnly.Local
		// Analysis restore InconsistentNaming
	};

	public static class ClippingPlanes
	{
		#region methods
		private static bool CalcClippingPlanesInternal (ViewportInfo vp, ClippingInfo clipping) 
		{
			bool isPerspective = vp.IsPerspectiveProjection;

			// iCalcBoundingBox() has set clipping.bbox and it cannot
			// be changed or ignored.
			GetBoundingBoxNearFarHelper (
				clipping.bbox, 
				isPerspective,
				vp.CameraLocation,
				vp.CameraZ, 
				clipping.bbox_near,
				clipping.bbox_far 
			);

			// Do sanity checks and update ON_Viewport frustum if it uses
			// parallel projection and near <= 0.0
			CalcClippingPlanesHelper(clipping.bbox_near, clipping.bbox_far, vp);

			// Set target_dist
			clipping.target_dist = (vp.CameraLocation - vp.TargetPoint)* vp.CameraZ;
			if ( double.IsNaN(clipping.target_dist) )
				clipping.target_dist = 0.5*(clipping.bbox_near + clipping.bbox_far);

			return true;
		}

		public static bool CalcClippingPlanes (ViewportInfo vp, ClippingInfo m_Clipping)
		{
			// Initialize m_Clipping frustum info. 
			// (left,right,top,bottom are not used but should be initialized).
			//const ON::view_projection projection = vp.Projection();

			vp.GetFrustum(out m_Clipping.left,      
			              out m_Clipping.right,
			              out m_Clipping.bottom,    
			              out m_Clipping.top,
			              out m_Clipping.bbox_near, 
			              out m_Clipping.bbox_far);

			m_Clipping.min_near_dist     = 0.000100;
			m_Clipping.min_near_over_far = 0.000100;
			m_Clipping.target_dist = (vp.CameraLocation - vp.TargetPoint)*vp.CameraZ;

			// Call virtual function that looks at m_Clipping.bbox and sets
			// m_Clipping.bbox_near and m_Clipping.bbox_far
			if ( !CalcClippingPlanesInternal( vp, m_Clipping ) )
				return false;  

			if ( double.IsNaN(m_Clipping.bbox_far) 
			    || double.IsNaN(m_Clipping.bbox_near)
			    || m_Clipping.bbox_far <= m_Clipping.bbox_near
			    || (vp.IsPerspectiveProjection && m_Clipping.bbox_near <= 1.0e-12)
			    || (vp.IsPerspectiveProjection  && m_Clipping.bbox_far > 1.0e16*m_Clipping.bbox_near)
			    || m_Clipping.bbox_near > 1.0e30
			    || m_Clipping.bbox_far  > 1.0e30
			    )
			{

				// Restore settings to something more sane
				vp.GetFrustum(out m_Clipping.left,      
				              out m_Clipping.right,
				              out m_Clipping.bottom,    
				              out m_Clipping.top,
				              out m_Clipping.bbox_near,  
				              out m_Clipping.bbox_far);

				m_Clipping.min_near_dist     =  0.000100;
				m_Clipping.min_near_over_far =  0.000100;
				m_Clipping.target_dist = (vp.CameraLocation - vp.TargetPoint)*vp.CameraZ;
			}

			return true;
		}

		private static bool AdjustFrustum (ViewportInfo vp, ClippingInfo clipping)
		{
			vp.SetFrustumNearFar (vp.FrustumNear * 0.1, vp.FrustumFar * 10);
			return true;
		}

		public static bool SetupFrustum (ViewportInfo vp, ClippingInfo clipping) 
		{
			double    n0 = vp.FrustumNear;
			double    f0 = vp.FrustumFar;

			// Because picking relies heavily on the projection, we first set the
			// viewport frustum here, capture and save it, then let the conduits
			// do what ever they want with it...eventually the viewport will be put
			// back to the captured state before leaving the pipeline...
			ClippingInfo m_SavedClipping = clipping;
			vp.SetFrustumNearFar(clipping.bbox_near, clipping.bbox_far, 
			                     clipping.min_near_dist, clipping.min_near_over_far, 
			                     clipping.target_dist 
			                     );

			vp.GetFrustum(out m_SavedClipping.left, 
			              out m_SavedClipping.right, 
			              out m_SavedClipping.bottom, 
			              out m_SavedClipping.top, 
			              out m_SavedClipping.bbox_near, 
			              out m_SavedClipping.bbox_far);

			// Next, set the values that the pipeline will actually use...  
			if (!(AdjustFrustum(vp, clipping)))
			    return false;

			return true;
		}

		private static void GetBoundingBoxNearFarHelper(BoundingBox bbox, 
																										bool isPerspective, 
																										Rhino.Geometry.Point3d camLoc, 
																										Rhino.Geometry.Vector3d camZ, 
																										double box_near, 
																										double box_far)
		{
			double n = 0.005;
			double f = 1000.0;
			if (bbox.IsValid)
			{
				Rhino.Geometry.Point3d p = new Point3d(0,0,0);
				double d;
				bool bFirstPoint = true;
				int i,j,k;

				for ( i = 0; i < 2; i++ )
				{
					p.X = bbox.GetCorners () [i].X;
					for ( j = 0; j < 2; j++)
					{
						p.Y = bbox.GetCorners()[j].Y;
						for ( k = 0; k < 2; k++)
						{
							p.Z = bbox.GetCorners()[k].Z;

							d = (camLoc-p)*camZ;
							if ( bFirstPoint )
							{
								n=d;
								f=d;
								bFirstPoint=false;
							}
							else
							{
								if (d < n)
									n = d;
								else if (d > f)
									f = d;
							}
						}
					}
				}
				// if n is invalid or f is invalid 
				if ( double.IsNaN(n) || double.IsNaN(f) || f < n )
				{
					n = 0.005;
					f = 1000.0;
				} else 	{
				
					// Bump things out just a bit so objects right on 
					// the edge of the bounding box do not land
					// on the near/far plane.
					if (isPerspective)
					{
						// perspective projection
						if ( f <= 1.0e-12 )
						{
							// everything is behind camera
							n = 0.005;
							f = 1000.0;
						}
						else if ( n <= 0.0 )
						{
							// grow f and handle n later
							f *= 1.01;
						}
						else if ( f <= n + 1.490116119385000000e-8 * n )
						{
							// 0 < n and f is nearly equal to n
							n *= 0.675;
							f *= 1.125;
							if ( f < 1.0e-6 )
								f = 1.0e-6;
						}
						else 
						{
							n *= 0.99;
							f *= 1.01;
						}
					}
					else // parallel projection
					{
						// with a parallel projection, we just add a 5% buffer.
						// The next step will move the camera back if n is negative.
						d = 0.05*Math.Abs(f-n);
						if ( d < 0.5 )
							d = 0.5;
						n -= d;
						f += d;
					}
				}
			}

			if (!double.IsNaN(box_near))
				box_near = n;
			if (!double.IsNaN(box_far))
				box_far = f;
		}

		private static void NegativeNearClippingHelper(double near_dist, double far_dist, ViewportInfo vp)
		{
			double n = near_dist;
			double f = far_dist;

			double min_near_dist = 0.000100;
			if ( double.IsNaN(min_near_dist) || min_near_dist < 1.0e-6 )
				min_near_dist = 1.0e-6;
			if ( vp.IsParallelProjection && n < min_near_dist )
			{
				// move camera back in ortho projection so everything shows
				double d = 1.00001*min_near_dist - n;
				if ( d < 0.005 )
					d = 0.005;
				n += d;
				f  += d;
				if (   double.IsNaN(d)
				    || d <= 0.0
				    || double.IsNaN(n)
				    || double.IsNaN(f)
				    || n < min_near_dist 
				    || f <= n
				    )
				{
					// Just give up but ... refuse to accept garbage
					n = 0.005;
					f = 1000.0;
				}
				else
				{
					Rhino.Geometry.Point3d new_loc = vp.CameraLocation + d*vp.CameraZ;          
					vp.SetCameraLocation( new_loc );
				}
				near_dist = n;
				far_dist = f;
			}
		}

		static void CalcClippingPlanesHelper(double near_dist, double far_dist, ViewportInfo vp)
		{
			// The only thing this function should do is make sure ortho cameras are
			// moved so near is > 0.  Everything else should be considered and emergency
			// fix for garbage input.
			double n = near_dist;
			double f = far_dist;
			double min_near_dist = 0.000100;
			if ( double.IsNaN(min_near_dist) || min_near_dist < 1.0e-6 )
				min_near_dist = 1.0e-6;

			if (!double.IsNaN(n) && !double.IsNaN(f) )
			{
				NegativeNearClippingHelper(n,f,vp);
				if ( n < min_near_dist )
					n = min_near_dist;
				if ( f <= 1.00001*n )
					f = 10.0 + 100.0*n;
			}
			else
			{
				// If being nice didn't work - refuse to accept garbage
				n = 0.005;
				f = 1000.0;
			}

			near_dist = n;
			far_dist = f;
		}
		#endregion

	}
}